using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlayerMovementBlocker : NetworkBehaviour
    {
        private const float ContactSkin = 0.002f;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private BoxCollider box;
        private Rigidbody body;

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            body = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            foreach (var other in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (other != NetworkObject && other.IsPlayerObject &&
                    other.TryGetComponent(out PlayerMovementBlocker blocker))
                    Physics.IgnoreCollision(box, blocker.box);
            }
        }

        public Vector3 LimitHorizontalVelocity(Vector3 velocity)
        {
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            float distance = planar.magnitude * Time.fixedDeltaTime;
            if (distance <= 0f)
                return velocity;

            Vector3 scale = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 center = body.position + body.rotation * Vector3.Scale(box.center, scale);
            int count = Physics.BoxCastNonAlloc(center, halfExtents, planar.normalized, hits,
                body.rotation, distance + ContactSkin, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearest = distance + ContactSkin;
            Vector3 normal = Vector3.zero;
            for (int index = 0; index < count; index++)
            {
                var hit = hits[index];
                if (hit.collider == box || hit.distance >= nearest ||
                    !hit.collider.TryGetComponent<PlayerMovementBlocker>(out _))
                    continue;

                var horizontalNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                if (Vector3.Dot(planar, horizontalNormal) >= 0f)
                    continue;
                nearest = hit.distance;
                normal = horizontalNormal;
            }

            float fraction = Mathf.Clamp01((nearest - ContactSkin) / distance);
            return velocity - Vector3.Dot(planar, normal) * (1f - fraction) * normal;
        }
    }
}
