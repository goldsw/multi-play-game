#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbySmokeRunner : MonoBehaviour
    {
        private SessionManager session;
        private NetworkManager network;
        private bool host;
        private bool succeeded;
        private float deadline;
        private float quitAt;

        public static bool TryGetRole(out bool isHost, out bool useRelay, out string joinCode)
        {
            var arguments = Environment.GetCommandLineArgs();
            var relayClient = Array.IndexOf(arguments, "--smoke-relay-client");
            useRelay = relayClient >= 0 || Array.IndexOf(arguments, "--smoke-relay-host") >= 0;
            isHost = Array.IndexOf(arguments, "--smoke-host") >= 0 ||
                Array.IndexOf(arguments, "--smoke-relay-host") >= 0;
            joinCode = relayClient >= 0 && relayClient + 1 < arguments.Length
                ? arguments[relayClient + 1] : "LOCAL";
            return isHost || useRelay || Array.IndexOf(arguments, "--smoke-client") >= 0;
        }

        public async void Initialize(
            SessionManager manager, NetworkManager networkManager, bool isHost, string joinCode)
        {
            session = manager;
            network = networkManager;
            host = isHost;
            deadline = Time.realtimeSinceStartup + 90f;
            try
            {
                if (host)
                {
                    await session.HostAsync();
                    if (session.State.Status == SessionStatus.Lobby)
                        Debug.Log("MUSHROOM_JOIN_CODE " + session.State.JoinCode);
                }
                else
                    await session.JoinAsync(joinCode);
            }
            catch (Exception exception)
            {
                Debug.LogError("MUSHROOM_SMOKE_FAILED " + exception.Message);
                Application.Quit(1);
            }
        }

        private void Update()
        {
            if (session == null)
                return;
            if (host && session.State.Status == SessionStatus.Lobby &&
                session.State.PlayerCount == LobbySettings.PlayerCount)
                session.StartMatch();

            if (!succeeded && session.State.Status == SessionStatus.Playing &&
                network.LocalClient.PlayerObject != null &&
                network.SpawnManager.SpawnedObjectsList.Count >= LobbySettings.PlayerCount + 1)
            {
                succeeded = true;
                quitAt = Time.realtimeSinceStartup + 8f;
                Debug.Log("MUSHROOM_SMOKE_PASSED Four cube players spawned; phase=Playing; owner=" +
                    network.LocalClientId);
            }
            if (succeeded && Time.realtimeSinceStartup >= quitAt)
                Application.Quit(0);
            else if (!succeeded && Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogError("MUSHROOM_SMOKE_FAILED phase=" + session.State.Status +
                    " count=" + session.State.PlayerCount + " message=" + session.State.Message);
                Application.Quit(1);
            }
        }
    }
}
#endif
