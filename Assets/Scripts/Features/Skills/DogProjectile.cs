using UnityEngine;

namespace MushroomSurvival
{
    public sealed class DogProjectile : SkillProjectile
    {
        protected override void ApplyHit(EffectManager target, Vector3 direction) =>
            TryApplyTo(target, new Vector2(-direction.x, -direction.z));
    }
}
