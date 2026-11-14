using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody), typeof(NetworkTransform))]
    public sealed class ItemBox : NetworkBehaviour
    {
        private const float DropHeight = 1.5f;
        private const double DropDuration = 0.45;
        private const float PushSpeed = 5f;
        private const float RecycleDepth = 2.5f;

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
        private readonly HashSet<ulong> touchingPlayers = new();
        private BoxCollider boxCollider;
        private Rigidbody boxBody;
        private NetworkTransform sync;
        private Renderer[] visuals;
        private ItemBoxSpawner spawner;
        private bool landed;

        internal bool IsActive => state.Value.Active;
        internal Vector3 Floor => state.Value.Floor;
        internal Vector3 OccupiedPosition => landed ? boxBody.position : Floor;

        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
            boxBody = GetComponent<Rigidbody>();
            sync = GetComponent<NetworkTransform>();
            boxCollider.enabled = false;
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
            ResetPhysicalState();
            SetVisible(false);
        }

        internal void Activate(ItemBoxSpawner owner, Vector3 floor)
        {
            if (!IsServer || !IsSpawned || IsActive)
                return;

            spawner = owner;
            var origin = floor + Vector3.up * DropHeight;
            boxBody.position = origin;
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
            if (IsServer && IsSpawned && IsActive)
                state.Value = default;
        }

        private void OnStateChanged(BoxState previous, BoxState current) => ApplyState();

        private void ApplyState()
        {
            landed = false;
            ResetPhysicalState();
            SetVisible(IsActive);
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || !IsActive)
                return;

            if (!landed)
            {
                var remaining = state.Value.LandsAt - NetworkManager.ServerTime.Time;
                if (remaining > 0d)
                {
                    float height = Mathf.Clamp01((float)(remaining / DropDuration)) * DropHeight;
                    boxBody.MovePosition(Floor + Vector3.up * height);
                    return;
                }

                if (spawner == null || !spawner.CanLandAt(Floor))
                {
                    Deactivate();
                    return;
                }

                Land();
                return;
            }

            if (boxBody.position.y < Floor.y - RecycleDepth)
            {
                Deactivate();
                return;
            }
            if (spawner.IsMatchPlaying)
                ResolvePlayerContacts();
        }

        private void Land()
        {
            landed = true;
            boxBody.position = Floor;
            boxBody.isKinematic = false;
            boxBody.useGravity = true;
            boxCollider.enabled = true;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<Collider>(out var playerCollider))
                    Physics.IgnoreCollision(boxCollider, playerCollider);
            }
        }

        private void ResolvePlayerContacts()
        {
            var pickup = boxCollider.bounds;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || player.gameObject.scene != gameObject.scene ||
                    !player.TryGetComponent<Collider>(out var body) || !pickup.Intersects(body.bounds) ||
                    !player.TryGetComponent<CharacterStateMachine>(out var state) || state.IsEliminated ||
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

                if (touchingPlayers.Add(client.ClientId))
                    PushAwayFrom(player, body);
            }
        }

        private void PushAwayFrom(NetworkObject player, Collider playerCollider)
        {
            var direction = boxBody.position - playerCollider.bounds.center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f && player.TryGetComponent<RigidbodyMovement>(out var movement))
            {
                var facing = movement.Facing;
                direction = new Vector3(facing.x, 0f, facing.y);
            }
            boxBody.AddForce(direction.normalized * PushSpeed, ForceMode.VelocityChange);
        }

        private void ResetPhysicalState()
        {
            if (!boxBody.isKinematic)
            {
                boxBody.linearVelocity = Vector3.zero;
                boxBody.angularVelocity = Vector3.zero;
            }
            boxBody.isKinematic = true;
            boxBody.useGravity = false;
            boxCollider.enabled = false;
            touchingPlayers.Clear();
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
