using UnityEngine;

namespace MushroomSurvival
{
    // Per-character presentation data. CharacterId matches LobbySlotData.CharacterId / RunnerCharacter.
    [CreateAssetMenu(menuName = "Mushroom Survival/Characters/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private int characterId;
        [SerializeField] private GameObject resultModel;
        [SerializeField, Min(0)] private int victoryPoseIndex;

        public int CharacterId => characterId;
        // Local (non-networked) model shown on the Result podium. Falls back to the scene placeholder when empty.
        public GameObject ResultModel => resultModel;
        // Index into VictoryPoseCatalog.VictoryPoses played when this character ranks first.
        public int VictoryPoseIndex => victoryPoseIndex;
    }
}
