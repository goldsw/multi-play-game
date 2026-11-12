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

        public string SkillId => skillId;
        public SkillRangeEnum RangeDisplay => skillRangeEnum;
        public float SkillRange => skillRange;
        public float TargetCircleRadius => targetCircleRadius;
    }
}
