using UnityEngine;

namespace MushroomSurvival
{
    public abstract class SkillProjectile : EffectObject
    {
        private const float Radius = 0.18f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private double updatedAt;
        private float remainingDistance;

        internal override bool TryGetSpawnPose(SkillDefinition skill, Collider caster, Collider floor,
            Vector3 direction, out Vector3 position, out Quaternion rotation)
        {
            position = caster.bounds.center;
            rotation = Quaternion.LookRotation(direction);
            return float.IsFinite(skill.ProjectileSpeed) && skill.ProjectileSpeed > 0f;
        }

        protected override void OnEffectStart(double now)
        {
            updatedAt = now;
            remainingDistance = Definition.SkillRange;
        }

        protected override void OnEffectUpdate(double now)
        {
            float distance = Mathf.Min(remainingDistance, Definition.ProjectileSpeed * (float)(now - updatedAt));
            updatedAt = now;
            if (distance <= 0f)
                return;

            var direction = transform.forward;
            int count = gameObject.scene.GetPhysicsScene().SphereCast(transform.position, Radius, direction, hits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            EffectManager target = null;
            for (int index = 0; index < count; index++)
            {
                var hit = hits[index];
                var candidate = hit.collider.GetComponentInParent<EffectManager>();
                if (candidate != null && (!IsEligibleTarget(candidate) || candidate.NetworkObjectId == SourcePlayerId))
                    continue;

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    target = candidate;
                }
            }

            if (!float.IsPositiveInfinity(nearest))
            {
                ApplyHit(target, direction);
                NetworkObject.Despawn();
                return;
            }

            transform.position += direction * distance;
            remainingDistance -= distance;
            if (remainingDistance <= 0f)
                NetworkObject.Despawn();
        }

        protected virtual void ApplyHit(EffectManager target, Vector3 direction) => TryApplyTo(target);
    }
}
