using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [RequireComponent(typeof(PlayerItemInventory))]
    [DisallowMultipleComponent]
    public sealed class ItemInput : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputAction action;
        private PlayerItemInventory inventory;

        public void Configure(InputActionAsset actions) => inputActions = actions;

        private void Awake() => inventory = GetComponent<PlayerItemInventory>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                EnableInput();
        }

        public override void OnGainedOwnership() => EnableInput();
        public override void OnLostOwnership() => ReleaseInput();
        public override void OnNetworkDespawn() => ReleaseInput();

        private void EnableInput()
        {
            if (action != null || inputActions == null)
                return;

            action = inputActions.FindAction("Player/Interact", throwIfNotFound: true).Clone();
            action.performed += OnUse;
            action.Enable();
        }

        private void ReleaseInput()
        {
            if (action == null)
                return;

            action.performed -= OnUse;
            action.Dispose();
            action = null;
        }

        private void OnUse(InputAction.CallbackContext context) => inventory.RequestUse();
    }
}
