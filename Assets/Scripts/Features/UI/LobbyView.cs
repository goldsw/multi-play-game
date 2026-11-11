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
        private string joinCode = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private GUIStyle inputStyle;

        public void Show(SessionData sessionState)
        {
            state = sessionState;
        }

        private void OnGUI()
        {
            if (state == null)
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
                bool playing = state.Status == SessionStatus.Playing;
                Rect panel = playing ? new Rect(24, 24, 420, 230) : new Rect(390, 100, 500, 520);
                GUILayout.BeginArea(panel, GUI.skin.box);
                GUILayout.Space(12);
                GUILayout.Label(playing ? "MATCH STARTED" : "MUSHROOM SURVIVAL", titleStyle);
                GUILayout.Label("Players: " + state.PlayerCount + " / " + LobbySettings.PlayerCount, textStyle);
                GUILayout.Label("Status: " + state.Status, textStyle);

                if (!string.IsNullOrEmpty(state.Message))
                    GUILayout.Label(state.Message, textStyle);

                bool busy = state.Status == SessionStatus.Connecting ||
                    state.Status == SessionStatus.Starting || state.Status == SessionStatus.Leaving;
                GUI.enabled = previousEnabled && !busy;
                if (playing)
                {
                    if (GUILayout.Button("Leave match", buttonStyle))
                        LeaveRequested?.Invoke();
                }
                else
                {
                    DrawLobby();
                }

                GUILayout.EndArea();
            }
            finally
            {
                GUI.enabled = previousEnabled;
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawLobby()
        {
            GUILayout.Space(12);
            if (state.Status == SessionStatus.Lobby || state.Status == SessionStatus.Starting)
            {
                GUILayout.Label("Join code: " + state.JoinCode, titleStyle);
                GUILayout.Label(state.LocalIsHost ? "You are the host." : "Waiting for the host.", textStyle);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && state.LocalIsHost &&
                    state.Status == SessionStatus.Lobby && state.PlayerCount >= LobbySettings.MinimumPlayersToStart;
                var startLabel = LobbySettings.MinimumPlayersToStart == 1
                    ? "Start match (solo test enabled)"
                    : $"Start match ({LobbySettings.MinimumPlayersToStart} players required)";
                if (GUILayout.Button(startLabel, buttonStyle))
                    StartRequested?.Invoke();
                GUI.enabled = previousEnabled;
                if (GUILayout.Button("Leave lobby", buttonStyle))
                    LeaveRequested?.Invoke();
                return;
            }

            if (GUILayout.Button("Create lobby", buttonStyle))
                HostRequested?.Invoke();
            GUILayout.Space(16);
            GUILayout.Label("Enter a Relay join code", textStyle);
            joinCode = GUILayout.TextField(joinCode, 32, inputStyle);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !string.IsNullOrWhiteSpace(joinCode);
            if (GUILayout.Button("Join lobby", buttonStyle))
                JoinRequested?.Invoke(joinCode);
            GUI.enabled = enabled;
            GUILayout.Space(12);
            GUILayout.Label($"Share the lobby code with up to {LobbySettings.PlayerCount - 1} players.", textStyle);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 19, fixedHeight = 46 };
            inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 22, fixedHeight = 42 };
        }
    }
}
