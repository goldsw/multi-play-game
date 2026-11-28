using System;
using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Text heading;
        [SerializeField] private Text playerCount;
        [SerializeField] private Text message;
        [SerializeField] private Button roomButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button[] characterButtons;
        [SerializeField] private GameObject joinPopup;
        [SerializeField] private InputField joinCode;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button joinButton;

        public event Action HostRequested;
        public event Action<string> JoinRequested;
        public event Action LeaveRequested;
        public event Action StartRequested;

        private SessionData state;
        private SessionNetworkState networkState;
        private Text roomLabel;
        private Font koreanFont;
        private SkillCaster localCaster;
        private bool lastLobbyWasHost = true;

        public void ConfigureCanvas(Canvas lobbyCanvas, Text statusHeading, Text count, Text sessionMessage,
            Button room, Button start, Button[] characters, GameObject popup, InputField code, Button cancel, Button join)
        {
            canvas = lobbyCanvas;
            heading = statusHeading;
            playerCount = count;
            message = sessionMessage;
            roomButton = room;
            startButton = start;
            characterButtons = characters;
            joinPopup = popup;
            joinCode = code;
            cancelButton = cancel;
            joinButton = join;
        }

        private void Awake()
        {
            roomLabel = roomButton.GetComponentInChildren<Text>();
            koreanFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 22);
            if (koreanFont != null)
            {
                foreach (var label in canvas.GetComponentsInChildren<Text>(includeInactive: true))
                    label.font = koreanFont;
            }

            roomButton.onClick.AddListener(UseRoomButton);
            startButton.onClick.AddListener(() => StartRequested?.Invoke());
            cancelButton.onClick.AddListener(CloseJoinPopup);
            joinButton.onClick.AddListener(JoinRoom);
            joinCode.onValueChanged.AddListener(UpdateJoinButton);
            for (int index = 0; index < characterButtons.Length; index++)
            {
                int character = index;
                characterButtons[index].onClick.AddListener(() => networkState?.SelectCharacter(character));
            }
            if (state != null)
                Render();
        }

        public void ConfigureCharacterSelection(SessionNetworkState lobby)
        {
            networkState = lobby;
            if (roomLabel != null && state != null)
                Render();
        }

        public void Show(SessionData sessionState)
        {
            state = sessionState;
            // The result scene has its own presentation; the lobby UI returns with the Lobby state.
            if (canvas != null)
                canvas.enabled = state.Status != SessionStatus.Result;
            if (state.Status == SessionStatus.Lobby)
                lastLobbyWasHost = state.LocalIsHost;
            if (state.Status != SessionStatus.Playing)
                localCaster = null;
            if (state.Status != SessionStatus.Lobby || !state.LocalIsHost)
                joinPopup.SetActive(false);
            if (roomLabel != null)
                Render();
        }

        private void Render()
        {
            heading.text = state.Status switch
            {
                SessionStatus.Lobby when !string.IsNullOrEmpty(state.JoinCode) => "방 코드  " + state.JoinCode,
                SessionStatus.LoadingInGame => "경기장 로딩 중",
                SessionStatus.Starting => "플레이어 준비 중",
                SessionStatus.Playing => MatchHeading(),
                _ => state.Status.ToString()
            };
            playerCount.text = $"인원  {state.PlayerCount} / {LobbySettings.PlayerCount}";
            message.text = state.Message;
            message.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(state.Message));

            bool inLobby = state.Status == SessionStatus.Lobby;
            bool playing = state.Status == SessionStatus.Playing;
            bool retry = state.Status is SessionStatus.Offline or SessionStatus.Failed;
            bool findRoom = lastLobbyWasHost && !playing;
            bool popupOpen = joinPopup.activeSelf;
            roomLabel.text = retry ? "다시 시도" : findRoom ? "방 찾기" : "방 나가기";
            roomButton.interactable = (retry || inLobby || playing) && !popupOpen;
            startButton.gameObject.SetActive(findRoom && !retry);
            startButton.interactable = inLobby && !popupOpen && state.PlayerCount >= LobbySettings.MinimumPlayersToStart;

            bool canSelect = inLobby && !popupOpen && networkState != null && networkState.IsSpawned;
            int selected = canSelect ? networkState.LocalCharacterId : -1;
            for (int index = 0; index < characterButtons.Length; index++)
            {
                var button = characterButtons[index];
                button.gameObject.SetActive(canSelect);
                button.interactable = index != selected;
            }
        }

        private void UseRoomButton()
        {
            if (state.Status is SessionStatus.Offline or SessionStatus.Failed)
            {
                HostRequested?.Invoke();
            }
            else if (state.Status == SessionStatus.Lobby && state.LocalIsHost)
            {
                joinPopup.SetActive(true);
                joinCode.SetTextWithoutNotify(string.Empty);
                UpdateJoinButton(joinCode.text);
                Render();
                joinCode.ActivateInputField();
            }
            else
            {
                LeaveRequested?.Invoke();
            }
        }

        private void UpdateJoinButton(string code) => joinButton.interactable = !string.IsNullOrWhiteSpace(code);

        private void CloseJoinPopup()
        {
            joinPopup.SetActive(false);
            Render();
        }

        private void JoinRoom()
        {
            string code = joinCode.text.Trim();
            if (code.Length == 0)
                return;

            CloseJoinPopup();
            JoinRequested?.Invoke(code);
        }

        private void LateUpdate()
        {
            if (state?.Status != SessionStatus.Playing)
                return;

            string current = MatchHeading();
            if (heading.text != current)
                heading.text = current;
        }

        private string MatchHeading()
        {
            if (localCaster == null && networkState != null && networkState.IsSpawned)
            {
                var player = networkState.NetworkManager.LocalClient?.PlayerObject;
                if (player != null)
                    localCaster = player.GetComponent<SkillCaster>();
            }
            if (localCaster == null)
                return "경기 중";
            return localCaster.IsTagger ? "경기 중 · 술래" : "경기 중 · 도망자";
        }

        private void OnDestroy()
        {
            if (koreanFont != null)
                Destroy(koreanFont);
        }
    }
}
