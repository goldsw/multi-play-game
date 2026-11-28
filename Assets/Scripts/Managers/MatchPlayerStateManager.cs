using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class MatchPlayerStateManager : MonoBehaviour
    {
        private const double EliminationPresentationSeconds = 2d;
        private SkillCaster tagger;
        private readonly List<NetworkObject> participants = new();
        private readonly HashSet<ulong> contacts = new();
        private NetworkManager network;
        private SessionNetworkState sessionState;
        private bool matchEnded;

        // Host only. Raised once with the final ranking when the match is ended.
        public event Action<IReadOnlyList<MatchResultEntry>> MatchEnded;

        public void Begin(NetworkManager manager, IReadOnlyList<ulong> playerIds, SessionNetworkState state = null)
        {
            if (!manager.IsServer)
                return;

            network = manager;
            sessionState = state;
            matchEnded = false;
            participants.Clear();
            foreach (ulong clientId in playerIds)
            {
                if (manager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                    participants.Add(client.PlayerObject);
            }
            if (participants.Count > 1)
                SetTagger(participants[UnityEngine.Random.Range(0, participants.Count)]);
        }

        public bool SetTagger(NetworkObject player)
        {
            if (player == null || !player.IsSpawned || !player.IsPlayerObject ||
                player.gameObject.scene != gameObject.scene ||
                !player.TryGetComponent<SkillCaster>(out var next) ||
                !player.TryGetComponent<CharacterStateMachine>(out var state) || !state.IsServer || state.IsEliminated)
                return false;

            if (next == tagger)
                return true;

            ClearTagger();
            tagger = next;
            tagger.SetTagger(true);
            RecordContacts(tagger.NetworkObject);
            return true;
        }

        public void ClearTagger()
        {
            if (tagger != null && tagger.IsServer && tagger.IsSpawned)
                tagger.SetTagger(false);
            tagger = null;
            contacts.Clear();
        }

        public bool Eliminate(NetworkObject player)
        {
            if (network == null || !network.IsServer || matchEnded || player == null || !participants.Contains(player) ||
                !player.IsSpawned || !player.IsPlayerObject ||
                player.gameObject.scene != gameObject.scene ||
                !player.TryGetComponent<CharacterStateMachine>(out var state) || !state.IsServer || state.IsEliminated)
                return false;

            if (tagger != null && tagger.NetworkObject == player)
                ClearTagger();

            int rank = 0;
            foreach (var participant in participants)
            {
                if (participant != null && participant.IsSpawned &&
                    participant.TryGetComponent<CharacterStateMachine>(out var character) && !character.IsEliminated)
                    rank++;
            }
            state.SetEliminated(new EliminationResult(rank, network.ServerTime.Time + EliminationPresentationSeconds));
            GetComponent<EffectObjectManager>()?.DespawnOwned(player.OwnerClientId);
            return true;
        }

        // Entry point for the poison mushroom bomb rule (host only). The bomb carrier loses, the rest are
        // ranked in participant order, and the final ranking is published through MatchEnded.
        public bool EndMatchByExplosion()
        {
            if (network == null || !network.IsServer || network.ShutdownInProgress || matchEnded)
                return false;

            var alive = new List<NetworkObject>();
            foreach (var participant in participants)
            {
                if (IsAlive(participant))
                    alive.Add(participant);
            }
            if (alive.Count == 0)
                return false;

            // Worst rank first: the carrier, then the others from the back of the participant order.
            var losers = new List<NetworkObject>();
            if (alive.Count > 1 && tagger != null && alive.Contains(tagger.NetworkObject))
            {
                losers.Add(tagger.NetworkObject);
                alive.Remove(tagger.NetworkObject);
            }
            for (int index = alive.Count - 1; index > 0; index--)
                losers.Add(alive[index]);
            foreach (var loser in losers)
                Eliminate(loser);

            var results = new List<MatchResultEntry>();
            var lobbySlots = sessionState != null && sessionState.IsSpawned ? sessionState.GetSlots() : Array.Empty<LobbySlotData>();
            foreach (var participant in participants)
            {
                if (participant == null || !participant.TryGetComponent<CharacterStateMachine>(out var state))
                    continue;

                ulong clientId = participant.OwnerClientId;
                int slot = Array.FindIndex(lobbySlots, entry => entry.Occupied && entry.ClientId == clientId);
                int characterId = slot >= 0 ? lobbySlots[slot].CharacterId : LobbySettings.DefaultCharacterId;
                results.Add(new MatchResultEntry(clientId, characterId, state.IsEliminated ? state.Elimination.Rank : 1));
            }
            results.Sort((left, right) => left.Rank.CompareTo(right.Rank));

            matchEnded = true;
            ClearTagger();
            MatchEnded?.Invoke(results);
            return true;
        }

        private static bool IsAlive(NetworkObject player) =>
            player != null && player.IsSpawned && player.TryGetComponent<CharacterStateMachine>(out var state) &&
            !state.IsEliminated;

        private void FixedUpdate()
        {
            if (network == null || !network.IsServer || network.ShutdownInProgress || matchEnded ||
                tagger == null || !tagger.IsSpawned)
                return;

            var carrier = tagger.NetworkObject;
            NetworkObject target = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var player in participants)
            {
                if (!Touches(carrier, player))
                    continue;
                if (contacts.Contains(player.NetworkObjectId))
                    continue;

                float distance = (player.transform.position - carrier.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    target = player;
                }
            }

            if (target != null)
                SetTagger(target);
            else
                RecordContacts(carrier);
        }

        private void RecordContacts(NetworkObject carrier)
        {
            contacts.Clear();
            foreach (var player in participants)
            {
                if (Touches(carrier, player))
                    contacts.Add(player.NetworkObjectId);
            }
        }

        private static bool Touches(NetworkObject carrier, NetworkObject candidate)
        {
            if (candidate == null || candidate == carrier || !candidate.IsSpawned ||
                !carrier.TryGetComponent<Collider>(out var carrierCollider) ||
                !candidate.TryGetComponent<Collider>(out var candidateCollider) ||
                !candidate.TryGetComponent<CharacterStateMachine>(out var state) || state.IsEliminated)
                return false;

            var contactBounds = carrierCollider.bounds;
            contactBounds.Expand(new Vector3(0.16f, 0f, 0.16f));
            return contactBounds.Intersects(candidateCollider.bounds);
        }
    }
}
