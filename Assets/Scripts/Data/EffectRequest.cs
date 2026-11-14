using UnityEngine;

namespace MushroomSurvival
{
    public readonly struct EffectRequest
    {
        public EffectDefinition Definition { get; }
        public Vector2 Direction { get; }
        public ulong SourcePlayerId { get; }

        public EffectRequest(EffectDefinition definition, Vector2 direction = default,
            ulong sourcePlayerId = EffectApplied.NoSourcePlayer)
        {
            Definition = definition;
            Direction = direction.normalized;
            SourcePlayerId = sourcePlayerId;
        }
    }
}
