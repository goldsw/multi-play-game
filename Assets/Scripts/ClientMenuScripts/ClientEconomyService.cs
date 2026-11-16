using System;

namespace MushroomSurvival.ClientUI
{
    /// <summary>Pure client-side demo economy. Production balances/rewards must be validated by the server.</summary>
    public sealed class ClientEconomyService
    {
        private readonly ClientMenuCatalog catalog;
        private readonly ClientProfileStore profiles;

        public ClientEconomyService(ClientMenuCatalog menuCatalog, ClientProfileStore profileStore)
        {
            catalog = menuCatalog;
            profiles = profileStore;
        }

        public bool Owns(string id) => !string.IsNullOrEmpty(id) && profiles.Profile.ownedCosmeticIds.Contains(id);

        public bool TryPurchase(string id, out string message)
        {
            var item = catalog != null ? catalog.FindCosmetic(id) : null;
            if (item == null) { message = "상품을 찾을 수 없습니다."; return false; }
            if (Owns(id)) { message = "이미 보유한 상품입니다."; return false; }
            if (item.price < 0 || profiles.Profile.softCurrency < item.price) { message = "보유 재화가 부족합니다."; return false; }
            profiles.Update(profile => { profile.softCurrency -= item.price; profile.ownedCosmeticIds.Add(item.id); });
            message = item.displayName + " 구매 완료";
            return true;
        }

        public bool TryEquip(string id, out string message)
        {
            var item = catalog != null ? catalog.FindCosmetic(id) : null;
            if (item == null || !Owns(id)) { message = "보유한 상품만 장착할 수 있습니다."; return false; }
            var selectedCharacter = profiles.Profile.selectedCharacter;
            if (item.character != ClientCharacterId.Any && item.character != selectedCharacter)
            {
                message = "현재 캐릭터에게 사용할 수 없는 상품입니다.";
                return false;
            }
            profiles.Update(profile =>
            {
                var loadout = profile.characterLoadouts.Find(entry => entry != null && entry.character == profile.selectedCharacter);
                if (loadout == null)
                {
                    loadout = new ClientCharacterLoadout { character = profile.selectedCharacter };
                    profile.characterLoadouts.Add(loadout);
                }
                switch (item.kind)
                {
                    case ClientCosmeticKind.Skin: loadout.equippedSkinId = item.id; break;
                    case ClientCosmeticKind.Voice: loadout.equippedVoiceId = item.id; break;
                    case ClientCosmeticKind.VictoryPose: loadout.equippedVictoryPoseId = item.id; break;
                }
            });
            message = item.displayName + " 장착 완료";
            return true;
        }

        public int RewardForRank(int rank)
        {
            var rewards = catalog != null ? catalog.rankRewards : null;
            return rewards != null && rank > 0 && rank <= rewards.Length ? Math.Max(0, rewards[rank - 1]) : 0;
        }

        public bool TryClaimReward(string matchId, int rank, out int amount)
        {
            amount = RewardForRank(rank);
            if (string.IsNullOrWhiteSpace(matchId) || amount <= 0 || profiles.Profile.claimedMatchIds.Contains(matchId)) return false;
            var rewardAmount = amount;
            profiles.Update(profile => { profile.claimedMatchIds.Add(matchId); profile.softCurrency += rewardAmount; });
            return true;
        }
    }
}
