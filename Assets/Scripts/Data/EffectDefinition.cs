using UnityEngine;

namespace MushroomSurvival
{
    public enum EffectReapplication
    {
        Refresh,
        Independent
    }

    public abstract class EffectDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float durationSeconds = 1f;
        [SerializeField] private EffectReapplication reapplication;

        public float DurationSeconds => durationSeconds;
        public EffectReapplication Reapplication => reapplication;

        internal virtual bool Accepts(EffectRequest request) => true;
    }
}
