using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ItemBoxPhysics : MonoBehaviour
    {
        private const float PushSpeed = 5f;
        private Rigidbody body;
        private BoxCollider boxCollider;

        public Vector3 Position => body.position;
        public Bounds Bounds => boxCollider.bounds;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            boxCollider = GetComponent<BoxCollider>();
            SetSimulation(false);
            boxCollider.enabled = false;
        }

        public void SetLanded(bool landed, NetworkManager network)
        {
            if (boxCollider.enabled == landed)
                return;

            boxCollider.enabled = landed;
            if (!landed)
                return;

            foreach (var player in network.SpawnManager.SpawnedObjectsList)
            {
                if (player != null && player.IsPlayerObject && player.TryGetComponent<Collider>(out var collider))
                    Physics.IgnoreCollision(boxCollider, collider);
            }
        }

        public void SetSimulation(bool simulating)
        {
            if (body.isKinematic == !simulating)
                return;

            if (!simulating)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = !simulating;
            body.useGravity = simulating;
        }

        public void SetPosition(Vector3 position) => body.position = position;

        public void MovePosition(Vector3 position) => body.MovePosition(position);

        public void Push(Vector3 direction) =>
            body.AddForce(direction * PushSpeed, ForceMode.VelocityChange);
    }
}
