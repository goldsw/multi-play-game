using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(CharacterStateMachine), typeof(RigidbodyMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerItemInventory : NetworkBehaviour
    {
        private readonly NetworkVariable<ItemKind> held = new(
            ItemKind.None, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<bool> inUse = new(
            false, NetworkVariableReadPermission.Owner);
        private ItemWorld world;
        private LobbyNetworkState lobby;
        private CharacterStateMachine character;
        private RigidbodyMovement movement;

        public ItemKind Held => held.Value;
        public bool InUse => inUse.Value;

        private void Awake()
        {
            character = GetComponent<CharacterStateMachine>();
            movement = GetComponent<RigidbodyMovement>();
        }

        internal void Initialize(ItemWorld itemWorld, LobbyNetworkState state)
        {
            world = itemWorld;
            lobby = state;
        }

        internal bool TryGrant(ItemKind kind)
        {
            if (!IsServer || !IsSpawned || lobby == null || lobby.Status != SessionStatus.Playing ||
                character.IsEliminated || Held != ItemKind.None || kind == ItemKind.None)
                return false;

            held.Value = kind;
            return true;
        }

        public void RequestUse()
        {
            if (IsSpawned && IsOwner && Held != ItemKind.None && character.AllowsItemUse)
                UseRpc(Held, movement.Facing);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseRpc(ItemKind requested, Vector2 facing)
        {
            if (requested == ItemKind.None || Held != requested || !character.AllowsItemUse ||
                lobby == null || lobby.Status != SessionStatus.Playing || world == null ||
                !float.IsFinite(facing.sqrMagnitude) || facing.sqrMagnitude < 0.01f)
                return;

            if (world.TryUse(NetworkObject, requested, facing.normalized))
            {
                held.Value = ItemKind.None;
                inUse.Value = world.IsInUse(NetworkObject, requested);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || world == null)
                return;

            if (character.IsEliminated && Held != ItemKind.None)
                held.Value = ItemKind.None;

            bool usingItem = !character.IsEliminated &&
                (world.IsInUse(NetworkObject, ItemKind.Water) || world.IsInUse(NetworkObject, ItemKind.Apple) ||
                    world.IsInUse(NetworkObject, ItemKind.Banana));
            if (inUse.Value != usingItem)
                inUse.Value = usingItem;
        }
    }
}
