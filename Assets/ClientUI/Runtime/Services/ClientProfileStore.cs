using System;
using System.IO;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    public sealed class ClientProfileStore
    {
        private const string FileName = "client-profile.json";
        private readonly ClientMenuCatalog catalog;
        private readonly string profilePath;

        public ClientProfile Profile { get; private set; }
        public event Action<ClientProfile> Changed;

        public ClientProfileStore(ClientMenuCatalog menuCatalog)
        {
            catalog = menuCatalog;
            profilePath = Path.Combine(Application.persistentDataPath, FileName);
            Profile = Load();
            Normalize();
            Save();
        }

        internal void Update(Action<ClientProfile> update)
        {
            update?.Invoke(Profile);
            Normalize();
            Save();
            Changed?.Invoke(Profile);
        }

        public void SelectCharacter(ClientCharacterId character)
        {
            if (catalog == null || catalog.FindCharacter(character) == null) return;
            Update(profile => profile.selectedCharacter = (int)character);
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

        private ClientProfile Load()
        {
            if (!File.Exists(profilePath))
                return NewProfile();

            try
            {
                var profile = JsonUtility.FromJson<ClientProfile>(File.ReadAllText(profilePath));
                return profile ?? NewProfile();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Client profile could not be read; a default profile was created. {exception.Message}");
                return NewProfile();
            }
        }

        private ClientProfile NewProfile()
        {
            return new ClientProfile
            {
                softCurrency = catalog != null ? Mathf.Max(0, catalog.startingCurrency) : 0,
                selectedCharacter = (int)ClientCharacterId.Dog
            };
        }

        private void Normalize()
        {
            if (Profile == null) Profile = NewProfile();
            Profile.schemaVersion = 1;
            Profile.softCurrency = Mathf.Max(0, Profile.softCurrency);
            if (Profile.ownedCosmeticIds == null) Profile.ownedCosmeticIds = new System.Collections.Generic.List<string>();
            if (Profile.claimedMatchIds == null) Profile.claimedMatchIds = new System.Collections.Generic.List<string>();
            Profile.masterVolume = Mathf.Clamp01(Profile.masterVolume);
            Profile.musicVolume = Mathf.Clamp01(Profile.musicVolume);
            Profile.effectsVolume = Mathf.Clamp01(Profile.effectsVolume);

            if (catalog?.cosmetics == null) return;
            for (int i = 0; i < catalog.cosmetics.Length; i++)
            {
                var cosmetic = catalog.cosmetics[i];
                if (cosmetic != null && cosmetic.price <= 0 && !string.IsNullOrEmpty(cosmetic.id) &&
                    !Profile.ownedCosmeticIds.Contains(cosmetic.id))
                    Profile.ownedCosmeticIds.Add(cosmetic.id);
            }
        }

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temporaryPath = profilePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Profile, true));
                if (File.Exists(profilePath))
                {
                    var backupPath = profilePath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Replace(temporaryPath, profilePath, backupPath);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, profilePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Client profile could not be saved. {exception.Message}");
            }
        }
    }
}
