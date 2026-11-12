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

        private void Update()
        {
            if (IsSpawned && IsOwner && action?.WasReleasedThisFrame() == true)
                caster.RequestCast();
        }

        private void EnableInput()
        {
            if (action != null || inputActions == null)
                return;

            action = inputActions.FindAction("Player/Skill", throwIfNotFound: true).Clone();
            action.Enable();
        }

        private void ReleaseInput()
        {
            action?.Dispose();
            action = null;
        }
    }
}
