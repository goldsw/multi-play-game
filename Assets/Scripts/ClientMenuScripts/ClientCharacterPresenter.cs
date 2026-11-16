using System;
using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    [Serializable]
    public sealed class ClientCharacterRendererSlot
    {
        public string key;
        public Renderer target;
        public Material[] defaultMaterials = Array.Empty<Material>();
    }

    [Serializable]
    public sealed class ClientCharacterSceneBinding
    {
        public ClientCharacterId character;
        [Tooltip("Existing, pre-placed scene model root. This presenter only toggles it; it never instantiates models.")]
        public GameObject sceneRoot;
        public Animator animator;
        public AudioSource voiceSource;
        public List<ClientCharacterRendererSlot> renderers = new List<ClientCharacterRendererSlot>();
    }

    /// <summary>
    /// Presents a character using scene-authored model instances and Inspector-assigned references.
    /// It applies materials/audio/Animator states without creating GameObjects or runtime material copies.
    /// </summary>
    public sealed class ClientCharacterPresenter : MonoBehaviour
    {
        [SerializeField] private List<ClientCharacterSceneBinding> sceneCharacters = new List<ClientCharacterSceneBinding>();
        [SerializeField] private bool hideUnselectedSceneCharacters = true;

        private ClientMenuCatalog catalog;
        private ClientProfile profile;
        private ClientCharacterSceneBinding activeBinding;
        private ClientCharacterLoadout activeLoadout;

        public void Present(ClientMenuCatalog menuCatalog, ClientProfile currentProfile)
        {
            catalog = menuCatalog;
            profile = currentProfile;
            if (catalog == null || profile == null) return;

            activeBinding = null;
            if (sceneCharacters == null) return;
            foreach (var binding in sceneCharacters)
            {
                if (binding == null) continue;
                var selected = binding.character == profile.selectedCharacter;
                if (selected) activeBinding = binding;
                if (hideUnselectedSceneCharacters && binding.sceneRoot != null)
                    binding.sceneRoot.SetActive(selected);
            }

            if (activeBinding == null)
            {
                Debug.LogWarning("No scene character binding matches the selected character.", this);
                return;
            }

            activeLoadout = profile.LoadoutFor(profile.selectedCharacter);
            if (activeLoadout == null) return;

            ApplyEquippedSkin();
            SetEquippedVoiceClip();
        }

        public bool PreviewVictoryPose()
        {
            if (activeBinding == null || activeBinding.animator == null || catalog == null || profile == null) return false;
            var pose = FindEquipped(ClientCosmeticKind.VictoryPose);
            if (pose == null || string.IsNullOrWhiteSpace(pose.animatorState)) return false;

            var stateHash = Animator.StringToHash(pose.animatorState);
            if (!activeBinding.animator.HasState(0, stateHash))
            {
                Debug.LogWarning($"Animator state '{pose.animatorState}' is missing for {activeBinding.character}.", activeBinding.animator);
                return false;
            }

            activeBinding.animator.CrossFadeInFixedTime(stateHash, .12f);
            return true;
        }

        public bool PlayEquippedVoice()
        {
            if (activeBinding == null || activeBinding.voiceSource == null) return false;
            var voice = FindEquipped(ClientCosmeticKind.Voice);
            if (voice == null || voice.characterAudio == null) return false;
            activeBinding.voiceSource.PlayOneShot(voice.characterAudio);
            return true;
        }

        private void ApplyEquippedSkin()
        {
            var skin = FindEquipped(ClientCosmeticKind.Skin);
            if (activeBinding.renderers == null) return;
            foreach (var slot in activeBinding.renderers)
            {
                if (slot == null || slot.target == null) continue;
                if (slot.defaultMaterials != null && slot.defaultMaterials.Length > 0)
                    slot.target.sharedMaterials = slot.defaultMaterials;
                if (skin == null || skin.materialOverrides == null) continue;
                foreach (var materialOverride in skin.materialOverrides)
                {
                    if (materialOverride == null || materialOverride.rendererKey != slot.key || materialOverride.materials == null || materialOverride.materials.Length == 0) continue;
                    slot.target.sharedMaterials = materialOverride.materials;
                    break;
                }
            }
        }

        private void SetEquippedVoiceClip()
        {
            if (activeBinding == null || activeBinding.voiceSource == null) return;
            var voice = FindEquipped(ClientCosmeticKind.Voice);
            activeBinding.voiceSource.clip = voice != null ? voice.characterAudio : null;
        }

        private ClientCosmeticDefinition FindEquipped(ClientCosmeticKind kind)
        {
            string id;
            switch (kind)
            {
                case ClientCosmeticKind.Skin: id = activeLoadout.equippedSkinId; break;
                case ClientCosmeticKind.Voice: id = activeLoadout.equippedVoiceId; break;
                case ClientCosmeticKind.VictoryPose: id = activeLoadout.equippedVictoryPoseId; break;
                default: return null;
            }

            var cosmetic = catalog.FindCosmetic(id);
            if (cosmetic == null || (cosmetic.character != profile.selectedCharacter && cosmetic.character != ClientCharacterId.Any))
                return null;
            return cosmetic.kind == kind ? cosmetic : null;
        }
    }
}
