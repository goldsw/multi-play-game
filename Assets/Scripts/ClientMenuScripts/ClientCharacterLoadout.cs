using System;

namespace MushroomSurvival.ClientUI
{
    /// <summary>Per-character equipped cosmetic state, independent from the account-wide owned-item inventory.</summary>
    [Serializable]
    public sealed class ClientCharacterLoadout
    {
        public ClientCharacterId character;
        public string equippedSkinId = "skin_default";
        public string equippedVoiceId = "voice_default";
        public string equippedVictoryPoseId = "pose_default";
    }
}
