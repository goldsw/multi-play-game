using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class LocalPlayerCameraFollow : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float followDistance = 18.868f;
        [SerializeField, Min(0f), Tooltip("Seconds the camera takes to catch up with a moving target.")]
        private float smoothTime = 0.08f;
        [SerializeField, Min(0f), Tooltip("Slower catch-up used right after the target changes.")]
        private float switchSmoothTime = 0.35f;
        [SerializeField, Min(0f), Tooltip("Seconds the camera takes to reach a new follow distance.")]
        private float zoomSmoothTime = 0.4f;

        private Transform spectatorTarget;
        private Transform overrideTarget;
        private Transform lastTarget;
        private float distanceOverride = -1f;
        private float currentDistance;
        private float distanceVelocity;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private bool initialized;
        private bool switching;
        private Camera followCamera;
        private float baseOrthographicSize;

        /// <summary>Default distance, or the active zoom override.</summary>
        public float FollowDistance => distanceOverride > 0f ? distanceOverride : followDistance;

        /// <summary>Target the camera followed on the last frame (override, spectated or own player).</summary>
        public Transform ActiveTarget => lastTarget;

        /// <summary>Spectated player. Ignored while an override target is set.</summary>
        public void SetSpectatorTarget(Transform target) => spectatorTarget = target;

        /// <summary>Highest-priority target (e.g. a winner shot). Pass null to release.</summary>
        public void SetOverrideTarget(Transform target) => overrideTarget = target;

        /// <summary>Smoothly zooms to the given follow distance. Release with ClearDistanceOverride.</summary>
        public void SetDistanceOverride(float distance) => distanceOverride = Mathf.Max(0.1f, distance);

        public void ClearDistanceOverride() => distanceOverride = -1f;

        private void Awake()
        {
            followCamera = GetComponent<Camera>();
            baseOrthographicSize = followCamera.orthographicSize;
        }

        private void LateUpdate()
        {
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsClient)
                return;

            var player = network.LocalClient?.PlayerObject;
            if (player == null || !player.IsSpawned || !player.IsOwner || player.gameObject.scene != gameObject.scene)
            {
                initialized = false;
                return;
            }

            var target = overrideTarget != null ? overrideTarget
                : spectatorTarget != null ? spectatorTarget : player.transform;
            float distance = FollowDistance;
            if (!initialized)
            {
                initialized = true;
                focus = target.position;
                focusVelocity = Vector3.zero;
                currentDistance = distance;
                distanceVelocity = 0f;
            }
            else
            {
                if (target != lastTarget)
                    switching = true;
                float time = switching ? switchSmoothTime : smoothTime;
                focus = Vector3.SmoothDamp(focus, target.position, ref focusVelocity, time);
                if (switching && (target.position - focus).sqrMagnitude < 0.25f)
                    switching = false;
                currentDistance = Mathf.SmoothDamp(currentDistance, distance, ref distanceVelocity, zoomSmoothTime);
            }
            lastTarget = target;
            transform.position = focus - transform.forward * currentDistance;
            // An orthographic camera ignores distance, so the zoom scales its size by the same ratio.
            if (followCamera.orthographic)
                followCamera.orthographicSize = baseOrthographicSize * currentDistance / followDistance;
        }
    }
}
