using System;

namespace MushroomSurvival.ClientUI
{
    /// <summary>로컬 클라이언트 미리보기용 경제 처리입니다. 실제 재화와 보상은 서버에서 검증해야 합니다.</summary>
    public sealed class ClientEconomyService
    {
        private readonly ClientMenuCatalog catalog;
        private readonly ClientProfileStore profiles;

        // 카탈로그와 로컬 프로필 저장소를 받아 경제 기능을 준비합니다.
        public ClientEconomyService(ClientMenuCatalog menuCatalog, ClientProfileStore profileStore)
        {
            catalog = menuCatalog;
            profiles = profileStore;
        }

        // 지정한 코스메틱 ID를 로컬 프로필에서 보유하는지 확인합니다.
        public bool Owns(string id) => !string.IsNullOrEmpty(id) && profiles.Profile.ownedCosmeticIds.Contains(id);

        // 상품과 재화를 확인한 뒤 구매 결과를 로컬 프로필에 저장합니다.
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

        // 보유 여부와 캐릭터 호환성을 검사하고 선택 캐릭터에 상품을 장착합니다.
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

        // 카탈로그에서 등수에 해당하는 임시 보상량을 가져옵니다.
        public int RewardForRank(int rank)
        {
            var rewards = catalog != null ? catalog.rankRewards : null;
            return rewards != null && rank > 0 && rank <= rewards.Length ? Math.Max(0, rewards[rank - 1]) : 0;
        }

        // 같은 경기 보상의 중복 수령을 막고 보상을 로컬 프로필에 추가합니다.
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
