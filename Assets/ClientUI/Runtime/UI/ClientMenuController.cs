using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival.ClientUI
{
    /// <summary>
    /// Builds an asset-free menu shell and exposes presentation events for future lobby/match adapters.
    /// This controller never starts a match or performs network or gameplay actions.
    /// </summary>
    public sealed class ClientMenuController : MonoBehaviour
    {
        public event Action<string, bool> RoomRequest;
        public event Action<string> MessageSubmitted;

        private static readonly Color Forest = new Color(0.055f, 0.12f, 0.105f, 1f);
        private static readonly Color PanelColor = new Color(0.11f, 0.20f, 0.17f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.27f, 0.48f, 0.34f, 1f);
        private static readonly Color Accent = new Color(0.91f, 0.73f, 0.34f, 1f);
        private static readonly Color Muted = new Color(0.79f, 0.85f, 0.80f, 1f);

        private ClientMenuCatalog catalog;
        private ClientProfileStore profileStore;
        private ClientEconomyService economy;
        private AudioSource previewAudio;
        private RectTransform screenRoot;
        private Text currencyText;
        private Text noticeText;
        private ClientCosmeticKind currentShopCategory;
        private InputField roomCodeInput;
        private InputField messageInput;
        private int previewRank = 1;
        private Text rankSelectionText;
        private string previewMatchId;
        private readonly List<string> messagePreview = new List<string>();
        private bool isLocallyReady;

        public void Initialize(ClientMenuCatalog menuCatalog, ClientProfileStore store, ClientEconomyService economyService)
        {
            catalog = menuCatalog;
            profileStore = store;
            economy = economyService;
            previewAudio = gameObject.AddComponent<AudioSource>();
            previewAudio.playOnAwake = false;
            previewAudio.loop = false;
            profileStore.Changed += OnProfileChanged;
            ShowMain();
        }

        private void OnDestroy()
        {
            if (profileStore != null) profileStore.Changed -= OnProfileChanged;
        }

        private void OnProfileChanged(ClientProfile _) => UpdateCurrencyLabel();

        private void ShowMain()
        {
            var body = BeginScreen("독버섯 폭탄 주의보", "숲 친구들과 함께하는 클라이언트 메뉴");
            var selected = CurrentCharacter();
            AddLabel(body, $"선택 캐릭터 · {selected.displayName}\n장착 스킨 · {CosmeticName(profileStore.Profile.equippedSkinId)}", 22, Accent, 74);
            AddButton(body, "캐릭터와 꾸미기", ShowCharacter, 58);
            AddButton(body, "상점", () => ShowShop(ClientCosmeticKind.Skin), 58);
            AddButton(body, "방 메뉴 (화면 미리보기)", ShowLobby, 58);
            AddButton(body, "재화 보상 미리보기", ShowResults, 58);
            AddButton(body, "아이템 / 스킬 정보", ShowReference, 58);
            AddButton(body, "메시지", ShowMessages, 58);
            AddButton(body, "설정", ShowSettings, 58);
            AddFooter(body, "※ 이 화면은 신규 클라이언트 UI 시연입니다. 네트워크·실제 경기 기능은 연결하지 않습니다.");
            FinishScreen(body);
        }

        private void ShowCharacter()
        {
            var body = BeginScreen("캐릭터 선택", "계정의 대표 캐릭터와 꾸미기 구성을 선택합니다.");
            var selected = CurrentCharacter();
            var equippedSkin = catalog.FindCosmetic(profileStore.Profile.equippedSkinId);
            AddPreviewCard(body, selected.displayName, selected.description, selected.placeholderColor,
                $"스킨  {CosmeticName(profileStore.Profile.equippedSkinId)}\n사운드  {CosmeticName(profileStore.Profile.equippedVoiceId)}\n승리 포즈  {CosmeticName(profileStore.Profile.equippedVictoryPoseId)}",
                equippedSkin != null ? equippedSkin.previewSprite : selected.portrait);
            for (int i = 0; i < catalog.characters.Length; i++)
            {
                var character = catalog.characters[i];
                if (character == null) continue;
                var marker = (int)character.id == profileStore.Profile.selectedCharacter ? "  ✓ 선택됨" : string.Empty;
                AddButton(body, character.displayName + marker, () =>
                {
                    profileStore.SelectCharacter(character.id);
                    ShowCharacter();
                }, 48);
            }
            AddButton(body, "꾸미기 상점", () => ShowShop(ClientCosmeticKind.Skin), 52);
            AddBackButton(body, ShowMain);
            FinishScreen(body);
        }

        private void ShowShop(ClientCosmeticKind category)
        {
            currentShopCategory = category;
            var categoryName = CategoryName(category);
            var body = BeginScreen("숲속 상점", $"{categoryName} 상품을 살펴보고 장착합니다. (재화는 로컬 프로필에 저장)");
            var tabs = AddRow(body, 48);
            AddButton(tabs, "스킨", () => ShowShop(ClientCosmeticKind.Skin), 44, category == ClientCosmeticKind.Skin ? Accent : ButtonColor);
            AddButton(tabs, "사운드", () => ShowShop(ClientCosmeticKind.Voice), 44, category == ClientCosmeticKind.Voice ? Accent : ButtonColor);
            AddButton(tabs, "승리 포즈", () => ShowShop(ClientCosmeticKind.VictoryPose), 44, category == ClientCosmeticKind.VictoryPose ? Accent : ButtonColor);

            var scroll = CreateScrollList(body);
            var found = false;
            if (catalog.cosmetics != null)
            {
                for (int i = 0; i < catalog.cosmetics.Length; i++)
                {
                    var item = catalog.cosmetics[i];
                    if (item == null || item.kind != category) continue;
                    found = true;
                    BuildShopRow(scroll, item);
                }
            }
            if (!found) AddLabel(scroll, "등록된 상품이 없습니다. ClientMenuCatalog 에셋에서 상품을 추가하세요.", 18, Muted, 54);
            AddButton(body, "아이템 / 스킬 도감", ShowReference, 46);
            AddBackButton(body, ShowMain);
            AddFooter(body, "준비 중인 그림·모델·사운드는 아이콘과 프리뷰 필드에 에셋을 넣으면 자동 표시됩니다.");
            FinishScreen(body);
        }

        private void BuildShopRow(RectTransform parent, ClientCosmeticDefinition item)
        {
            var row = AddRow(parent, 104);
            if (item.icon != null) AddImage(row, item.icon, new Vector2(76, 76));
            var details = CreateColumn(row, "상품 설명", new Vector2(0, 0), true);
            AddLabel(details, item.displayName, 20, Color.white, 30, TextAnchor.MiddleLeft);
            AddLabel(details, item.description, 14, Muted, 28, TextAnchor.MiddleLeft);
            var owned = economy.Owns(item.id);
            AddLabel(details, owned ? "보유 중" : $"가격 · {item.price:N0} 잎", 15, Accent, 28, TextAnchor.MiddleLeft);
            AddButton(row, owned ? IsEquipped(item) ? "장착 중" : "장착" : "구매", () =>
            {
                if (owned)
                {
                    economy.TryEquip(item.id, out var message);
                    ShowShop(currentShopCategory);
                    SetNotice(message);
                    return;
                }
                string resultMessage;
                if (economy.TryPurchase(item.id, out resultMessage))
                {
                    economy.TryEquip(item.id, out resultMessage);
                }
                ShowShop(currentShopCategory);
                SetNotice(resultMessage);
            }, 48, owned ? ButtonColor : Accent);
            if (item.kind == ClientCosmeticKind.Voice)
                AddButton(row, "미리 듣기", () => PlayPreview(item), 48, ButtonColor);
        }

        private void PlayPreview(ClientCosmeticDefinition item)
        {
            if (item.previewAudio == null)
            {
                SetNotice("사운드 에셋이 준비되면 미리 듣기를 사용할 수 있습니다.");
                return;
            }
            previewAudio.clip = item.previewAudio;
            previewAudio.volume = profileStore.Profile.masterVolume * profileStore.Profile.effectsVolume;
            previewAudio.Play();
        }

        private void ShowLobby()
        {
            var body = BeginScreen("방 메뉴", "로비 UI 프로토타입 · 실제 방 생성과 참가 네트워크는 연결하지 않았습니다.");
            AddLabel(body, "참가 인원 미리보기", 20, Accent, 36);
            AddLabel(body, $"1  나 ({(isLocallyReady ? "준비 완료" : "준비 전")})\n2  빈 자리\n3  빈 자리\n4  빈 자리", 19, Color.white, 120);
            AddButton(body, isLocallyReady ? "준비 취소" : "준비 완료", () =>
            {
                isLocallyReady = !isLocallyReady;
                ShowLobby();
            }, 46);
            roomCodeInput = AddInput(body, "방 코드 입력", 48);
            var actions = AddRow(body, 52);
            AddButton(actions, "방 만들기", () => RequestRoom("", true), 48);
            AddButton(actions, "방 참가", () => RequestRoom(roomCodeInput != null ? roomCodeInput.text : string.Empty, false), 48);
            AddButton(body, "캐릭터 선택", ShowCharacter, 50);
            AddButton(body, "보상 화면 미리보기", ShowResults, 50);
            AddBackButton(body, ShowMain);
            AddFooter(body, "방 버튼은 RoomRequest 이벤트만 내보냅니다. 게임 시작·방장 권한은 이 클라이언트 범위에서 처리하지 않습니다.");
            FinishScreen(body);
        }

        private void RequestRoom(string code, bool host)
        {
            if (!host && string.IsNullOrWhiteSpace(code))
            {
                SetNotice("참가할 방 코드를 입력하세요.");
                return;
            }
            if (RoomRequest == null)
            {
                SetNotice("네트워크 어댑터가 연결되지 않았습니다. 방 화면만 표시 중입니다.");
                return;
            }
            RoomRequest.Invoke(code.Trim(), host);
        }

        private void ShowMessages()
        {
            var body = BeginScreen("메시지", "채팅 전송은 제외했습니다. 여기서는 메시지 UI와 어댑터 이벤트만 제공합니다.");
            AddLabel(body, "최근 메시지", 20, Accent, 36);
            if (messagePreview.Count == 0)
                AddLabel(body, "숲 친구들과 인사를 나눠보세요.\n보낸 문장은 이 화면에서만 미리보기로 표시됩니다.", 18, Color.white, 76);
            else
            {
                var start = Mathf.Max(0, messagePreview.Count - 4);
                for (int i = start; i < messagePreview.Count; i++)
                    AddLabel(body, messagePreview[i], 17, Color.white, 34, TextAnchor.MiddleLeft);
            }
            messageInput = AddInput(body, "메시지 입력", 48);
            AddButton(body, "보내기 미리보기", () =>
            {
                var text = messageInput != null ? messageInput.text.Trim() : string.Empty;
                if (string.IsNullOrEmpty(text)) { SetNotice("메시지를 입력하세요."); return; }
                MessageSubmitted?.Invoke(text);
                messagePreview.Add("나 · " + text);
                var status = MessageSubmitted == null ? "네트워크 채팅 연결 전입니다. 메시지는 전송되지 않았습니다." : "메시지를 어댑터에 전달했습니다.";
                ShowMessages();
                SetNotice(status);
            }, 50);
            AddBackButton(body, ShowMain);
            FinishScreen(body);
        }

        private void ShowSettings()
        {
            var body = BeginScreen("설정", "클라이언트 로컬 음량 설정");
            AddSlider(body, "전체 음량", profileStore.Profile.masterVolume, value =>
            {
                profileStore.SetVolumes(value, profileStore.Profile.musicVolume, profileStore.Profile.effectsVolume);
                AudioListener.volume = value;
            });
            AddSlider(body, "배경 음악", profileStore.Profile.musicVolume, value =>
                profileStore.SetVolumes(profileStore.Profile.masterVolume, value, profileStore.Profile.effectsVolume));
            AddSlider(body, "효과음", profileStore.Profile.effectsVolume, value =>
                profileStore.SetVolumes(profileStore.Profile.masterVolume, profileStore.Profile.musicVolume, value));
            AddLabel(body, "화면·조작 옵션은 실제 게임 및 플랫폼 연동 범위에서 추가할 수 있습니다.", 16, Muted, 52);
            AddBackButton(body, ShowMain);
            FinishScreen(body);
        }

        private void ShowResults()
        {
            previewMatchId = Guid.NewGuid().ToString("N");
            var body = BeginScreen("결과 / 보상 미리보기", "샘플 순위로 보상 흐름을 확인합니다. 실제 경기 결과와 연결되지 않았습니다.");
            rankSelectionText = AddLabel(body, $"선택 순위 · {previewRank}위 · 보상 {economy.GetRewardForRank(previewRank):N0}잎", 18, Accent, 40);
            var ranks = AddRow(body, 46);
            var rankButtons = new List<Button>();
            for (int rank = 1; rank <= 4; rank++)
            {
                var selectedRank = rank;
                var rankButton = AddButton(ranks, $"{rank}위", () =>
                {
                    previewRank = selectedRank;
                    if (rankSelectionText != null)
                        rankSelectionText.text = $"선택 순위 · {previewRank}위 · 보상 {economy.GetRewardForRank(previewRank):N0}잎";
                    for (int i = 0; i < rankButtons.Count; i++)
                        rankButtons[i].targetGraphic.color = i + 1 == previewRank ? Accent : ButtonColor;
                }, 42, previewRank == rank ? Accent : ButtonColor);
                rankButtons.Add(rankButton);
            }
            AddLabel(body, "순위별 보상은 ClientMenuCatalog.rankRewards 에서 수정합니다.", 16, Muted, 44);
            AddButton(body, "보상 받기 (샘플)", () =>
            {
                int rank = previewRank;
                if (economy.TryClaimMatchReward(previewMatchId, rank, out var amount))
                    SetNotice($"{rank}위 샘플 보상 {amount:N0}잎을 받았습니다.");
                else
                    SetNotice("이 순위 샘플 보상은 이미 받았습니다. 재실행 시 프로필이 초기화됩니다.");
            }, 56, Accent);
            AddLabel(body, "승리 포즈 미리보기", 20, Accent, 34);
            var victoryPose = catalog.FindCosmetic(profileStore.Profile.equippedVictoryPoseId);
            AddPreviewCard(body, CurrentCharacter().displayName, CosmeticName(profileStore.Profile.equippedVictoryPoseId), CurrentCharacter().placeholderColor,
                "실제 캐릭터 모델과 애니메이션 에셋은 준비 후 연결합니다.", victoryPose != null ? victoryPose.previewSprite : null);
            AddButton(body, "캐릭터 꾸미기", ShowCharacter, 48);
            AddBackButton(body, ShowMain);
            AddFooter(body, "클라이언트 보상 저장은 시연용입니다. 실제 서비스에서는 서버가 결과와 재화를 검증해야 합니다.");
            FinishScreen(body);
        }

        private void ShowReference()
        {
            var body = BeginScreen("아이템 / 스킬 도감", "테이블 데이터는 UI 노출용입니다. effectDescription 문자열을 실행하지 않습니다.");
            AddLabel(body, "아이템", 20, Accent, 34);
            if (catalog.items != null)
            {
                for (int i = 0; i < catalog.items.Length; i++)
                {
                    var item = catalog.items[i];
                    if (item == null) continue;
                    var duration = item.activeDurationSeconds < 0 ? "미정" : item.activeDurationSeconds.ToString("0.##") + "초";
                    AddLabel(body, $"{item.displayName} · {item.itemType} · 지속 {duration}\n{item.effectDescription}", 15, Color.white, 58, TextAnchor.MiddleLeft);
                }
            }
            AddLabel(body, "스킬", 20, Accent, 34);
            if (catalog.skills != null)
            {
                for (int i = 0; i < catalog.skills.Length; i++)
                {
                    var skill = catalog.skills[i];
                    if (skill == null) continue;
                    var cooldown = skill.cooldownSeconds < 0 ? "미정" : skill.cooldownSeconds.ToString("0.##") + "초";
                    AddLabel(body, $"{skill.displayName} · {skill.character} · 재사용 {cooldown}\n{skill.effectDescription}", 15, Color.white, 56, TextAnchor.MiddleLeft);
                }
            }
            AddBackButton(body, ShowMain);
            FinishScreen(body);
        }

        private RectTransform BeginScreen(string title, string subtitle)
        {
            if (screenRoot != null) Destroy(screenRoot.gameObject);
            screenRoot = CreateRect(transform, "Screen " + title, Vector2.zero, true);
            var background = screenRoot.gameObject.AddComponent<Image>();
            background.color = Forest;

            var page = CreateRect(screenRoot, "Page", new Vector2(1160, 900), false);
            page.anchorMin = page.anchorMax = new Vector2(0.5f, 0.5f);
            page.anchoredPosition = Vector2.zero;
            page.sizeDelta = new Vector2(Mathf.Min(1160, Screen.width - 48), Mathf.Min(900, Screen.height - 48));
            var image = page.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            var column = page.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(34, 34, 25, 25);
            column.spacing = 8;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = false;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var header = AddRow(page, 54);
            AddLabel(header, title, 31, Color.white, 48, TextAnchor.MiddleLeft);
            currencyText = AddLabel(header, string.Empty, 20, Accent, 48, TextAnchor.MiddleRight);
            UpdateCurrencyLabel();
            AddLabel(page, subtitle, 16, Muted, 34);
            noticeText = AddLabel(page, string.Empty, 15, Accent, 30);
            return page;
        }

        private void FinishScreen(RectTransform body)
        {
            if (body != null && body.gameObject.GetComponent<VerticalLayoutGroup>() == null)
            {
                var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 8;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
        }

        private RectTransform CreateColumn(RectTransform parent, string name, Vector2 size, bool flexible)
        {
            var rect = CreateRect(parent, name, size, false);
            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = flexible ? 1 : 0;
            layoutElement.minWidth = flexible ? 100 : size.x;
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private RectTransform AddRow(RectTransform parent, float height)
        {
            var row = CreateRect(parent, "Row", new Vector2(0, height), false);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private Text AddLabel(RectTransform parent, string value, int size, Color color, float height, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(parent, "Text " + value, new Vector2(0, height), false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button AddButton(RectTransform parent, string label, Action action, float height, Color? color = null)
        {
            var rect = CreateRect(parent, "Button " + label, new Vector2(0, height), false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color ?? ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            var textRect = CreateRect(rect, "Label", Vector2.zero, true);
            textRect.offsetMin = new Vector2(12, 4);
            textRect.offsetMax = new Vector2(-12, -4);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = label;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.06f, 0.10f, 0.08f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return button;
        }

        private InputField AddInput(RectTransform parent, string placeholder, float height)
        {
            var rect = CreateRect(parent, "Input " + placeholder, new Vector2(0, height), false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.92f, 0.95f, 0.90f, 1f);
            var input = rect.gameObject.AddComponent<InputField>();
            var textRect = CreateRect(rect, "Text", Vector2.zero, true);
            textRect.offsetMin = new Vector2(12, 4);
            textRect.offsetMax = new Vector2(-12, -4);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.black;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text;
            var placeholderRect = CreateRect(rect, "Placeholder", Vector2.zero, true);
            placeholderRect.offsetMin = new Vector2(12, 4);
            placeholderRect.offsetMax = new Vector2(-12, -4);
            var placeholderText = placeholderRect.gameObject.AddComponent<Text>();
            placeholderText.font = text.font;
            placeholderText.text = placeholder;
            placeholderText.fontSize = 16;
            placeholderText.color = new Color(0.35f, 0.40f, 0.36f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.raycastTarget = false;
            input.placeholder = placeholderText;
            input.targetGraphic = image;
            return input;
        }

        private void AddSlider(RectTransform parent, string label, float initialValue, Action<float> onValueChanged)
        {
            AddLabel(parent, $"{label} · {(int)(initialValue * 100)}%", 18, Color.white, 30, TextAnchor.MiddleLeft);
            var rect = CreateRect(parent, "Slider " + label, new Vector2(0, 34), false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 34;
            element.preferredHeight = 34;
            element.flexibleWidth = 1;
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = initialValue;
            var background = CreateRect(rect, "Background", Vector2.zero, true);
            background.offsetMin = new Vector2(0, 12);
            background.offsetMax = new Vector2(0, -12);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.14f, 0.27f, 0.22f, 1f);
            slider.targetGraphic = backgroundImage;
            var fillArea = CreateRect(rect, "Fill Area", Vector2.zero, true);
            fillArea.offsetMin = new Vector2(5, 12);
            fillArea.offsetMax = new Vector2(-5, -12);
            var fill = CreateRect(fillArea, "Fill", Vector2.zero, true);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Accent;
            slider.fillRect = fill;
            var handleArea = CreateRect(rect, "Handle Slide Area", Vector2.zero, true);
            handleArea.offsetMin = new Vector2(5, 0);
            handleArea.offsetMax = new Vector2(-5, 0);
            var handle = CreateRect(handleArea, "Handle", new Vector2(24, 24), false);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(value => onValueChanged?.Invoke(value));
        }

        private RectTransform CreateScrollList(RectTransform parent)
        {
            var viewportRect = CreateRect(parent, "Catalog Scroll", new Vector2(0, 440), false);
            var element = viewportRect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 250;
            element.flexibleHeight = 1;
            element.flexibleWidth = 1;
            var viewportImage = viewportRect.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.06f, 0.14f, 0.12f, 0.45f);
            var viewport = viewportRect.gameObject.AddComponent<Mask>();
            viewport.showMaskGraphic = false;
            var scroll = viewportRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var content = CreateRect(viewportRect, "Content", new Vector2(0, 0), false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = content;
            return content;
        }

        private void AddPreviewCard(RectTransform parent, string title, string subtitle, Color color, string detail, Sprite previewSprite = null)
        {
            var row = AddRow(parent, 140);
            var swatchRect = CreateRect(row, "Character Placeholder", new Vector2(116, 116), false);
            var swatch = swatchRect.gameObject.AddComponent<Image>();
            swatch.sprite = previewSprite;
            swatch.preserveAspect = true;
            swatch.color = previewSprite != null ? Color.white : color;
            var details = CreateColumn(row, "Character Preview", Vector2.zero, true);
            AddLabel(details, title, 22, Color.white, 38, TextAnchor.MiddleLeft);
            AddLabel(details, subtitle, 15, Muted, 30, TextAnchor.MiddleLeft);
            AddLabel(details, detail, 15, Accent, 58, TextAnchor.MiddleLeft);
        }

        private void AddImage(RectTransform parent, Sprite sprite, Vector2 size)
        {
            var rect = CreateRect(parent, "Catalog Icon", size, false);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
        }

        private void AddFooter(RectTransform parent, string text)
        {
            AddLabel(parent, text, 13, Muted, 35);
        }

        private void AddBackButton(RectTransform parent, Action action)
        {
            AddButton(parent, "메인 메뉴", action, 44, ButtonColor);
        }

        private void SetNotice(string text)
        {
            if (noticeText != null) noticeText.text = text;
        }

        private void UpdateCurrencyLabel()
        {
            if (currencyText != null && profileStore != null)
                currencyText.text = $"{profileStore.Profile.softCurrency:N0} 잎";
        }

        private ClientCharacterDefinition CurrentCharacter()
        {
            var id = (ClientCharacterId)profileStore.Profile.selectedCharacter;
            if (catalog == null) return new ClientCharacterDefinition { displayName = "숲 친구" };
            return catalog.FindCharacter(id) ?? (catalog.characters != null && catalog.characters.Length > 0
                ? catalog.characters[0]
                : new ClientCharacterDefinition { displayName = "숲 친구" });
        }

        private string CosmeticName(string cosmeticId)
        {
            var cosmetic = catalog.FindCosmetic(cosmeticId);
            return cosmetic != null ? cosmetic.displayName : "기본";
        }

        private bool IsEquipped(ClientCosmeticDefinition item)
        {
            var profile = profileStore.Profile;
            switch (item.kind)
            {
                case ClientCosmeticKind.Skin: return profile.equippedSkinId == item.id;
                case ClientCosmeticKind.Voice: return profile.equippedVoiceId == item.id;
                case ClientCosmeticKind.VictoryPose: return profile.equippedVictoryPoseId == item.id;
                default: return false;
            }
        }

        private static string CategoryName(ClientCosmeticKind kind)
        {
            switch (kind)
            {
                case ClientCosmeticKind.Skin: return "스킨";
                case ClientCosmeticKind.Voice: return "사운드";
                case ClientCosmeticKind.VictoryPose: return "승리 포즈";
                default: return "상품";
            }
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 size, bool stretch)
        {
            var child = new GameObject(string.IsNullOrEmpty(name) ? "UI Element" : name, typeof(RectTransform));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = size;
            }
            return rect;
        }
    }
}
