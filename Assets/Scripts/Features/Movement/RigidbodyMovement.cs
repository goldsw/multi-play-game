using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerMovementBlocker))]
    public sealed class RigidbodyMovement : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private Rigidbody body;
        private PlayerMovementBlocker blocker;
        private MovementEffectSync effects;
        private InputAction moveAction;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            blocker = GetComponent<PlayerMovementBlocker>();
            effects = GetComponent<MovementEffectSync>();
        }

        public void Configure(InputActionAsset actions)
        {
            inputActions = actions;

            if (IsSpawned && IsOwner)
            {
                ReleaseInput();
                EnableInput();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                EnableInput();
            }
        }

        public override void OnGainedOwnership()
        {
            EnableInput();
        }

        public override void OnLostOwnership()
        {
            ReleaseInput();
        }

        public override void OnNetworkDespawn()
        {
            ReleaseInput();
        }

        private void EnableInput()
        {
            if (moveAction != null || inputActions == null)
            {
                return;
            }

            moveAction = inputActions.FindAction(MovementSettings.MoveActionPath, throwIfNotFound: true).Clone();
            moveAction.Enable();
        }

        private void ReleaseInput()
        {
            moveAction?.Dispose();
            moveAction = null;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || body.isKinematic || moveAction == null)
            {
                return;
            }

            var state = effects != null ? effects.State : MovementEffectState.Neutral;
            var velocity = body.linearVelocity;
            var planarVelocity = Vector2.zero;
            if (!state.Stunned)
            {
                var target = state.ForcedPlanarVelocity;
                if (!state.HasForcedMovement)
                {
                    var input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
                    target = input * (MovementSettings.Speed * state.SpeedMultiplier);
                }

                planarVelocity = Vector2.MoveTowards(new Vector2(velocity.x, velocity.z), target,
                    MovementSettings.Acceleration * Time.fixedDeltaTime);
            }

            body.linearVelocity = blocker.LimitHorizontalVelocity(
                new Vector3(planarVelocity.x, velocity.y, planarVelocity.y));
        }
    }
}
