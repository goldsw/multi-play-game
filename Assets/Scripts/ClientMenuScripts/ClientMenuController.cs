using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MushroomSurvival.ClientUI
{
    [Serializable]
    public sealed class ClientMenuCharacterSlot
    {
        public Button button;
        public Text label;
        public Image portrait;
    }

    [Serializable]
    public sealed class ClientMenuCosmeticSlot
    {
        public GameObject root;
        public Button button;
        public Text label;
        public Image icon;
    }

    /// <summary>
    /// Binds pre-existing scene UI to client data. All Canvas objects and controls are assigned in the Inspector.
    /// This class never creates, clones, or destroys UI objects.
    /// </summary>
    public sealed class ClientMenuController : MonoBehaviour
    {
        public event Action<string, bool> RoomRequestPreview;
        public event Action<string> MessageSubmittedPreview;

        [Header("Data and existing scene pages")]
        [SerializeField] private ClientMenuCatalog catalog;
        [SerializeField] private ClientCharacterPresenter characterPresenter;
        [SerializeField] private GameObject[] pages = new GameObject[8];
        [SerializeField] private ClientMenuCharacterSlot[] characterSlots = Array.Empty<ClientMenuCharacterSlot>();
        [SerializeField] private ClientMenuCosmeticSlot[] cosmeticSlots = Array.Empty<ClientMenuCosmeticSlot>();
        [SerializeField] private Text[] itemRows = Array.Empty<Text>();

        [Header("Existing scene labels and preview images")]
        [SerializeField] private Text currencyLabel;
        [SerializeField] private Text noticeLabel;
        [SerializeField] private Text characterDetails;
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Text shopDetails;
        [SerializeField] private Image cosmeticPreviewImage;
        [SerializeField] private Text lobbyStatus;
        [SerializeField] private Text rewardDetails;
        [SerializeField] private Text messageHistory;

        [Header("Existing scene inputs, sliders, and audio source")]
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private InputField messageInput;
        [SerializeField] private Slider[] volumeSliders = Array.Empty<Slider>();
        [SerializeField] private AudioSource previewAudio;

        private readonly List<ClientCosmeticDefinition> visibleCosmetics = new List<ClientCosmeticDefinition>();
        private readonly List<ButtonListener> buttonListeners = new List<ButtonListener>();
        private readonly List<SliderListener> sliderListeners = new List<SliderListener>();
        private readonly List<string> recentMessages = new List<string>();
        private ClientProfileStore profiles;
        private ClientEconomyService economy;
        private ClientCosmeticKind shopCategory;
        private ClientCosmeticDefinition selectedCosmetic;
        private int selectedRewardRank = 1;
        private string rewardPreviewId;
        private bool locallyReady;

        private sealed class ButtonListener
        {
            public Button button;
            public UnityAction callback;
        }

        private sealed class SliderListener
        {
            public Slider slider;
            public UnityAction<float> callback;
        }

        private void Awake()
        {
            if (catalog == null)
            {
                Debug.LogError("Assign ClientMenuCatalog in the Inspector.", this);
                enabled = false;
                return;
            }

            profiles = new ClientProfileStore();
            profiles.Initialize(catalog);
            economy = new ClientEconomyService(catalog, profiles);
            profiles.Changed += OnProfileChanged;
            if (previewAudio != null) previewAudio.playOnAwake = false;
            BindExistingButtons();
            BindExistingSliders();
            ShowPage(0);
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (profiles != null) profiles.Changed -= OnProfileChanged;
            foreach (var binding in buttonListeners)
                if (binding.button != null) binding.button.onClick.RemoveListener(binding.callback);
            foreach (var binding in sliderListeners)
                if (binding.slider != null) binding.slider.onValueChanged.RemoveListener(binding.callback);
        }

        private void BindExistingButtons()
        {
            var actions = GetComponentsInChildren<ClientMenuAction>(true);
            foreach (var action in actions)
            {
                var button = action.GetComponent<Button>();
                if (button == null) continue;
                var capturedAction = action;
                UnityAction callback = () => Dispatch(capturedAction.action, capturedAction.index);
                button.onClick.AddListener(callback);
                buttonListeners.Add(new ButtonListener { button = button, callback = callback });
            }
        }

        private void BindExistingSliders()
        {
            if (volumeSliders == null) return;
            for (var i = 0; i < volumeSliders.Length && i < 3; i++)
            {
                var slider = volumeSliders[i];
                if (slider == null) continue;
                var index = i;
                UnityAction<float> callback = value => SetVolume(index, value);
                slider.onValueChanged.AddListener(callback);
                sliderListeners.Add(new SliderListener { slider = slider, callback = callback });
                var saved = i == 0 ? profiles.Profile.masterVolume : i == 1 ? profiles.Profile.musicVolume : profiles.Profile.effectsVolume;
                slider.SetValueWithoutNotify(saved);
            }
        }

        private void Dispatch(ClientMenuActionType action, int index)
        {
            switch (action)
            {
                case ClientMenuActionType.OpenPage: ShowPage(index); break;
                case ClientMenuActionType.SelectCharacter: SelectCharacter(index); break;
                case ClientMenuActionType.SelectShopCategory: SelectShopCategory(index); break;
                case ClientMenuActionType.SelectCosmeticSlot: SelectCosmetic(index); break;
                case ClientMenuActionType.PurchaseSelected: PurchaseSelected(); break;
                case ClientMenuActionType.EquipSelected: EquipSelected(); break;
                case ClientMenuActionType.PreviewSelectedAudio: PreviewSelectedAudio(); break;
                case ClientMenuActionType.PreviewEquippedVictoryPose: PreviewEquippedVictoryPose(); break;
                case ClientMenuActionType.PlayEquippedCharacterVoice: PlayEquippedCharacterVoice(); break;
                case ClientMenuActionType.SelectRewardRank: SelectRewardRank(index + 1); break;
                case ClientMenuActionType.ClaimReward: ClaimReward(); break;
                case ClientMenuActionType.CreateRoomPreview: RequestRoom(true); break;
                case ClientMenuActionType.JoinRoomPreview: RequestRoom(false); break;
                case ClientMenuActionType.ToggleReadyPreview: ToggleReady(); break;
                case ClientMenuActionType.SubmitMessagePreview: SubmitMessage(); break;
            }
        }

        private void ShowPage(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;
            for (var i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].SetActive(i == index);
            SetNotice(string.Empty);
            if (index == 1) RefreshCharacter();
            if (index == 2) RefreshCosmetics();
            if (index == 4) RefreshReward();
            if (index == 5) RefreshItems();
        }

        private void SelectCharacter(int slotIndex)
        {
            if (catalog.characters == null || slotIndex < 0 || slotIndex >= catalog.characters.Count) return;
            var entry = catalog.characters[slotIndex];
            if (entry == null) return;
            profiles.SelectCharacter(entry.id);
            RefreshCharacter();
        }

        private void SelectShopCategory(int categoryIndex)
        {
            if (!Enum.IsDefined(typeof(ClientCosmeticKind), categoryIndex)) return;
            shopCategory = (ClientCosmeticKind)categoryIndex;
            selectedCosmetic = null;
            RefreshCosmetics();
        }

        private void SelectCosmetic(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= visibleCosmetics.Count) return;
            selectedCosmetic = visibleCosmetics[slotIndex];
            RefreshSelectedCosmetic();
        }

        private void PurchaseSelected()
        {
            if (selectedCosmetic == null) { SetNotice("상품을 먼저 선택하세요."); return; }
            economy.TryPurchase(selectedCosmetic.id, out var message);
            SetNotice(message);
            RefreshCosmetics();
        }

        private void EquipSelected()
        {
            if (selectedCosmetic == null) { SetNotice("상품을 먼저 선택하세요."); return; }
            economy.TryEquip(selectedCosmetic.id, out var message);
            SetNotice(message);
            RefreshCosmetics();
            RefreshCharacter();
        }

        private void PreviewSelectedAudio()
        {
            if (selectedCosmetic == null || selectedCosmetic.previewAudio == null || previewAudio == null)
            {
                SetNotice("연결된 사운드 미리보기 에셋이 없습니다.");
                return;
            }
            previewAudio.Stop();
            previewAudio.clip = selectedCosmetic.previewAudio;
            previewAudio.Play();
            SetNotice(selectedCosmetic.displayName + " 미리 듣기");
        }

        private void PreviewEquippedVictoryPose()
        {
            SetNotice(characterPresenter != null && characterPresenter.PreviewVictoryPose()
                ? "장착한 승리 포즈를 미리 보여줍니다."
                : "장착한 포즈 또는 Animator 상태가 연결되지 않았습니다.");
        }

        private void PlayEquippedCharacterVoice()
        {
            SetNotice(characterPresenter != null && characterPresenter.PlayEquippedVoice()
                ? "장착한 캐릭터 사운드를 재생합니다."
                : "장착한 캐릭터 사운드가 연결되지 않았습니다.");
        }

        private void SelectRewardRank(int rank)
        {
            selectedRewardRank = Mathf.Clamp(rank, 1, Math.Max(1, catalog.rankRewards != null ? catalog.rankRewards.Length : 1));
            rewardPreviewId = "client-menu-preview-rank-" + selectedRewardRank;
            RefreshReward();
        }

        private void ClaimReward()
        {
            if (string.IsNullOrEmpty(rewardPreviewId)) SelectRewardRank(selectedRewardRank);
            var claimed = economy.TryClaimReward(rewardPreviewId, selectedRewardRank, out var amount);
            SetNotice(claimed ? $"{amount:N0} 잎을 받았습니다." : "이 미리보기 보상은 이미 받았습니다.");
            RefreshReward();
        }

        private void RequestRoom(bool create)
        {
            var code = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;
            RoomRequestPreview?.Invoke(code, create);
            if (lobbyStatus != null) lobbyStatus.text = create ? "방 생성 요청 미리보기" : $"방 코드 {code} 참가 요청 미리보기";
        }

        private void ToggleReady()
        {
            locallyReady = !locallyReady;
            if (lobbyStatus != null) lobbyStatus.text = locallyReady ? "로컬 준비 완료" : "로컬 준비 해제";
        }

        private void SubmitMessage()
        {
            var message = messageInput != null ? messageInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(message)) { SetNotice("메시지를 입력하세요."); return; }
            MessageSubmittedPreview?.Invoke(message);
            recentMessages.Add(message);
            if (recentMessages.Count > 6) recentMessages.RemoveAt(0);
            if (messageHistory != null) messageHistory.text = string.Join("\n", recentMessages);
            if (messageInput != null) messageInput.text = string.Empty;
        }

        private void SetVolume(int index, float value)
        {
            var profile = profiles.Profile;
            if (index == 0)
            {
                AudioListener.volume = value;
                profiles.SetVolumes(value, profile.musicVolume, profile.effectsVolume);
            }
            else if (index == 1) profiles.SetVolumes(profile.masterVolume, value, profile.effectsVolume);
            else profiles.SetVolumes(profile.masterVolume, profile.musicVolume, value);
        }

        private void RefreshAll()
        {
            RefreshCurrency();
            RefreshCharacter();
            RefreshCosmetics();
            RefreshReward();
            RefreshItems();
        }

        private void RefreshCurrency()
        {
            if (currencyLabel != null && profiles != null) currencyLabel.text = $"{profiles.Profile.softCurrency:N0} 잎";
        }

        private void RefreshCharacter()
        {
            if (catalog == null || profiles == null) return;
            var character = catalog.FindCharacter(profiles.Profile.selectedCharacter);
            if (character == null) return;
            if (characterPresenter != null) characterPresenter.Present(catalog, profiles.Profile);
            if (characterDetails != null)
            {
                var profile = profiles.Profile;
                var loadout = profile.LoadoutFor(character.id);
                characterDetails.text = $"{character.displayName}\n{character.description}\n고유 스킬 보유: {(character.hasAssignedSkill ? "보유" : "미보유")}\n스킨: {CosmeticName(loadout?.equippedSkinId)} · 사운드: {CosmeticName(loadout?.equippedVoiceId)} · 승리 포즈: {CosmeticName(loadout?.equippedVictoryPoseId)}";
            }
            if (characterPortrait != null) characterPortrait.sprite = character.portrait;
            for (var i = 0; i < characterSlots.Length; i++)
            {
                var definition = i < catalog.characters.Count ? catalog.characters[i] : null;
                var slot = characterSlots[i];
                if (slot == null || definition == null) continue;
                if (slot.label != null) slot.label.text = definition.displayName + (definition.id == character.id ? " ✓" : string.Empty);
                if (slot.portrait != null) slot.portrait.sprite = definition.portrait;
            }
        }

        private void RefreshCosmetics()
        {
            visibleCosmetics.Clear();
            if (catalog.cosmetics != null)
                foreach (var item in catalog.cosmetics)
                    if (item != null && item.kind == shopCategory &&
                        (item.character == ClientCharacterId.Any || item.character == profiles.Profile.selectedCharacter))
                        visibleCosmetics.Add(item);

            for (var i = 0; i < cosmeticSlots.Length; i++)
            {
                var slot = cosmeticSlots[i];
                if (slot == null) continue;
                var hasItem = i < visibleCosmetics.Count;
                if (slot.root != null) slot.root.SetActive(hasItem);
                if (!hasItem) continue;
                var item = visibleCosmetics[i];
                if (slot.label != null) slot.label.text = $"{item.displayName}\n{(economy.Owns(item.id) ? "보유" : $"{item.price:N0} 잎")}";
                if (slot.icon != null) slot.icon.sprite = item.icon;
            }
            if (selectedCosmetic != null && !visibleCosmetics.Contains(selectedCosmetic)) selectedCosmetic = null;
            RefreshSelectedCosmetic();
        }

        private void RefreshSelectedCosmetic()
        {
            if (selectedCosmetic == null)
            {
                if (shopDetails != null) shopDetails.text = "상품을 선택하면 설명과 가격이 표시됩니다.";
                if (cosmeticPreviewImage != null) cosmeticPreviewImage.sprite = null;
                return;
            }
            var state = economy.Owns(selectedCosmetic.id) ? "보유 중" : $"가격: {selectedCosmetic.price:N0} 잎";
            if (shopDetails != null) shopDetails.text = $"{selectedCosmetic.displayName}\n{selectedCosmetic.description}\n{state}";
            if (cosmeticPreviewImage != null) cosmeticPreviewImage.sprite = selectedCosmetic.previewSprite;
        }

        private void RefreshReward()
        {
            if (rewardDetails != null)
                rewardDetails.text = $"{selectedRewardRank}위 보상: {economy.RewardForRank(selectedRewardRank):N0} 잎\n실제 경기 결과와 연결되지 않은 미리보기입니다.";
        }

        private void RefreshItems()
        {
            for (var i = 0; i < itemRows.Length; i++)
            {
                var row = itemRows[i];
                if (row == null) continue;
                var item = catalog.items != null && i < catalog.items.Count ? catalog.items[i] : null;
                row.text = item == null ? string.Empty : $"{item.displayName} · {item.itemType} · {item.effectDescription}";
            }
        }

        private string CosmeticName(string id)
        {
            var item = catalog.FindCosmetic(id);
            return item != null ? item.displayName : "기본";
        }

        private void OnProfileChanged(ClientProfile _)
        {
            RefreshCurrency();
            RefreshCharacter();
            RefreshCosmetics();
        }

        private void SetNotice(string message)
        {
            if (noticeLabel != null) noticeLabel.text = message;
        }
    }
}
