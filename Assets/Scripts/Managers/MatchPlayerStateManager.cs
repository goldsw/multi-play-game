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

        public void Begin(NetworkManager manager, IReadOnlyList<ulong> playerIds)
        {
            if (!manager.IsServer)
                return;

            network = manager;
            participants.Clear();
            foreach (ulong clientId in playerIds)
            {
                if (manager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                    participants.Add(client.PlayerObject);
            }
            if (participants.Count > 1)
                SetTagger(participants[Random.Range(0, participants.Count)]);
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
            if (network == null || !network.IsServer || player == null || !participants.Contains(player) ||
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

        private void FixedUpdate()
        {
            if (network == null || !network.IsServer || network.ShutdownInProgress ||
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
