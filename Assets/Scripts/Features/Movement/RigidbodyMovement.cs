using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerMovementBlocker), typeof(WallContactResponse))]
    [RequireComponent(typeof(MovementEffectSync))]
    public sealed class RigidbodyMovement : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private Rigidbody body;
        private PlayerMovementBlocker blocker;
        private WallContactResponse wallResponse;
        private MovementEffectSync effects;
        private CharacterStateMachine character;
        private InputAction moveAction;

        public Vector2 Facing { get; private set; } = Vector2.up;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            blocker = GetComponent<PlayerMovementBlocker>();
            wallResponse = GetComponent<WallContactResponse>();
            effects = GetComponent<MovementEffectSync>();
            character = GetComponent<CharacterStateMachine>();
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
            wallResponse.ClearContacts();
        }

        public override void OnNetworkDespawn()
        {
            ReleaseInput();
            wallResponse.ClearContacts();
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

            var effectState = effects != null ? effects.State : MovementEffectState.Neutral;
            var velocity = body.linearVelocity;
            if (effectState.Stunned || character?.StopsMovement == true)
            {
                body.linearVelocity = new Vector3(0f, velocity.y, 0f);
                return;
            }

            var desiredVelocity = effectState.ForcedPlanarVelocity;
            if (!effectState.HasForcedMovement)
            {
                var input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
                if (input.sqrMagnitude > 0.01f)
                    Facing = input.normalized;
                desiredVelocity = input * (MovementSettings.Speed * effectState.SpeedMultiplier);
            }

            var planarVelocity = Vector2.MoveTowards(new Vector2(velocity.x, velocity.z), desiredVelocity,
                MovementSettings.Acceleration * Time.fixedDeltaTime);
            var targetVelocity = blocker.LimitHorizontalVelocity(
                new Vector3(planarVelocity.x, velocity.y, planarVelocity.y));
            var movementForce = (targetVelocity - velocity) * (body.mass / Time.fixedDeltaTime);
            body.AddForce(movementForce, ForceMode.Force);
            wallResponse.ApplyReaction(movementForce);
        }
    }
}
