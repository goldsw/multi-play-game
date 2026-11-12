using UnityEngine;

namespace MushroomSurvival
{
    public enum SkillRangeEnum
    {
        None,
        TargetCircle,
        DirectionLine,
        SelfCircle
    }

    [CreateAssetMenu(menuName = "Mushroom Survival/Skills/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string skillId;
        [SerializeField] private SkillRangeEnum skillRangeEnum;
        [SerializeField, Min(0f)] private float skillRange;
        [SerializeField, Min(0f)] private float targetCircleRadius = 0.75f;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(1)] private int charges = 1;
        [SerializeField] private EffectDefinition effect;
        [SerializeField] private EffectObject spawnPrefab;
        [SerializeField, Min(0)] private int objectLimit;
        [SerializeField, Min(0f)] private float armDelay;
        [SerializeField, Min(0f)] private float objectLifetime;

        public string SkillId => skillId;
        public SkillRangeEnum RangeDisplay => skillRangeEnum;
        public float SkillRange => skillRange;
        public float TargetCircleRadius => targetCircleRadius;
        public float CooldownSeconds => cooldownSeconds;
        public int Charges => charges;
        public EffectDefinition Effect => effect;
        public EffectObject SpawnPrefab => spawnPrefab;
        public int ObjectLimit => objectLimit;
        public float ArmDelay => armDelay;
        public float ObjectLifetime => objectLifetime;
    }
}
