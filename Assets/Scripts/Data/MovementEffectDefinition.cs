using UnityEngine;

namespace MushroomSurvival
{
    public enum MovementEffectKind
    {
        Stun,
        SpeedMultiplier,
        ForcedMovement
    }

    [CreateAssetMenu(menuName = "Mushroom Survival/Effects/Movement Effect")]
    public sealed class MovementEffectDefinition : EffectDefinition
    {
        [SerializeField] private MovementEffectKind kind;
        [SerializeField, Min(0.01f)] private float amount = 1f;

        public MovementEffectKind Kind => kind;
        public float Amount => amount;
        public override CharacterEffects CharacterFlags => kind switch
        {
            MovementEffectKind.Stun => CharacterEffects.Stunned,
            MovementEffectKind.ForcedMovement => CharacterEffects.ForcedMovement,
            MovementEffectKind.SpeedMultiplier when amount < 1f => CharacterEffects.Slowed,
            MovementEffectKind.SpeedMultiplier when amount > 1f => CharacterEffects.Boosted,
            _ => CharacterEffects.None
        };

        internal override bool Accepts(EffectRequest request) => kind switch
        {
            MovementEffectKind.Stun => true,
            MovementEffectKind.SpeedMultiplier => float.IsFinite(amount) && amount > 0f,
            MovementEffectKind.ForcedMovement => float.IsFinite(amount) && amount > 0f &&
                float.IsFinite(request.Direction.sqrMagnitude) && request.Direction.sqrMagnitude > 0f,
            _ => false
        };
    }
}
