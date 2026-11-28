using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class PlayerCollisionManager : MonoBehaviour
    {
        private const int MaxPlayers = LobbySettings.PlayerCount;
        private const float ContactSkin = 0.002f;
        private const float StaticPenetrationTolerance = 0.01f;
        private const float CorrectionToleranceSquared = 0.000001f;
        private const float CorrectionRetryInterval = 0.15f;

        private sealed class PlayerState
        {
            public NetworkObject Player;
            public Rigidbody Body;
            public BoxCollider Collider;
            public PlayerPositionCorrection Correction;
            public Vector3 Accepted;
            public Vector3 Observed;
            public Vector3 CenterOffset;
            public Vector3 HalfExtents;
            public Vector3 Delta;
            public Vector3 Normal;
            public Vector3 LastCorrection;
            public Vector3 LastNormal;
            public float NextCorrectionTime;
        }

        private readonly PlayerState[] tracked = new PlayerState[MaxPlayers];
        private readonly PlayerState[] active = new PlayerState[MaxPlayers];
        private readonly Collider[] staticHits = new Collider[32];
        [SerializeField] private NetworkManager networkManager;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("lobbyState")] private SessionNetworkState sessionState;
        private int activeCount;
        private uint sequence;

        public void Configure(NetworkManager manager, SessionNetworkState state)
        {
            networkManager = manager;
            sessionState = state;
            System.Array.Clear(tracked, 0, tracked.Length);
            System.Array.Clear(active, 0, active.Length);
            activeCount = 0;
        }

        private void FixedUpdate()
        {
            if (networkManager == null || !networkManager.IsHost || sessionState == null
                || sessionState.Status != SessionStatus.Playing)
            {
                System.Array.Clear(tracked, 0, tracked.Length);
                System.Array.Clear(active, 0, active.Length);
                activeCount = 0;
                return;
            }

            GatherPlayers();
            ResolvePlayerContacts();
            bool accept = CanAcceptPositions();
            float now = Time.fixedTime;
            sequence++;
            for (int i = 0; i < activeCount; i++)
            {
                PlayerState player = active[i];
                if (accept)
                    player.Accepted += player.Delta;

                if ((player.Observed - player.Accepted).sqrMagnitude <= CorrectionToleranceSquared)
                {
                    player.NextCorrectionTime = 0f;
                    continue;
                }

                bool sameCorrection = (player.LastCorrection - player.Accepted).sqrMagnitude
                    <= CorrectionToleranceSquared && player.LastNormal == player.Normal;
                if (sameCorrection && now < player.NextCorrectionTime)
                    continue;

                player.LastCorrection = player.Accepted;
                player.LastNormal = player.Normal;
                player.NextCorrectionTime = now + CorrectionRetryInterval;
                player.Correction.SendCorrection(player.Accepted, player.Normal, sequence);
            }
        }

        private void GatherPlayers()
        {
            for (int i = 0; i < tracked.Length; i++)
            {
                NetworkObject player = tracked[i]?.Player;
                if (player == null || !player.IsSpawned
                    || !networkManager.ConnectedClients.TryGetValue(player.OwnerClientId, out NetworkClient client)
                    || client.PlayerObject != player)
                    tracked[i] = null;
            }

            System.Array.Clear(active, 0, active.Length);
            activeCount = 0;
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (activeCount == MaxPlayers)
                    break;

                NetworkObject player = networkManager.ConnectedClients[clientId].PlayerObject;
                if (player == null || !player.IsSpawned)
                    continue;

                PlayerState state = GetOrTrack(player);
                if (state == null)
                    continue;

                state.Observed = state.Body.position;
                state.Delta = state.Observed - state.Accepted;
                state.Normal = Vector3.zero;
                if (!IsClearOfStatics(state, state.Observed, out Vector3 staticNormal))
                {
                    state.Delta = Vector3.zero;
                    state.Normal = staticNormal;
                }
                active[activeCount++] = state;
            }
        }

        private PlayerState GetOrTrack(NetworkObject player)
        {
            int freeSlot = -1;
            for (int i = 0; i < tracked.Length; i++)
            {
                if (tracked[i]?.Player == player)
                    return tracked[i];
                if (tracked[i] == null && freeSlot < 0)
                    freeSlot = i;
            }

            if (freeSlot < 0 || !player.TryGetComponent(out Rigidbody body)
                || !player.TryGetComponent(out BoxCollider box)
                || !player.TryGetComponent(out PlayerPositionCorrection correction))
                return null;

            Vector3 scale = box.transform.lossyScale;
            PlayerState state = new PlayerState
            {
                Player = player,
                Body = body,
                Collider = box,
                Correction = correction,
                Accepted = body.position,
                CenterOffset = Vector3.Scale(box.center, scale),
                HalfExtents = Vector3.Scale(box.size * 0.5f,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)))
            };
            tracked[freeSlot] = state;
            return state;
        }

        private void ResolvePlayerContacts()
        {
            // Clipping a path can introduce a contact with a third player. Recheck all pairs.
            for (int pass = 0; pass < MaxPlayers * MaxPlayers; pass++)
            {
                float earliest = float.PositiveInfinity;
                PlayerState firstHit = null;
                PlayerState secondHit = null;
                Vector3 hitNormal = Vector3.zero;
                for (int i = 0; i < activeCount; i++)
                {
                    PlayerState first = active[i];
                    for (int j = i + 1; j < activeCount; j++)
                    {
                        PlayerState second = active[j];
                        if (PlayerCollisionSweep.TryHit(
                            first.Accepted + first.CenterOffset, first.Delta,
                            second.Accepted + second.CenterOffset, second.Delta,
                            first.HalfExtents + second.HalfExtents, out float time, out Vector3 normal)
                            && time < earliest)
                        {
                            earliest = time;
                            firstHit = first;
                            secondHit = second;
                            hitNormal = normal;
                        }
                    }
                }

                if (firstHit == null)
                    return;

                float approach = -Vector3.Dot(firstHit.Delta - secondHit.Delta, hitNormal);
                float stopTime = Mathf.Max(0f, earliest - ContactSkin / approach);
                ClipInwardMotion(firstHit, hitNormal, stopTime);
                ClipInwardMotion(secondHit, -hitNormal, stopTime);
            }

            // Keep the accepted baseline if a set of contacts does not settle within the pass limit.
            for (int i = 0; i < activeCount; i++)
                active[i].Delta = Vector3.zero;
        }

        private static void ClipInwardMotion(PlayerState player, Vector3 normal, float time)
        {
            if (Vector3.Dot(player.Delta, normal) >= 0f)
                return;

            player.Delta *= time;
            player.Normal = normal;
        }

        private bool CanAcceptPositions()
        {
            for (int i = 0; i < activeCount; i++)
            {
                PlayerState first = active[i];
                Vector3 position = first.Accepted + first.Delta;
                if (!IsClearOfStatics(first, position, out Vector3 normal))
                {
                    first.Normal = normal;
                    return false;
                }

                for (int j = i + 1; j < activeCount; j++)
                {
                    PlayerState second = active[j];
                    if (PlayerCollisionSweep.Overlaps(
                        position + first.CenterOffset,
                        second.Accepted + second.Delta + second.CenterOffset,
                        first.HalfExtents + second.HalfExtents))
                        return false;
                }
            }
            return true;
        }

        private bool IsClearOfStatics(PlayerState state, Vector3 position, out Vector3 normal)
        {
            normal = Vector3.zero;
            int count = Physics.OverlapBoxNonAlloc(position + state.CenterOffset, state.HalfExtents,
                staticHits, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = staticHits[i];
                if (other.attachedRigidbody != null
                    || Physics.GetIgnoreLayerCollision(state.Collider.gameObject.layer, other.gameObject.layer))
                    continue;

                if (Physics.ComputePenetration(state.Collider, position, Quaternion.identity,
                    other, other.transform.position, other.transform.rotation, out Vector3 direction,
                    out float depth) && depth > StaticPenetrationTolerance)
                {
                    normal = direction;
                    return false;
                }
            }
            return count < staticHits.Length;
        }
    }
}
