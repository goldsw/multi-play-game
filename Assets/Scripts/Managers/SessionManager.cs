using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class SessionManager : MonoBehaviour
    {
        private NetworkManager networkManager;
        private LobbyNetworkState networkState;
        private NetworkPlayerSpawner playerSpawner;
        private ISessionConnector connector;
        private SlotManager slotManager;
        private bool busy;
        private bool destroyed;
        private string joinCode = "";
        private ulong[] matchParticipants = Array.Empty<ulong>();
        private float startDeadline;

        public SessionData State { get; private set; } = new(SessionStatus.Offline);
        public event Action<SessionData> StateChanged;

        public void Initialize(
            NetworkManager network,
            LobbyNetworkState state,
            NetworkPlayerSpawner spawner,
            ISessionConnector sessionConnector,
            SlotManager slots)
        {
            networkManager = network;
            networkState = state;
            playerSpawner = spawner;
            connector = sessionConnector;
            slotManager = slots;
            networkState.Changed += RefreshState;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnTransportFailure += OnTransportFailure;
        }

        public Task HostAsync() => networkManager.IsListening ? Task.CompletedTask : ConnectAsync(true, "");

        public Task JoinAsync(string code)
        {
            if (busy)
                return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetState(State.Status, "Enter a join code.");
                return Task.CompletedTask;
            }
            var targetCode = code.Trim().ToUpperInvariant();
            if (targetCode == joinCode)
            {
                SetState(State.Status, "You are already in this room.");
                return Task.CompletedTask;
            }
            return ConnectAsync(false, targetCode);
        }

        private async Task ConnectAsync(bool host, string code, string notice = "")
        {
            if (busy || destroyed)
                return;
            busy = true;
            SetState(SessionStatus.Connecting, "Connecting to Relay...");
            try
            {
                await CleanUpAsync();
                await OpenRoomAsync(host, code, notice);
            }
            catch (Exception exception)
            {
                await CleanUpAsync();
                if (host)
                    SetState(SessionStatus.Failed, exception.Message);
                else
                    await RestoreOwnRoomAsync(exception.Message);
            }
            finally
            {
                busy = false;
            }
        }

        private async Task OpenRoomAsync(bool host, string code, string notice)
        {
            if (destroyed)
                return;
            if (host)
                joinCode = await connector.CreateAsync();
            else
            {
                await connector.JoinAsync(code);
                joinCode = code;
            }
            if (destroyed)
            {
                await CleanUpAsync();
                return;
            }
            if (!networkManager.IsListening || !networkManager.IsConnectedClient)
                throw new InvalidOperationException("The session did not start networking.");
            SetState(networkState.IsSpawned ? networkState.Status : SessionStatus.Connecting, notice);
        }

        private async Task RestoreOwnRoomAsync(string joinError)
        {
            try
            {
                await OpenRoomAsync(true, "", joinError);
            }
            catch (Exception exception)
            {
                await CleanUpAsync();
                SetState(SessionStatus.Failed, joinError + "\n" + exception.Message);
            }
        }

        public Task LeaveAsync() => ConnectAsync(true, "");

        public void StartMatch()
        {
            if (busy || !networkManager.IsHost || !networkState.IsSpawned ||
                networkState.Status != SessionStatus.Lobby ||
                networkState.PlayerCount < LobbySettings.MinimumPlayersToStart)
                return;
            matchParticipants = networkState.GetSlots().Where(slot => slot.Occupied)
                .Select(slot => slot.ClientId).ToArray();
            networkState.PublishStatus(SessionStatus.Starting);
            startDeadline = Time.realtimeSinceStartup + 15f;
            try
            {
                playerSpawner.SpawnPlayers(networkManager, matchParticipants);
            }
            catch (Exception exception)
            {
                FailSession(exception.Message);
            }
        }

        private void Update()
        {
            if (networkManager == null || !networkManager.IsHost || !networkState.IsSpawned ||
                networkState.Status != SessionStatus.Starting)
                return;
            if (playerSpawner.AllPlayersReady(networkManager, matchParticipants))
                networkState.PublishStatus(SessionStatus.Playing);
            else if (Time.realtimeSinceStartup >= startDeadline)
                FailSession("Player spawning timed out.");
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager.IsHost)
            {
                if (!networkState.IsSpawned || networkState.Status == SessionStatus.Lobby)
                {
                    if (!slotManager.TryReserveSlot(clientId, out int index))
                    {
                        networkManager.DisconnectClient(clientId);
                        return;
                    }
                    var slot = slotManager.InitSlot(index, clientId, LobbySettings.DefaultCharacterId);
                    if (networkState.IsSpawned)
                        networkState.PublishSlot(index, slot);
                }
                else if (Array.IndexOf(matchParticipants, clientId) < 0)
                    networkManager.DisconnectClient(clientId);
            }
            RefreshState();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (destroyed)
                return;
            if (!networkManager.IsHost || clientId == networkManager.LocalClientId)
            {
                FailSession("Disconnected from the host.");
                return;
            }
            int index = slotManager.ReleaseSlot(clientId);
            if (networkState.Status == SessionStatus.Starting || networkState.Status == SessionStatus.Playing)
            {
                if (Array.IndexOf(matchParticipants, clientId) >= 0)
                    FailSession("A player left. The session has ended.");
            }
            else if (networkState.IsSpawned && index >= 0)
                networkState.PublishSlot(index, default);
        }

        private void OnTransportFailure() => FailSession("The network connection failed.");

        private async void FailSession(string message) => await ConnectAsync(true, "", message);

        private async Task CleanUpAsync()
        {
            try
            {
                await connector.LeaveAsync();
            }
            catch (Exception exception)
            {
                if (!destroyed)
                    Debug.LogWarning("Session cleanup: " + exception.Message);
            }
            finally
            {
                if (networkManager != null && networkManager.IsListening)
                    networkManager.Shutdown();
                while (!destroyed && networkManager != null && networkManager.ShutdownInProgress)
                    await Task.Yield();
                joinCode = "";
                matchParticipants = Array.Empty<ulong>();
                slotManager.Clear();
            }
        }

        private void RefreshState()
        {
            if (destroyed || busy || !networkManager.IsListening)
                return;
            var phase = networkState.IsSpawned ? networkState.Status : SessionStatus.Connecting;
            SetState(phase);
        }

        private void SetState(SessionStatus status, string message = "")
        {
            if (destroyed)
                return;
            bool hasLobby = networkState != null && networkState.IsSpawned;
            State = new SessionData(status, joinCode, hasLobby ? networkState.PlayerCount : 0,
                networkManager != null && networkManager.IsHost, message,
                hasLobby ? networkState.GetSlots() : null);
            StateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            destroyed = true;
            if (networkState != null)
                networkState.Changed -= RefreshState;
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                networkManager.OnTransportFailure -= OnTransportFailure;
                if (networkManager.IsListening)
                    networkManager.Shutdown();
            }
            if (connector != null)
                _ = CleanUpAsync();
        }
    }
}
