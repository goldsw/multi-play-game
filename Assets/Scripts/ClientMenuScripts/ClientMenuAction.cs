using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    // 메뉴 버튼에서 실행할 동작의 종류입니다.
    public enum ClientMenuActionType
    {
        OpenPage,
        SelectCharacter,
        SelectShopCategory,
        SelectCosmeticSlot,
        PurchaseSelected,
        EquipSelected,
        PreviewSelectedAudio,
        PreviewEquippedVictoryPose,
        PlayEquippedCharacterVoice,
        SelectRewardRank,
        ClaimReward,
        CreateRoomPreview,
        JoinRoomPreview,
        ToggleReadyPreview,
        SubmitMessagePreview,
        LeaveRoomPreview,
        SelectVictoryPose
    }

    /// <summary>기존 씬 Button에 붙이고 Inspector에서 동작과 인덱스를 지정합니다.</summary>
    public sealed class ClientMenuAction : MonoBehaviour
    {
        // 버튼 클릭 시 실행할 동작입니다.
        public ClientMenuActionType action;
        // 페이지, 슬롯, 카테고리 등에 사용하는 0부터 시작하는 인덱스입니다.
        public int index;
    }
}

