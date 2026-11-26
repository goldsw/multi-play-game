using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(NetworkObject), typeof(OwnerNetworkTransform), typeof(ItemBoxPhysics))]
    public sealed class ItemBox : NetworkBehaviour
    {
        private const float DropHeight = 1.5f;
        private const double DropDuration = 0.45;
        private const float RecycleDepth = 2.5f;

        private struct BoxState : INetworkSerializable, IEquatable<BoxState>
        {
            public bool Active;
            public bool Landed;
            public Vector3 Floor;
            public double LandsAt;
            public ulong PushOwner;
            public uint PushVersion;
            public Vector3 PushOrigin;
            public Vector3 PushDirection;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Active);
                serializer.SerializeValue(ref Landed);
                serializer.SerializeValue(ref Floor);
                serializer.SerializeValue(ref LandsAt);
                serializer.SerializeValue(ref PushOwner);
                serializer.SerializeValue(ref PushVersion);
                serializer.SerializeValue(ref PushOrigin);
                serializer.SerializeValue(ref PushDirection);
            }

            public bool Equals(BoxState other) => Active == other.Active && Landed == other.Landed &&
                Floor.Equals(other.Floor) && LandsAt == other.LandsAt && PushOwner == other.PushOwner &&
                PushVersion == other.PushVersion && PushOrigin.Equals(other.PushOrigin) &&
                PushDirection.Equals(other.PushDirection);
        }

        private readonly NetworkVariable<BoxState> state = new();
        private readonly HashSet<ulong> touchingPlayers = new();
        private ItemBoxPhysics physics;
        private OwnerNetworkTransform sync;
        private Renderer[] visuals;
        private ItemBoxSpawner spawner;
        private uint pushVersion;
        private uint appliedPush;

        internal bool IsActive => state.Value.Active;
        internal Vector3 Floor => state.Value.Floor;
        internal Vector3 OccupiedPosition => state.Value.Landed ? physics.Position : Floor;

        private void Awake()
        {
            physics = GetComponent<ItemBoxPhysics>();
            sync = GetComponent<OwnerNetworkTransform>();
            visuals = GetComponentsInChildren<Renderer>(true);
            SetVisible(false);
        }

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += OnStateChanged;
            ApplyState();
        }

        public override void OnLostOwnership() => physics.SetSimulation(false);

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            physics.SetSimulation(false);
            physics.SetLanded(false, NetworkManager);
            SetVisible(false);
            touchingPlayers.Clear();
        }

        internal void Activate(ItemBoxSpawner owner, Vector3 floor)
        {
            if (!IsServer || !IsSpawned || IsActive)
                return;

            spawner = owner;
            var origin = floor + Vector3.up * DropHeight;
            physics.SetPosition(origin);
            sync.Teleport(origin, Quaternion.identity, transform.localScale);
            state.Value = new BoxState
            {
                Active = true,
                Floor = floor,
                LandsAt = NetworkManager.ServerTime.Time + DropDuration
            };
        }

        internal void Deactivate()
        {
            if (!IsServer || !IsSpawned || !IsActive)
                return;

            state.Value = default;
            touchingPlayers.Clear();
            if (!IsOwnedByServer)
                NetworkObject.RemoveOwnership();
        }

        private void OnStateChanged(BoxState previous, BoxState current) => ApplyState();

        private void ApplyState()
        {
            BoxState current = state.Value;
            physics.SetLanded(current.Landed, NetworkManager);
            if (!current.Landed)
                physics.SetSimulation(false);
            SetVisible(current.Active);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned)
                return;

            BoxState current = state.Value;
            physics.SetSimulation(current.Landed && IsOwner);
            if (!current.Active)
                return;

            if (!current.Landed)
            {
                if (IsServer)
                    UpdateDrop(current);
                return;
            }

            // Occupancy and ownership travel independently. Apply the impulse only when both agree.
            if (IsOwner && current.PushOwner == OwnerClientId && current.PushVersion != 0 &&
                current.PushVersion != appliedPush)
            {
                appliedPush = current.PushVersion;
                if (!IsServer)
                    physics.SetPosition(current.PushOrigin);
                physics.Push(current.PushDirection);
            }

            if (!IsServer)
                return;

            if (physics.Position.y < Floor.y - RecycleDepth)
            {
                Deactivate();
                return;
            }
            if (spawner.IsMatchPlaying)
                ResolvePlayerContacts();
        }

        private void UpdateDrop(BoxState current)
        {
            double remaining = current.LandsAt - NetworkManager.ServerTime.Time;
            if (remaining > 0d)
            {
                float height = Mathf.Clamp01((float)(remaining / DropDuration)) * DropHeight;
                physics.MovePosition(current.Floor + Vector3.up * height);
                return;
            }
            if (spawner == null || !spawner.CanLandAt(current.Floor))
            {
                Deactivate();
                return;
            }

            physics.SetPosition(current.Floor);
            current.Landed = true;
            state.Value = current;
        }

        private void ResolvePlayerContacts()
        {
            var pickup = physics.Bounds;
            NetworkObject pusher = null;
            Collider pusherCollider = null;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || player.gameObject.scene != gameObject.scene ||
                    !player.TryGetComponent<Collider>(out var body) || !pickup.Intersects(body.bounds) ||
                    !player.TryGetComponent<CharacterStateMachine>(out var character) || character.IsEliminated ||
                    !player.TryGetComponent<PlayerItemInventory>(out var inventory))
                {
                    touchingPlayers.Remove(client.ClientId);
                    continue;
                }

                if (inventory.Held == ItemKind.None)
                {
                    if (!inventory.TryGrant(RollItem()))
                        continue;
                    Deactivate();
                    return;
                }

                if (pusher == null && touchingPlayers.Add(client.ClientId))
                {
                    pusher = player;
                    pusherCollider = body;
                }
            }
            if (pusher != null)
                TransferPush(pusher, pusherCollider);
        }

        private void TransferPush(NetworkObject player, Collider playerCollider)
        {
            var direction = physics.Position - playerCollider.bounds.center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f && player.TryGetComponent<RigidbodyMovement>(out var movement))
            {
                var facing = movement.Facing;
                direction = new Vector3(facing.x, 0f, facing.y);
            }

            var owner = player.OwnerClientId;
            var current = state.Value;
            current.PushOwner = owner;
            current.PushVersion = ++pushVersion;
            current.PushOrigin = physics.Position;
            current.PushDirection = direction.normalized;
            if (OwnerClientId != owner)
                NetworkObject.ChangeOwnership(owner);
            state.Value = current;
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
