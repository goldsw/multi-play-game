using System;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyView : MonoBehaviour
    {
        public event Action HostRequested;
        public event Action<string> JoinRequested;
        public event Action LeaveRequested;
        public event Action StartRequested;

        private SessionData state;
        private LobbyNetworkState networkState;
        private bool joinPopupOpen;
        private bool lastLobbyWasHost = true;
        private string joinCode = string.Empty;
        private Font koreanFont;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private GUIStyle inputStyle;
        private static readonly string[] CharacterNames = { "고양이", "오리", "강아지", "토끼", "다람쥐" };

        public void ConfigureCharacterSelection(LobbyNetworkState lobby) => networkState = lobby;

        public void Show(SessionData sessionState)
        {
            state = sessionState;
            if (state.Status == SessionStatus.Lobby)
                lastLobbyWasHost = state.LocalIsHost;
            if (state.Status != SessionStatus.Lobby || !state.LocalIsHost)
                joinPopupOpen = false;
        }

        private void OnGUI()
        {
            if (state == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - 1280f * scale) / 2f,
                    (Screen.height - 720f * scale) / 2f, 0f),
                Quaternion.identity, Vector3.one * scale);

            try
            {
                DrawStatus();
                DrawRoomControls();
                if (state.Status == SessionStatus.Lobby && !joinPopupOpen && networkState != null && networkState.IsSpawned)
                    DrawCharacterSelection();
                if (joinPopupOpen)
                    DrawJoinPopup();
            }
            finally
            {
                GUI.enabled = previousEnabled;
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawCharacterSelection()
        {
            int selected = networkState.LocalCharacterId;
            int choice = GUI.SelectionGrid(new Rect(380, 651, 520, 46), selected, CharacterNames, 5, buttonStyle);
            if (choice != selected)
                networkState.SelectCharacter(choice);
        }

        private void DrawStatus()
        {
            GUI.Box(new Rect(20, 20, 420, 92), GUIContent.none);
            string heading = state.Status switch
            {
                SessionStatus.Lobby when !string.IsNullOrEmpty(state.JoinCode) => "방 코드  " + state.JoinCode,
                SessionStatus.LoadingInGame => "경기장 로딩 중",
                SessionStatus.Starting => "플레이어 준비 중",
                SessionStatus.Playing => MatchHeading(),
                _ => state.Status.ToString()
            };

            GUI.Label(new Rect(36, 29, 388, 40), heading, titleStyle);
            GUI.Label(new Rect(36, 68, 388, 35),
                "인원  " + state.PlayerCount + " / " + LobbySettings.PlayerCount, textStyle);

            if (!string.IsNullOrEmpty(state.Message))
            {
                GUI.Box(new Rect(20, 124, 420, 82), GUIContent.none);
                GUI.Label(new Rect(36, 132, 388, 66), state.Message, textStyle);
            }
        }

        private string MatchHeading()
        {
            var player = networkState != null ? networkState.NetworkManager.LocalClient?.PlayerObject : null;
            if (player == null || !player.TryGetComponent<SkillCaster>(out var caster))
                return "경기 중";
            return caster.IsTagger ? "경기 중 · 술래" : "경기 중 · 도망자";
        }

        private void DrawRoomControls()
        {
            Rect roomButton = new Rect(20, 650, 172, 50);
            if (state.Status == SessionStatus.Offline || state.Status == SessionStatus.Failed)
            {
                if (GUI.Button(roomButton, "다시 시도", buttonStyle))
                    HostRequested?.Invoke();
                return;
            }

            bool previousEnabled = GUI.enabled;
            bool inLobby = state.Status == SessionStatus.Lobby;
            bool playing = state.Status == SessionStatus.Playing;
            bool canUseRoom = inLobby || playing;
            GUI.enabled = previousEnabled && canUseRoom && !joinPopupOpen;
            if (GUI.Button(roomButton, lastLobbyWasHost && !playing ? "방 찾기" : "방 나가기", buttonStyle))
            {
                if (lastLobbyWasHost && !playing)
                {
                    joinCode = string.Empty;
                    joinPopupOpen = true;
                }
                else
                {
                    LeaveRequested?.Invoke();
                }
            }

            if (lastLobbyWasHost && !playing)
            {
                GUI.enabled = previousEnabled && inLobby && !joinPopupOpen &&
                    state.PlayerCount >= LobbySettings.MinimumPlayersToStart;
                if (GUI.Button(new Rect(1088, 650, 172, 50), "게임 시작", buttonStyle))
                    StartRequested?.Invoke();
            }

            GUI.enabled = previousEnabled;
        }

        private void DrawJoinPopup()
        {
            GUI.Box(new Rect(430, 260, 420, 200), GUIContent.none);
            GUI.Label(new Rect(454, 277, 372, 40), "참가할 방 코드", titleStyle);
            joinCode = GUI.TextField(new Rect(454, 325, 372, 42), joinCode, 32, inputStyle);

            if (GUI.Button(new Rect(454, 390, 178, 45), "취소", buttonStyle))
                joinPopupOpen = false;

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !string.IsNullOrWhiteSpace(joinCode);
            if (GUI.Button(new Rect(648, 390, 178, 45), "참가", buttonStyle))
            {
                joinPopupOpen = false;
                JoinRequested?.Invoke(joinCode);
            }
            GUI.enabled = previousEnabled;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            koreanFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 22);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 22 };
            if (koreanFont != null)
            {
                titleStyle.font = koreanFont;
                textStyle.font = koreanFont;
                buttonStyle.font = koreanFont;
                inputStyle.font = koreanFont;
            }
        }

        private void OnDestroy()
        {
            if (koreanFont != null)
                Destroy(koreanFont);
        }
    }
}
