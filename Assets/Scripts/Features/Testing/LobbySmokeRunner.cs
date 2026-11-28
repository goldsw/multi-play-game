#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    public sealed class LobbySmokeRunner : MonoBehaviour
    {
        private SessionManager session;
        private NetworkManager network;
        private bool host;
        private string joinCode;
        private int expectedPlayers;
        private bool succeeded;
        private bool verifyingReturn;
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

        public void Initialize(
            SessionManager manager, NetworkManager networkManager, bool isHost, string joinCode)
        {
            session = manager;
            network = networkManager;
            host = isHost;
            this.joinCode = joinCode;
            expectedPlayers = Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-solo") >= 0
                ? 1 : LobbySettings.PlayerCount;
        }

        private async void Start()
        {
            deadline = Time.realtimeSinceStartup + 90f;
            try
            {
                if (host)
                {
                    await session.HostAsync();
                    if (session.State.Status == SessionStatus.Lobby)
                        Debug.Log("MUSHROOM_JOIN_CODE " + session.State.JoinCode);
                }
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-room-switch") >= 0)
                    await VerifyRoomSwitchAsync();
                else
                    await session.JoinAsync(joinCode);
            }
            catch (Exception exception)
            {
                Debug.LogError("MUSHROOM_SMOKE_FAILED " + exception.Message);
                Application.Quit(1);
            }
        }

        private async Task VerifyRoomSwitchAsync()
        {
            await session.HostAsync();
            if (await WaitForOwnSlotAsync(true) != 0)
                throw new InvalidOperationException("The owner must occupy slot 0 in their own room.");

            await session.JoinAsync(joinCode);
            int firstSlot = await WaitForOwnSlotAsync(false);
            await session.LeaveAsync();
            if (await WaitForOwnSlotAsync(true) != 0)
                throw new InvalidOperationException("Leaving must open a new own room.");

            await session.JoinAsync(joinCode);
            if (await WaitForOwnSlotAsync(false) != firstSlot)
                throw new InvalidOperationException("The freed slot was not reused.");
            Debug.Log("MUSHROOM_ROOM_SWITCH_PASSED own room, join, leave, and reuse");
        }

        private async Task<int> WaitForOwnSlotAsync(bool expectHost)
        {
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until && session.State.Status != SessionStatus.Failed)
            {
                var state = session.State;
                if (state.Status == SessionStatus.Lobby && state.LocalIsHost == expectHost)
                {
                    for (int index = 0; index < state.Slots.Count; index++)
                    {
                        var slot = state.Slots[index];
                        if (slot.Occupied && slot.ClientId == network.LocalClientId)
                            return index;
                    }
                }
                await Task.Yield();
            }
            throw new InvalidOperationException("No confirmed slot for the local player. " + session.State.Message);
        }

        private void Update()
        {
            if (session == null)
                return;
            if (!succeeded && host && session.State.Status == SessionStatus.Lobby &&
                session.State.PlayerCount == expectedPlayers)
                session.StartMatch();

            if (!succeeded && session.State.Status == SessionStatus.Playing &&
                session.State.PlayerCount == expectedPlayers &&
                network.LocalClient.PlayerObject != null &&
                network.LocalClient.PlayerObject.gameObject.scene.name == InGameSceneLoader.SceneName &&
                network.SpawnManager.SpawnedObjectsList.Count >= expectedPlayers + 1)
            {
                succeeded = true;
                quitAt = Time.realtimeSinceStartup + 8f;
                Debug.Log($"MUSHROOM_SMOKE_PASSED players={expectedPlayers}; scene=InGame; phase=Playing; owner={network.LocalClientId}");
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-move") >= 0)
                    gameObject.AddComponent<MovementSmokeProbe>().Initialize(network);
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-spectator-cycle") >= 0)
                {
                    quitAt = Time.realtimeSinceStartup + 40f;
                    gameObject.AddComponent<SpectatorCycleSmokeProbe>().Initialize(network);
                }
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-end-match") >= 0)
                {
                    gameObject.AddComponent<WinnerFocusSmokeProbe>().Initialize(network);
                    verifyingReturn = true;
                    quitAt = Time.realtimeSinceStartup + 30f;
                    _ = VerifyResultAsync();
                }
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-ingame-return") >= 0)
                {
                    verifyingReturn = true;
                    quitAt = Time.realtimeSinceStartup + 30f;
                    _ = VerifyInGameReturnAsync();
                }
            }
            if (succeeded && Time.realtimeSinceStartup >= quitAt)
            {
                if (verifyingReturn)
                    Debug.LogError("MUSHROOM_SMOKE_FAILED InGame return timed out.");
                Application.Quit(verifyingReturn ? 1 : 0);
            }
            else if (!succeeded && Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogError("MUSHROOM_SMOKE_FAILED phase=" + session.State.Status +
                    " count=" + session.State.PlayerCount + " message=" + session.State.Message);
                Application.Quit(1);
            }
        }

        private async Task VerifyResultAsync()
        {
            try
            {
                if (network.IsServer)
                {
                    await Task.Delay(1000);
                    if (!FindFirstObjectByType<MatchPlayerStateManager>().EndMatchByExplosion())
                        throw new InvalidOperationException("EndMatchByExplosion was rejected.");
                }
                var store = FindFirstObjectByType<MatchResultStore>();
                float until = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < until)
                {
                    var result = SceneManager.GetSceneByName(ResultSceneLoader.SceneName);
                    if (result.isLoaded && !SceneManager.GetSceneByName(InGameSceneLoader.SceneName).isLoaded &&
                        store.IsReady && store.Entries.Count == expectedPlayers)
                    {
                        Debug.Log($"MUSHROOM_RESULT_PASSED peer={network.LocalClientId}; status={session.State.Status}; " +
                            "entries=" + string.Join(",", System.Linq.Enumerable.Select(store.Entries,
                                entry => $"{entry.ClientId}/{entry.CharacterId}/{entry.Rank}")));
                        await Task.Delay(1000); // let WinnerFocusSmokeProbe report first
                        if (network.IsServer)
                            await Task.Delay(6000); // keep the host up until clients have verified too
                        Application.Quit(0);
                        return;
                    }
                    await Task.Yield();
                }
                throw new InvalidOperationException("Result scene or data did not arrive.");
            }
            catch (Exception exception)
            {
                Debug.LogError("MUSHROOM_SMOKE_FAILED " + exception.Message);
                Application.Quit(1);
            }
        }

        private async Task VerifyInGameReturnAsync()
        {
            try
            {
                await session.LeaveAsync();
                if (session.State.Status != SessionStatus.Lobby || !session.State.LocalIsHost ||
                    SceneManager.GetSceneByName(InGameSceneLoader.SceneName).isLoaded ||
                    network.LocalClient.PlayerObject != null)
                    throw new InvalidOperationException("Returning to the lobby did not unload the match.");
                Debug.Log("MUSHROOM_INGAME_RETURN_PASSED scene unloaded; own room restored");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("MUSHROOM_SMOKE_FAILED " + exception.Message);
                Application.Quit(1);
            }
        }
    }
}
#endif
