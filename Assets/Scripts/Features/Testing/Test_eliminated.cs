using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class Test_eliminated : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        public void Configure(InputActionAsset actions) => inputActions = actions;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private InputAction action;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                RegisterInput();
        }

        public override void OnGainedOwnership() => RegisterInput();
        public override void OnLostOwnership() => ReleaseInput();
        public override void OnNetworkDespawn() => ReleaseInput();

        private void RegisterInput()
        {
            if (action != null || inputActions == null)
                return;

            action = inputActions.FindAction("Player/TestEliminate", throwIfNotFound: true).Clone();
            action.performed += OnTestEliminate;
            action.Enable();
        }

        private void ReleaseInput()
        {
            if (action == null)
                return;

            action.performed -= OnTestEliminate;
            action.Dispose();
            action = null;
        }

        private void OnTestEliminate(InputAction.CallbackContext context)
        {
            if (IsSpawned && IsOwner)
                EliminateSelfRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void EliminateSelfRpc()
        {
            var match = FindFirstObjectByType<MatchPlayerStateManager>();
            if (match != null && match.gameObject.scene == gameObject.scene)
                match.Eliminate(NetworkObject);
        }
#endif
    }
}
