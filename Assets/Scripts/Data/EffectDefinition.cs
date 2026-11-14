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
        [SerializeField, Min(0)] private int effectId;
        [SerializeField, Min(0.01f)] private float durationSeconds = 1f;
        [SerializeField] private EffectReapplication reapplication;

        public int EffectId => effectId;
        public float DurationSeconds => durationSeconds;
        public EffectReapplication Reapplication => reapplication;
        public virtual CharacterEffects CharacterFlags => CharacterEffects.None;

        internal virtual bool Accepts(EffectRequest request) => true;
    }
}
