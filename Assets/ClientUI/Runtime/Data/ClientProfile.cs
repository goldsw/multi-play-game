using System;
using System.Collections.Generic;

namespace MushroomSurvival.ClientUI
{
    [Serializable]
    public sealed class ClientProfile
    {
        public int schemaVersion = 1;
        public int softCurrency;
        public int selectedCharacter;
        public List<string> ownedCosmeticIds = new List<string>();
        public string equippedSkinId = "skin_default";
        public string equippedVoiceId = "voice_default";
        public string equippedVictoryPoseId = "pose_default";
        public List<string> claimedMatchIds = new List<string>();
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float effectsVolume = 1f;
    }
}
