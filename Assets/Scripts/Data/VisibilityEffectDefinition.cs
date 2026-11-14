using UnityEngine;

namespace MushroomSurvival
{
    [CreateAssetMenu(menuName = "Mushroom Survival/Effects/Visibility Effect")]
    public sealed class VisibilityEffectDefinition : EffectDefinition
    {
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.5f;
        [SerializeField, Range(0.05f, 1f)] private float ownerOpacity = 0.35f;

        public float FadeSeconds => fadeSeconds;
        public float OwnerOpacity => ownerOpacity;
        public override CharacterEffects CharacterFlags => CharacterEffects.Invisible;
    }
}
