using UnityEngine;

namespace MushroomSurvival
{
    // Art drops clips in here; nothing else needs to change. Empty or out-of-range entries are tolerated.
    [CreateAssetMenu(menuName = "Mushroom Survival/Result/Victory Pose Catalog")]
    public sealed class VictoryPoseCatalog : ScriptableObject
    {
        [SerializeField] private AnimationClip[] victoryPoses;
        [SerializeField] private AnimationClip deathMotion;

        public int VictoryPoseCount => victoryPoses?.Length ?? 0;
        // Shared by every non-winning rank (2nd to 4th).
        public AnimationClip DeathMotion => deathMotion;

        public bool TryGetVictoryPose(int index, out AnimationClip clip)
        {
            clip = victoryPoses != null && index >= 0 && index < victoryPoses.Length ? victoryPoses[index] : null;
            return clip != null;
        }
    }
}
