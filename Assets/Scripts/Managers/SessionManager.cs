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
            ISessionConnector sessionConnector)
        {
            networkManager = network;
            networkState = state;
            playerSpawner = spawner;
            connector = sessionConnector;
            networkState.Changed += RefreshState;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnTransportFailure += OnTransportFailure;
        }

        public Task HostAsync() => ConnectAsync(true, "");

        public Task JoinAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                SetState(SessionStatus.Failed, "Enter a join code.");
                return Task.CompletedTask;
            }
            return ConnectAsync(false, code.Trim().ToUpperInvariant());
        }

        private async Task ConnectAsync(bool host, string code)
        {
            if (busy || networkManager.IsListening || networkManager.ShutdownInProgress)
                return;
            busy = true;
            SetState(SessionStatus.Connecting, "Connecting to Relay...");
            try
            {
                await connector.LeaveAsync();
                joinCode = host ? await connector.CreateAsync() : code;
                if (!host)
                    await connector.JoinAsync(code);
                if (destroyed)
                {
                    await connector.LeaveAsync();
                    return;
                }
                if (!networkManager.IsListening || !networkManager.IsConnectedClient)
                    throw new InvalidOperationException("The session did not start networking.");
                RefreshState();
            }
            catch (Exception exception)
            {
                await CleanUpAsync();
                if (!destroyed)
                    SetState(SessionStatus.Failed, exception.Message);
            }
            finally
            {
                busy = false;
            }
        }

        public async Task LeaveAsync()
        {
            if (busy)
                return;
            busy = true;
            SetState(SessionStatus.Leaving, "Leaving session...");
            await CleanUpAsync();
            if (!destroyed)
                SetState(SessionStatus.Offline);
            busy = false;
        }

        public void StartMatch()
        {
            if (busy || !networkManager.IsHost || !networkState.IsSpawned ||
                networkState.Status != SessionStatus.Lobby ||
                networkManager.ConnectedClientsIds.Count < LobbySettings.MinimumPlayersToStart)
                return;
            matchParticipants = networkManager.ConnectedClientsIds.ToArray();
            networkState.Publish(matchParticipants.Length, SessionStatus.Starting);
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
                networkState.Publish(matchParticipants.Length, SessionStatus.Playing);
            else if (Time.realtimeSinceStartup >= startDeadline)
                FailSession("Player spawning timed out.");
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager.IsHost && networkState.IsSpawned)
            {
                if (networkState.Status == SessionStatus.Lobby)
                    networkState.Publish(networkManager.ConnectedClientsIds.Count, SessionStatus.Lobby);
                else if (Array.IndexOf(matchParticipants, clientId) < 0)
                    networkManager.DisconnectClient(clientId);
            }
            RefreshState();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (destroyed || busy)
                return;
            if (!networkManager.IsHost || clientId == networkManager.LocalClientId)
            {
                FailSession("Disconnected from the host. Return to the menu and join again.");
                return;
            }
            if (networkState.Status == SessionStatus.Starting || networkState.Status == SessionStatus.Playing)
            {
                if (Array.IndexOf(matchParticipants, clientId) >= 0)
                    FailSession("A player left. The session has ended.");
            }
            else if (networkState.IsSpawned)
                networkState.Publish(networkManager.ConnectedClientsIds.Count, SessionStatus.Lobby);
        }

        private void OnTransportFailure() => FailSession("The network connection failed.");

        private async void FailSession(string message)
        {
            if (busy || destroyed)
                return;
            busy = true;
            await CleanUpAsync();
            if (!destroyed)
                SetState(SessionStatus.Failed, message);
            busy = false;
        }

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
            }
        }

        private void RefreshState()
        {
            if (destroyed || State.Status == SessionStatus.Leaving || !networkManager.IsListening)
                return;
            var phase = networkState.IsSpawned ? networkState.Status : SessionStatus.Connecting;
            SetState(phase);
        }

        private void SetState(SessionStatus status, string message = "")
        {
            if (destroyed)
                return;
            var count = networkState != null && networkState.IsSpawned ? networkState.PlayerCount : 0;
            State = new SessionData(status, joinCode, count, networkManager != null && networkManager.IsHost, message);
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
