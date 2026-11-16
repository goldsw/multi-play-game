using System;
using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    [Serializable]
    public sealed class ClientProfile
    {
        public int schemaVersion = 1;
        public int softCurrency;
        public ClientCharacterId selectedCharacter = ClientCharacterId.Dog;
        public List<string> ownedCosmeticIds = new List<string>();
        public List<ClientCharacterLoadout> characterLoadouts = new List<ClientCharacterLoadout>();
        public List<string> claimedMatchIds = new List<string>();
        [Range(0, 1)] public float masterVolume = 1f;
        [Range(0, 1)] public float musicVolume = .8f;
        [Range(0, 1)] public float effectsVolume = 1f;

        public ClientCharacterLoadout LoadoutFor(ClientCharacterId id)
        {
            return characterLoadouts != null
                ? characterLoadouts.Find(loadout => loadout != null && loadout.character == id)
                : null;
        }
    }

    /// <summary>Plain data/persistence service. It has no Unity UI or GameObject responsibilities.</summary>
    public sealed class ClientProfileStore
    {
        private const string PlayerPrefsKey = "mushroom-survival.client-profile.v1";
        private ClientMenuCatalog catalog;
        public ClientProfile Profile { get; private set; }
        public event Action<ClientProfile> Changed;

        public void Initialize(ClientMenuCatalog menuCatalog)
        {
            catalog = menuCatalog;
            Profile = Load();
            Normalize();
            Save();
        }

        public void SelectCharacter(ClientCharacterId id)
        {
            if (catalog == null || catalog.FindCharacter(id) == null) return;
            Update(profile => profile.selectedCharacter = id);
        }

        public ClientCharacterLoadout LoadoutFor(ClientCharacterId character)
        {
            return Profile != null ? Profile.LoadoutFor(character) : null;
        }

        public void SetVolumes(float master, float music, float effects)
        {
            Update(profile =>
            {
                profile.masterVolume = Mathf.Clamp01(master);
                profile.musicVolume = Mathf.Clamp01(music);
                profile.effectsVolume = Mathf.Clamp01(effects);
            });
        }

        internal void Update(Action<ClientProfile> update)
        {
            if (Profile == null) return;
            update?.Invoke(Profile);
            Normalize();
            Save();
            Changed?.Invoke(Profile);
        }

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

        private ClientProfile NewProfile()
        {
            return new ClientProfile { softCurrency = catalog != null ? Mathf.Max(0, catalog.startingCurrency) : 0 };
        }

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

        private void Save()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Profile));
            PlayerPrefs.Save();
        }
    }
}
