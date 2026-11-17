using System;
using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    // PlayerPrefs에 저장되는 로컬 사용자 데이터입니다.
    [Serializable]
    public sealed class ClientProfile
    {
        // 저장 데이터 구조의 버전입니다.
        public int schemaVersion = 1;
        // 현재 보유한 로컬 재화입니다.
        public int softCurrency;
        // 마지막으로 선택한 캐릭터입니다.
        public ClientCharacterId selectedCharacter = ClientCharacterId.Dog;
        // 보유 코스메틱 ID 목록입니다.
        public List<string> ownedCosmeticIds = new List<string>();
        // 캐릭터마다 장착한 꾸미기 정보입니다.
        public List<ClientCharacterLoadout> characterLoadouts = new List<ClientCharacterLoadout>();
        // 중복 보상 수령 방지를 위해 기록한 경기 ID 목록입니다.
        public List<string> claimedMatchIds = new List<string>();
        // 전체 음량 설정값(0~1)입니다.
        [Range(0, 1)] public float masterVolume = 1f;
        // 음악 음량 설정값(0~1)입니다.
        [Range(0, 1)] public float musicVolume = .8f;
        // 효과음 음량 설정값(0~1)입니다.
        [Range(0, 1)] public float effectsVolume = 1f;

        // 지정 캐릭터의 장착 데이터를 찾아 반환합니다.
        public ClientCharacterLoadout LoadoutFor(ClientCharacterId id)
        {
            return characterLoadouts != null
                ? characterLoadouts.Find(loadout => loadout != null && loadout.character == id)
                : null;
        }
    }

    /// <summary>프로필 데이터를 불러오고 저장하며, Unity UI 오브젝트는 다루지 않습니다.</summary>
    public sealed class ClientProfileStore
    {
        // 프로필 저장에 사용하는 PlayerPrefs 키입니다.
        private const string PlayerPrefsKey = "mushroom-survival.client-profile.v1";
        private ClientMenuCatalog catalog;
        public ClientProfile Profile { get; private set; }
        // 프로필 데이터가 바뀌었을 때 UI 갱신을 알리는 이벤트입니다.
        public event Action<ClientProfile> Changed;

        // 카탈로그를 기준으로 프로필을 불러오고 보정한 뒤 저장합니다.
        public void Initialize(ClientMenuCatalog menuCatalog)
        {
            catalog = menuCatalog;
            Profile = Load();
            Normalize();
            Save();
        }

        // 카탈로그에 존재하는 캐릭터만 선택 상태로 저장합니다.
        public void SelectCharacter(ClientCharacterId id)
        {
            if (catalog == null || catalog.FindCharacter(id) == null) return;
            Update(profile => profile.selectedCharacter = id);
        }

        // 지정 캐릭터의 장착 정보를 조회합니다.
        public ClientCharacterLoadout LoadoutFor(ClientCharacterId character)
        {
            return Profile != null ? Profile.LoadoutFor(character) : null;
        }

        // 세 종류의 음량 값을 0~1 범위로 제한하여 저장합니다.
        public void SetVolumes(float master, float music, float effects)
        {
            Update(profile =>
            {
                profile.masterVolume = Mathf.Clamp01(master);
                profile.musicVolume = Mathf.Clamp01(music);
                profile.effectsVolume = Mathf.Clamp01(effects);
            });
        }

        // 프로필을 수정하고 정규화·저장한 다음 변경 이벤트를 알립니다.
        internal void Update(Action<ClientProfile> update)
        {
            if (Profile == null) return;
            update?.Invoke(Profile);
            Normalize();
            Save();
            Changed?.Invoke(Profile);
        }

        // PlayerPrefs의 JSON을 읽고 실패하면 기본 프로필을 생성합니다.
        private ClientProfile Load()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey)) return NewProfile();
            try { return JsonUtility.FromJson<ClientProfile>(PlayerPrefs.GetString(PlayerPrefsKey)) ?? NewProfile(); }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read client menu profile; using defaults. " + exception.Message);
                return NewProfile();
            }
        }

        // 카탈로그의 초기 재화를 적용한 새 프로필을 만듭니다.
        private ClientProfile NewProfile()
        {
            return new ClientProfile { softCurrency = catalog != null ? Mathf.Max(0, catalog.startingCurrency) : 0 };
        }

        // 누락된 목록과 캐릭터별 장착 정보를 보정하고 값 범위를 정리합니다.
        private void Normalize()
        {
            if (Profile == null) Profile = NewProfile();
            Profile.schemaVersion = 1;
            Profile.softCurrency = Mathf.Max(0, Profile.softCurrency);
            if (Profile.ownedCosmeticIds == null) Profile.ownedCosmeticIds = new List<string>();
            if (Profile.claimedMatchIds == null) Profile.claimedMatchIds = new List<string>();
            if (Profile.characterLoadouts == null) Profile.characterLoadouts = new List<ClientCharacterLoadout>();
            Profile.masterVolume = Mathf.Clamp01(Profile.masterVolume);
            Profile.musicVolume = Mathf.Clamp01(Profile.musicVolume);
            Profile.effectsVolume = Mathf.Clamp01(Profile.effectsVolume);
            if (catalog == null) return;
            if (catalog.characters != null)
                foreach (var character in catalog.characters)
                    if (character != null && LoadoutFor(character.id) == null)
                        Profile.characterLoadouts.Add(new ClientCharacterLoadout { character = character.id });
            if (catalog.cosmetics == null) return;
            foreach (var item in catalog.cosmetics)
                if (item != null && item.price <= 0 && !string.IsNullOrEmpty(item.id) && !Profile.ownedCosmeticIds.Contains(item.id))
                    Profile.ownedCosmeticIds.Add(item.id);
        }

        // 현재 프로필을 JSON으로 직렬화해 PlayerPrefs에 저장합니다.
        private void Save()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Profile));
            PlayerPrefs.Save();
        }
    }
}
