using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public class DuckEgg : EffectObject
    {
        private const float ContactHalfHeight = 0.15f;
        [SerializeField] private Transform shell;

        private readonly NetworkVariable<bool> armed = new();
        private readonly Collider[] overlaps = new Collider[32];
        private Vector3 shellScale;
        private double armsAt;
        private double expiresAt;

        public bool IsArmed => armed.Value;

        public void Configure(Transform visual) => shell = visual;

        protected void Awake() => shellScale = shell.localScale;

        internal override bool TryGetSpawnPose(SkillDefinition skill, Collider caster, Collider floor,
            Vector3 direction, out Vector3 position, out Quaternion rotation)
        {
            var bounds = caster.bounds;
            var ray = new Ray(bounds.center, Vector3.down);
            position = default;
            rotation = Quaternion.identity;
            if (floor == null || (skill.ObjectLifetime > 0f && skill.ObjectLifetime <= skill.ArmDelay) ||
                !floor.Raycast(ray, out var hit, bounds.extents.y + 0.2f))
                return false;

            position = hit.point;
            return true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            armed.OnValueChanged += OnArmedChanged;
            OnArmedChanged(false, armed.Value);
        }

        public override void OnNetworkDespawn()
        {
            armed.OnValueChanged -= OnArmedChanged;
            base.OnNetworkDespawn();
        }

        protected override void OnEffectStart(double now)
        {
            armsAt = now + Definition.ArmDelay;
            expiresAt = Definition.ObjectLifetime > 0f ? now + Definition.ObjectLifetime : double.PositiveInfinity;
        }

        protected override void OnEffectUpdate(double now)
        {
            if (now >= expiresAt)
            {
                NetworkObject.Despawn();
                return;
            }

            if (now < armsAt)
                return;
            if (!armed.Value)
                armed.Value = true;

            var target = FindTarget();
            if (TryApplyTo(target))
                NetworkObject.Despawn();
        }

        private EffectManager FindTarget()
        {
            var origin = transform.position;
            float radius = Definition.SkillRange;
            int count = Physics.OverlapBoxNonAlloc(origin + Vector3.up * ContactHalfHeight,
                new Vector3(radius, ContactHalfHeight, radius), overlaps,
                Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            EffectManager nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                var collider = overlaps[index];
                var candidate = collider.GetComponentInParent<EffectManager>();
                if (!IsEligibleTarget(candidate))
                    continue;

                var offset = collider.ClosestPoint(origin) - origin;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius)
                    continue;

                var center = candidate.transform.position - origin;
                center.y = 0f;
                float distance = center.sqrMagnitude;
                if (distance < nearestDistance ||
                    (distance == nearestDistance && candidate.NetworkObjectId < nearest.NetworkObjectId))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private void OnArmedChanged(bool previous, bool current) =>
            shell.localScale = current ? shellScale : shellScale * 0.7f;
    }
}
