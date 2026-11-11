using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyConnectionGate : MonoBehaviour
    {
        private readonly HashSet<ulong> admittedClients = new();
        private NetworkManager network;
        private LobbyNetworkState state;

        public void Initialize(NetworkManager manager, LobbyNetworkState lobbyState)
        {
            network = manager;
            state = lobbyState;
            network.ConnectionApprovalCallback += Approve;
            network.OnClientDisconnectCallback += Release;
        }

        private void Approve(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var canJoin = admittedClients.Count < LobbySettings.PlayerCount &&
                (!state.IsSpawned || state.Status == SessionStatus.Lobby);
            response.Approved = canJoin;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = canJoin ? "" : "The session is full or the match has already started.";
            if (canJoin)
                admittedClients.Add(request.ClientNetworkId);
        }

        private void Release(ulong clientId) => admittedClients.Remove(clientId);

        private void Update()
        {
            if (network != null && !network.IsListening)
                admittedClients.Clear();
        }

        private void OnDestroy()
        {
            if (network == null)
                return;
            network.ConnectionApprovalCallback -= Approve;
            network.OnClientDisconnectCallback -= Release;
        }
    }
}
