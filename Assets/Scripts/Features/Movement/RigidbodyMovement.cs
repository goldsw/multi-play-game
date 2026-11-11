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
        private InputAction moveAction;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            blocker = GetComponent<PlayerMovementBlocker>();
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

            var input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            var velocity = body.linearVelocity;
            var planarVelocity = Vector2.MoveTowards(
                new Vector2(velocity.x, velocity.z),
                input * MovementSettings.Speed,
                MovementSettings.Acceleration * Time.fixedDeltaTime);

            body.linearVelocity = blocker.LimitHorizontalVelocity(
                new Vector3(planarVelocity.x, velocity.y, planarVelocity.y));
        }
    }
}
