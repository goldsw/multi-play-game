using UnityEngine;

namespace MushroomSurvival
{
    // [임시/테스트용] 결과 씬 테스트용 임시 캐릭터 정의입니다.
    // 팀원이 만드는 정식 캐릭터 정의가 들어오면 이 클래스는 교체·삭제 대상입니다.
    // Per-character presentation data. CharacterId matches LobbySlotData.CharacterId / RunnerCharacter.
    [CreateAssetMenu(menuName = "Mushroom Survival/Characters/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private int characterId;
        // 테스트용: 정식 정의의 모델 필드로 교체 예정
        [SerializeField] private GameObject resultModel;
        // 테스트용: 정식 정의의 승리 포즈 인덱스로 교체 예정
        [SerializeField, Min(0)] private int victoryPoseIndex;

        public int CharacterId => characterId;
        // Local (non-networked) model shown on the Result podium. Falls back to the scene placeholder when empty.
        public GameObject ResultModel => resultModel;
        // Index into VictoryPoseCatalog.VictoryPoses played when this character ranks first.
        public int VictoryPoseIndex => victoryPoseIndex;
    }
}
