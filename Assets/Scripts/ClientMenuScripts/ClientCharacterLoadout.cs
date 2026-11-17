using System;

namespace MushroomSurvival.ClientUI
{
    /// <summary>계정 전체 보유 목록과 별개로 캐릭터별 장착 상태를 저장합니다.</summary>
    [Serializable]
    public sealed class ClientCharacterLoadout
    {
        // 이 장착 정보가 속한 캐릭터입니다.
        public ClientCharacterId character;
        // 장착한 스킨, 음성, 승리 포즈의 카탈로그 ID입니다.
        public string equippedSkinId = "skin_default";
        public string equippedVoiceId = "voice_default";
        public string equippedVictoryPoseId = "pose_default";
    }
}
