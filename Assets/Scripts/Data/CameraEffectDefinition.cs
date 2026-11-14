using UnityEngine;

namespace MushroomSurvival
{
    [CreateAssetMenu(menuName = "Mushroom Survival/Effects/Camera Effect")]
    public sealed class CameraEffectDefinition : EffectDefinition
    {
        [SerializeField] private Sprite overlay;
        [SerializeField] private Color tint = new(0.12f, 0.43f, 0.91f, 1f);
        [SerializeField] private AnimationCurve opacity = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        public Sprite Overlay => overlay;
        public override CharacterEffects CharacterFlags => CharacterEffects.ScreenObscured;
        public Color Tint => tint;
        public float OpacityAt(float progress) => Mathf.Clamp01(opacity.Evaluate(progress));
    }
}
