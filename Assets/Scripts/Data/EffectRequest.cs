using UnityEngine;

namespace MushroomSurvival
{
    public readonly struct EffectRequest
    {
        public EffectDefinition Definition { get; }
        public Vector2 Direction { get; }

        public EffectRequest(EffectDefinition definition, Vector2 direction = default)
        {
            Definition = definition;
            Direction = direction.normalized;
        }
    }
}
