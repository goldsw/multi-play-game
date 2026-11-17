using System;
using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    // 스킨 적용 대상 Renderer와 기본 Material을 Inspector에서 연결합니다.
    [Serializable]
    public sealed class ClientCharacterRendererSlot
    {
        // 카탈로그의 Material override가 참조하는 Renderer 식별자입니다.
        public string key;
        // 씬에 배치된 캐릭터 모델의 Renderer입니다.
        public Renderer target;
        // 스킨을 바꾸기 전에 복원할 기본 Material 목록입니다.
        public Material[] defaultMaterials = Array.Empty<Material>();
    }

    // 캐릭터 하나의 씬 모델, Animator, 음성 및 Renderer 참조를 묶습니다.
    [Serializable]
    public sealed class ClientCharacterSceneBinding
    {
        // 이 바인딩이 표시할 캐릭터 ID입니다.
        public ClientCharacterId character;
        [Tooltip("씬에 미리 배치한 모델 루트입니다. 이 Presenter는 활성 상태만 바꾸며 모델을 생성하지 않습니다.")]
        public GameObject sceneRoot;
        // 승리 포즈 미리보기에 사용할 Animator입니다.
        public Animator animator;
        // 장착 음성을 재생할 AudioSource입니다.
        public AudioSource voiceSource;
        // 스킨 적용 대상 Renderer 목록입니다.
        public List<ClientCharacterRendererSlot> renderers = new List<ClientCharacterRendererSlot>();
    }

    /// <summary>
    /// 씬에 미리 배치한 모델과 Inspector 참조를 사용해 캐릭터를 표시합니다.
    /// GameObject나 런타임 Material 복사본을 만들지 않고 Material·오디오·Animator 상태를 적용합니다.
    /// </summary>
    public sealed class ClientCharacterPresenter : MonoBehaviour
    {
        // 씬에 미리 배치된 캐릭터 모델 참조입니다.
        [SerializeField] private List<ClientCharacterSceneBinding> sceneCharacters = new List<ClientCharacterSceneBinding>();
        // 선택하지 않은 캐릭터 모델을 비활성화할지 결정합니다.
        [SerializeField] private bool hideUnselectedSceneCharacters = true;

        private ClientMenuCatalog catalog;
        private ClientProfile profile;
        private ClientCharacterSceneBinding activeBinding;
        private ClientCharacterLoadout activeLoadout;

        // 선택 캐릭터 모델을 표시하고 장착 스킨과 음성 클립을 적용합니다.
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

        // 장착한 승리 포즈의 Animator 상태를 재생합니다.
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

        // 장착한 캐릭터 음성을 한 번 재생합니다.
        public bool PlayEquippedVoice()
        {
            if (activeBinding == null || activeBinding.voiceSource == null) return false;
            var voice = FindEquipped(ClientCosmeticKind.Voice);
            if (voice == null || voice.characterAudio == null) return false;
            activeBinding.voiceSource.PlayOneShot(voice.characterAudio);
            return true;
        }

        // 기본 Material을 복원한 뒤 현재 스킨의 Material override를 적용합니다.
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

        // 현재 캐릭터 AudioSource에 장착 음성 클립을 지정합니다.
        private void SetEquippedVoiceClip()
        {
            if (activeBinding == null || activeBinding.voiceSource == null) return;
            var voice = FindEquipped(ClientCosmeticKind.Voice);
            activeBinding.voiceSource.clip = voice != null ? voice.characterAudio : null;
        }

        // 현재 캐릭터가 장착한 항목 중 종류와 캐릭터 조건이 맞는 데이터를 찾습니다.
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
