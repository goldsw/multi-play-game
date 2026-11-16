using UnityEngine;

namespace MushroomSurvival.ClientUI
{
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
        SubmitMessagePreview
    }

    /// <summary>Place this on an existing scene Button; configure action/index in its Inspector.</summary>
    public sealed class ClientMenuAction : MonoBehaviour
    {
        public ClientMenuActionType action;
        public int index;
    }
}
