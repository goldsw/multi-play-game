using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class SkillInput : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private SkillCaster caster;
        private InputAction action;

        public bool IsHeld => action?.IsPressed() == true;

        public void Configure(InputActionAsset actions) => inputActions = actions;

        private void Awake() => caster = GetComponent<SkillCaster>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                EnableInput();
        }

        public override void OnGainedOwnership() => EnableInput();
        public override void OnLostOwnership() => ReleaseInput();
        public override void OnNetworkDespawn() => ReleaseInput();

        private void OnSkillPressed(InputAction.CallbackContext context)
        {
            if (IsSpawned && IsOwner)
                caster.RequestCast();
        }

        private void EnableInput()
        {
            if (action != null || inputActions == null)
                return;

            action = inputActions.FindAction("Player/Skill", throwIfNotFound: true).Clone();
            action.performed += OnSkillPressed;
            action.Enable();
        }

        private void ReleaseInput()
        {
            if (action == null)
                return;

            action.performed -= OnSkillPressed;
            action.Dispose();
            action = null;
        }
    }
}
