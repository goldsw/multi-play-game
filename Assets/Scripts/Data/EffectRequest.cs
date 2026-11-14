using UnityEngine;

namespace MushroomSurvival
{
    public readonly struct EffectRequest
    {
        public EffectDefinition Definition { get; }
        public Vector2 Direction { get; }
        public ulong SourcePlayerId { get; }
        public float DurationSeconds { get; }
        public bool Notify { get; }

        public EffectRequest(EffectDefinition definition, Vector2 direction = default,
            ulong sourcePlayerId = EffectApplied.NoSourcePlayer, float? durationSeconds = null, bool notify = true)
        {
            Definition = definition;
            Direction = direction.normalized;
            SourcePlayerId = sourcePlayerId;
            DurationSeconds = durationSeconds ?? definition?.DurationSeconds ?? 0f;
            Notify = notify;
        }
    }
}
