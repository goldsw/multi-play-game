using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class LocalPlayerCameraFollow : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float followDistance = 18.868f;
        private Transform spectatorTarget;

        public void SetSpectatorTarget(Transform target) => spectatorTarget = target;

        private void LateUpdate()
        {
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsClient)
                return;

            var player = network.LocalClient?.PlayerObject;
            if (player == null || !player.IsSpawned || !player.IsOwner || player.gameObject.scene != gameObject.scene)
                return;

            var target = spectatorTarget != null ? spectatorTarget : player.transform;
            transform.position = target.position - transform.forward * followDistance;
        }
    }
}
