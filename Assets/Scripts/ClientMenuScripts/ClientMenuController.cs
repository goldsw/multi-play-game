using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MushroomSurvival.ClientUI
{
    // 캐릭터 버튼과 이름·초상화 표시 참조를 하나의 슬롯으로 묶습니다.
    [Serializable]
    public sealed class ClientMenuCharacterSlot
    {
        public Button button;
        public Text label;
        public Image portrait;
    }

    // 상품 버튼과 이름·아이콘 표시 참조를 하나의 슬롯으로 묶습니다.
    [Serializable]
    public sealed class ClientMenuCosmeticSlot
    {
        public GameObject root;
        public Button button;
        public Text label;
        public Image icon;
    }

    /// <summary>
    /// 씬에 미리 배치된 UI를 클라이언트 데이터에 연결합니다. Canvas와 컨트롤은 Inspector에서 지정합니다.
    /// 이 클래스는 UI 오브젝트를 생성·복제·삭제하지 않습니다.
    /// </summary>
    public sealed class ClientMenuController : MonoBehaviour
    {
        // 외부 로컬 코드가 방 생성/참가 미리보기 요청을 구독하는 이벤트입니다.
        public event Action<string, bool> RoomRequestPreview;
        // 외부 로컬 코드가 메시지 제출 미리보기를 구독하는 이벤트입니다.
        public event Action<string> MessageSubmittedPreview;

        // 데이터 에셋, Presenter 및 씬에 미리 만든 페이지·슬롯 참조입니다.
        [Header("Data and existing scene pages")]
        [SerializeField] private ClientMenuCatalog catalog;
        [SerializeField] private ClientCharacterPresenter characterPresenter;
        [SerializeField] private GameObject[] pages = new GameObject[8];
        [SerializeField] private ClientMenuCharacterSlot[] characterSlots = Array.Empty<ClientMenuCharacterSlot>();
        [SerializeField] private ClientMenuCosmeticSlot[] cosmeticSlots = Array.Empty<ClientMenuCosmeticSlot>();
        [SerializeField] private Text[] itemRows = Array.Empty<Text>();

        // 화면에서 갱신할 텍스트와 이미지 참조입니다.
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

        // 입력 필드, 음량 슬라이더와 미리보기 AudioSource입니다.
        [Header("Existing scene inputs, sliders, and audio source")]
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private InputField messageInput;
        [SerializeField] private Slider[] volumeSliders = Array.Empty<Slider>();
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private AudioSource previewAudio;

        private readonly List<ClientCosmeticDefinition> visibleCosmetics = new List<ClientCosmeticDefinition>();
        private readonly List<ButtonListener> buttonListeners = new List<ButtonListener>();
        private readonly List<SliderListener> sliderListeners = new List<SliderListener>();
        private UnityAction<int> languageListener;
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

        // 프로필·경제 서비스를 초기화하고 기존 버튼과 슬라이더를 연결합니다.
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
            AudioListener.volume = profiles.Profile.masterVolume;
            profiles.Changed += OnProfileChanged;
            if (previewAudio != null) previewAudio.playOnAwake = false;
            BindExistingButtons();
            BindExistingSliders();
            BindLanguageDropdown();
            ShowPage(0);
            RefreshAll();
        }

        // 등록했던 이벤트 리스너를 해제합니다.
        private void OnDestroy()
        {
            if (profiles != null) profiles.Changed -= OnProfileChanged;
            foreach (var binding in buttonListeners)
                if (binding.button != null) binding.button.onClick.RemoveListener(binding.callback);
            foreach (var binding in sliderListeners)
                if (binding.slider != null) binding.slider.onValueChanged.RemoveListener(binding.callback);
            if (languageDropdown != null && languageListener != null)
                languageDropdown.onValueChanged.RemoveListener(languageListener);
        }

        // ClientMenuAction이 붙은 기존 Button을 찾아 클릭 동작을 연결합니다.
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

        // 음량 슬라이더 최대 세 개를 로컬 프로필 값에 연결합니다.
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
                var saved = i == 0 ? profiles.Profile.effectsVolume : i == 1 ? profiles.Profile.musicVolume : profiles.Profile.masterVolume;
                slider.SetValueWithoutNotify(saved);
            }
        }

        // 언어 선택값을 로컬 환경설정으로 저장합니다. 실제 번역 문자열은 별도 현지화 데이터가 필요합니다.
        private void BindLanguageDropdown()
        {
            if (languageDropdown == null) return;
            if (languageDropdown.options == null || languageDropdown.options.Count == 0)
                languageDropdown.AddOptions(new List<string> { "한국어", "English", "日本語" });
            var selected = Mathf.Clamp(PlayerPrefs.GetInt("mushroom-survival.language", 0), 0, languageDropdown.options.Count - 1);
            languageDropdown.SetValueWithoutNotify(selected);
            languageListener = value =>
            {
                PlayerPrefs.SetInt("mushroom-survival.language", value);
                PlayerPrefs.Save();
                SetNotice("언어 선택이 저장되었습니다. 번역 데이터는 별도 연결이 필요합니다.");
            };
            languageDropdown.onValueChanged.AddListener(languageListener);
        }

        // 버튼에 지정된 동작 종류에 따라 해당 처리 메서드를 호출합니다.
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
                case ClientMenuActionType.LeaveRoomPreview: LeaveRoom(); break;
            }
        }

        // 지정 페이지 하나만 표시하고 해당 페이지의 데이터를 갱신합니다.
        private void ShowPage(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;
            for (var i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].SetActive(i == index);
            SetNotice(string.Empty);
            if (index == 1) RefreshCharacter();
            if (index == 2) RefreshCosmetics();
            if (index == 6) RefreshReward();
            if (index == 5) RefreshItems();
        }

        // Catalog의 인덱스로 캐릭터를 선택하고 표시를 갱신합니다.
        private void SelectCharacter(int slotIndex)
        {
            if (catalog.characters == null || slotIndex < 0 || slotIndex >= catalog.characters.Count) return;
            var entry = catalog.characters[slotIndex];
            if (entry == null) return;
            profiles.SelectCharacter(entry.id);
            RefreshCharacter();
        }

        // 상점 분류를 바꾸고 해당 상품 목록을 다시 표시합니다.
        private void SelectShopCategory(int categoryIndex)
        {
            if (!Enum.IsDefined(typeof(ClientCosmeticKind), categoryIndex)) return;
            shopCategory = (ClientCosmeticKind)categoryIndex;
            selectedCosmetic = null;
            RefreshCosmetics();
        }

        // 현재 상품 목록에서 선택 상품을 지정합니다.
        private void SelectCosmetic(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= visibleCosmetics.Count) return;
            selectedCosmetic = visibleCosmetics[slotIndex];
            RefreshSelectedCosmetic();
        }

        // 선택한 상품을 로컬 재화로 구매합니다.
        private void PurchaseSelected()
        {
            if (selectedCosmetic == null) { SetNotice("상품을 먼저 선택하세요."); return; }
            economy.TryPurchase(selectedCosmetic.id, out var message);
            SetNotice(message);
            RefreshCosmetics();
        }

        // 선택한 보유 상품을 현재 캐릭터에 장착합니다.
        private void EquipSelected()
        {
            if (selectedCosmetic == null) { SetNotice("상품을 먼저 선택하세요."); return; }
            economy.TryEquip(selectedCosmetic.id, out var message);
            SetNotice(message);
            RefreshCosmetics();
            RefreshCharacter();
        }

        // 선택 상품에 연결된 오디오 미리보기를 재생합니다.
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

        // Presenter에 장착한 승리 포즈의 미리보기를 요청합니다.
        private void PreviewEquippedVictoryPose()
        {
            SetNotice(characterPresenter != null && characterPresenter.PreviewVictoryPose()
                ? "장착한 승리 포즈를 미리 보여줍니다."
                : "장착한 포즈 또는 Animator 상태가 연결되지 않았습니다.");
        }

        // Presenter에 장착 음성 재생을 요청합니다.
        private void PlayEquippedCharacterVoice()
        {
            SetNotice(characterPresenter != null && characterPresenter.PlayEquippedVoice()
                ? "장착한 캐릭터 사운드를 재생합니다."
                : "장착한 캐릭터 사운드가 연결되지 않았습니다.");
        }

        // 보상 화면에서 미리 볼 등수를 선택합니다.
        private void SelectRewardRank(int rank)
        {
            selectedRewardRank = Mathf.Clamp(rank, 1, Math.Max(1, catalog.rankRewards != null ? catalog.rankRewards.Length : 1));
            rewardPreviewId = "client-menu-preview-rank-" + selectedRewardRank;
            RefreshReward();
        }

        // 로컬 미리보기 보상을 수령하고 결과 문구를 표시합니다.
        private void ClaimReward()
        {
            if (string.IsNullOrEmpty(rewardPreviewId)) SelectRewardRank(selectedRewardRank);
            var claimed = economy.TryClaimReward(rewardPreviewId, selectedRewardRank, out var amount);
            SetNotice(claimed ? $"{amount:N0} 잎을 받았습니다." : "이 미리보기 보상은 이미 받았습니다.");
            RefreshReward();
        }

        // 방 생성 또는 참가 미리보기 이벤트를 알리고 상태 문구를 갱신합니다.
        private void RequestRoom(bool create)
        {
            var code = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;
            RoomRequestPreview?.Invoke(code, create);
            if (lobbyStatus != null) lobbyStatus.text = create ? "방 생성 요청 미리보기" : $"방 코드 {code} 참가 요청 미리보기";
        }

        // 현재 방을 떠날 때의 로컬 안내만 표시합니다. 실제 참가자 이동은 네트워크 연결 후 처리합니다.
        private void LeaveRoom()
        {
            if (lobbyStatus != null) lobbyStatus.text = "방 나가기 미리보기 · 실제 이동은 네트워크 연결 후 처리됩니다.";
        }

        // 네트워크와 무관한 로컬 준비 상태 표시를 전환합니다.
        private void ToggleReady()
        {
            locallyReady = !locallyReady;
            if (lobbyStatus != null) lobbyStatus.text = locallyReady ? "로컬 준비 완료" : "로컬 준비 해제";
        }

        // 입력 메시지를 로컬 기록에 추가하고 최근 여섯 줄을 표시합니다.
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

        // 슬라이더 순서에 따라 효과음·배경음·전체 음량을 로컬 프로필에 저장합니다.
        private void SetVolume(int index, float value)
        {
            var profile = profiles.Profile;
            if (index == 0) profiles.SetVolumes(profile.masterVolume, profile.musicVolume, value);
            else if (index == 1) profiles.SetVolumes(profile.masterVolume, value, profile.effectsVolume);
            else
            {
                AudioListener.volume = value;
                profiles.SetVolumes(value, profile.musicVolume, profile.effectsVolume);
            }
        }

        // 주요 메뉴 화면의 표시 데이터를 모두 갱신합니다.
        private void RefreshAll()
        {
            RefreshCurrency();
            RefreshCharacter();
            RefreshCosmetics();
            RefreshReward();
            RefreshItems();
        }

        // 현재 로컬 재화 표시를 갱신합니다.
        private void RefreshCurrency()
        {
            if (currencyLabel != null && profiles != null) currencyLabel.text = $"{profiles.Profile.softCurrency:N0} 잎";
        }

        // 선택 캐릭터 정보와 슬롯, 초상화 및 모델 미리보기를 갱신합니다.
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
            if (characterPortrait != null && character.portrait != null) characterPortrait.sprite = character.portrait;
            for (var i = 0; i < characterSlots.Length; i++)
            {
                var definition = i < catalog.characters.Count ? catalog.characters[i] : null;
                var slot = characterSlots[i];
                if (slot == null || definition == null) continue;
                if (slot.label != null) slot.label.text = definition.displayName + (definition.id == character.id ? " ✓" : string.Empty);
                if (slot.portrait != null && definition.portrait != null) slot.portrait.sprite = definition.portrait;
            }
        }

        // 현재 캐릭터와 상품 분류에 맞는 상점 슬롯을 갱신합니다.
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
                if (slot.icon != null && item.icon != null) slot.icon.sprite = item.icon;
            }
            if (selectedCosmetic != null && !visibleCosmetics.Contains(selectedCosmetic)) selectedCosmetic = null;
            RefreshSelectedCosmetic();
        }

        // 선택 상품의 상세 설명과 미리보기 이미지를 갱신합니다.
        private void RefreshSelectedCosmetic()
        {
            if (selectedCosmetic == null)
            {
                if (shopDetails != null) shopDetails.text = catalog.cosmetics == null || catalog.cosmetics.Count == 0 ? "등록된 상품이 없습니다." : "상품을 선택하세요.";
                return;
            }
            var state = economy.Owns(selectedCosmetic.id) ? "보유 중" : $"가격: {selectedCosmetic.price:N0} 잎";
            if (shopDetails != null) shopDetails.text = $"{selectedCosmetic.displayName}\n{selectedCosmetic.description}\n{state}";
            if (cosmeticPreviewImage != null && selectedCosmetic.previewSprite != null) cosmeticPreviewImage.sprite = selectedCosmetic.previewSprite;
        }

        // 선택 등수의 로컬 미리보기 보상을 표시합니다.
        private void RefreshReward()
        {
            if (rewardDetails != null)
            {
                var reward = economy.RewardForRank(selectedRewardRank);
                rewardDetails.text = reward > 0 ? $"{selectedRewardRank}위 보상: {reward:N0} 잎" : "보상 정보 없음";
            }
        }

        // Catalog의 아이템 설명을 안내 텍스트에 표시합니다.
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

        // 코스메틱 ID를 이름으로 변환하며 찾지 못하면 기본값을 표시합니다.
        private string CosmeticName(string id)
        {
            var item = catalog.FindCosmetic(id);
            return item != null ? item.displayName : "기본";
        }

        // 프로필이 바뀌면 관련 메뉴 표시를 다시 갱신합니다.
        private void OnProfileChanged(ClientProfile _)
        {
            RefreshCurrency();
            RefreshCharacter();
            RefreshCosmetics();
        }

        // 연결된 안내 텍스트에 메시지를 표시합니다.
        private void SetNotice(string message)
        {
            if (noticeLabel != null) noticeLabel.text = message;
        }
    }
}



