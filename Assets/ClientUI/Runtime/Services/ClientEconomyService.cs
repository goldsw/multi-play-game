using System;

namespace MushroomSurvival.ClientUI
{
    public sealed class ClientEconomyService
    {
        private readonly ClientMenuCatalog catalog;
        private readonly ClientProfileStore profileStore;

        public ClientEconomyService(ClientMenuCatalog menuCatalog, ClientProfileStore store)
        {
            catalog = menuCatalog;
            profileStore = store;
        }

        public bool Owns(string cosmeticId)
        {
            return !string.IsNullOrEmpty(cosmeticId) && profileStore.Profile.ownedCosmeticIds.Contains(cosmeticId);
        }

        public bool TryPurchase(string cosmeticId, out string reason)
        {
            var cosmetic = catalog != null ? catalog.FindCosmetic(cosmeticId) : null;
            if (cosmetic == null)
            {
                reason = "상품 정보를 찾을 수 없습니다.";
                return false;
            }
            if (Owns(cosmeticId))
            {
                reason = "이미 보유한 상품입니다.";
                return false;
            }
            if (cosmetic.price < 0 || profileStore.Profile.softCurrency < cosmetic.price)
            {
                reason = "보유 재화가 부족합니다.";
                return false;
            }

            profileStore.Update(profile =>
            {
                profile.softCurrency -= cosmetic.price;
                profile.ownedCosmeticIds.Add(cosmetic.id);
            });
            reason = $"{cosmetic.displayName} 구매 완료";
            return true;
        }

        public bool TryEquip(string cosmeticId, out string reason)
        {
            var cosmetic = catalog != null ? catalog.FindCosmetic(cosmeticId) : null;
            if (cosmetic == null || !Owns(cosmeticId))
            {
                reason = "보유한 상품만 장착할 수 있습니다.";
                return false;
            }

            profileStore.Update(profile =>
            {
                switch (cosmetic.kind)
                {
                    case ClientCosmeticKind.Skin: profile.equippedSkinId = cosmetic.id; break;
                    case ClientCosmeticKind.Voice: profile.equippedVoiceId = cosmetic.id; break;
                    case ClientCosmeticKind.VictoryPose: profile.equippedVictoryPoseId = cosmetic.id; break;
                }
            });
            reason = $"{cosmetic.displayName} 장착 완료";
            return true;
        }

        public int GetRewardForRank(int rank)
        {
            var rewards = catalog != null ? catalog.rankRewards : null;
            if (rewards == null || rank < 1 || rank > rewards.Length) return 0;
            return Math.Max(0, rewards[rank - 1]);
        }

        public bool TryClaimMatchReward(string matchId, int rank, out int amount)
        {
            var rewardAmount = GetRewardForRank(rank);
            amount = rewardAmount;
            var rewards = catalog != null ? catalog.rankRewards : null;
            if (string.IsNullOrWhiteSpace(matchId) || rewards == null || rank < 1 || rank > rewards.Length || OwnsClaim(matchId)) return false;
            profileStore.Update(profile =>
            {
                profile.claimedMatchIds.Add(matchId);
                profile.softCurrency += rewardAmount;
            });
            return true;
        }

        private bool OwnsClaim(string matchId)
        {
            return profileStore.Profile.claimedMatchIds.Contains(matchId);
        }
    }
}
