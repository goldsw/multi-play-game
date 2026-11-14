using System;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class ItemBox : NetworkBehaviour
    {
        private const float DropHeight = 1.5f;
        private const double DropDuration = 0.45;

        private struct BoxState : INetworkSerializable, IEquatable<BoxState>
        {
            public bool Active;
            public Vector3 Floor;
            public double LandsAt;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Active);
                serializer.SerializeValue(ref Floor);
                serializer.SerializeValue(ref LandsAt);
            }

            public bool Equals(BoxState other)
            {
                return Active == other.Active && Floor.Equals(other.Floor) && LandsAt == other.LandsAt;
            }
        }

        private readonly NetworkVariable<BoxState> state = new();
        private BoxCollider pickupTrigger;
        private Renderer[] visuals;
        private ItemBoxSpawner spawner;
        private bool landed;

        internal bool IsActive => state.Value.Active;
        internal Vector3 Floor => state.Value.Floor;

        private void Awake()
        {
            pickupTrigger = GetComponent<BoxCollider>();
            pickupTrigger.enabled = false;
            visuals = GetComponentsInChildren<Renderer>(true);
            SetVisible(false);
        }

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += OnStateChanged;
            ApplyState();
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            pickupTrigger.enabled = false;
            SetVisible(false);
        }

        internal void Activate(ItemBoxSpawner owner, Vector3 floor)
        {
            if (!IsServer || !IsSpawned || IsActive)
                return;

            spawner = owner;
            state.Value = new BoxState
            {
                Active = true,
                Floor = floor,
                LandsAt = NetworkManager.ServerTime.Time + DropDuration
            };
        }

        internal void Deactivate()
        {
            if (IsServer && IsSpawned && IsActive)
                state.Value = default;
        }

        private void OnStateChanged(BoxState previous, BoxState current) => ApplyState();

        private void ApplyState()
        {
            landed = false;
            SetVisible(IsActive);
            if (IsActive)
                UpdatePose();
            enabled = IsActive;
        }

        private void Update()
        {
            if (!landed)
            {
                UpdatePose();
                if (NetworkManager.ServerTime.Time < state.Value.LandsAt)
                    return;
                landed = true;
                if (!IsServer)
                {
                    enabled = false;
                    return;
                }

                if (spawner != null && !spawner.CanLandAt(Floor))
                {
                    Deactivate();
                    return;
                }
            }
            TryCollect();
        }

        private void UpdatePose()
        {
            BoxState current = state.Value;
            double remaining = current.LandsAt - NetworkManager.ServerTime.Time;
            float height = Mathf.Clamp01((float)(remaining / DropDuration)) * DropHeight;
            transform.position = current.Floor + Vector3.up * height;
        }

        private void TryCollect()
        {
            if (spawner == null || !spawner.IsMatchPlaying)
                return;

            var pickup = new Bounds(transform.TransformPoint(pickupTrigger.center), pickupTrigger.size);
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || player.gameObject.scene != gameObject.scene ||
                    !player.TryGetComponent<Collider>(out var body) || !pickup.Intersects(body.bounds) ||
                    !player.TryGetComponent<PlayerItemInventory>(out var inventory) ||
                    inventory.Held != ItemKind.None || !inventory.TryGrant(RollItem()))
                    continue;

                Deactivate();
                return;
            }
        }

        private static ItemKind RollItem()
        {
            return UnityEngine.Random.Range(0, 3) switch
            {
                0 => ItemKind.Water,
                1 => ItemKind.Apple,
                _ => ItemKind.Banana
            };
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer visual in visuals)
                visual.enabled = visible;
        }
    }
}
