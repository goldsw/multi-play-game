using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyConnectionGate : MonoBehaviour
    {
        private NetworkManager network;
        private SessionNetworkState state;
        private SlotManager slotManager;

        public void Initialize(NetworkManager manager, SessionNetworkState sessionState, SlotManager slots)
        {
            network = manager;
            state = sessionState;
            slotManager = slots;
            network.ConnectionApprovalCallback += Approve;
        }

        private void Approve(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var canJoin = (!state.IsSpawned || state.Status == SessionStatus.Lobby) &&
                slotManager.TryReserveSlot(request.ClientNetworkId, out _);
            response.Approved = canJoin;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = canJoin ? "" : "The session is full or the match has already started.";
        }

        private void OnDestroy()
        {
            if (network == null)
                return;
            network.ConnectionApprovalCallback -= Approve;
        }
    }
}
