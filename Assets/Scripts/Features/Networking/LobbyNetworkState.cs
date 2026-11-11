using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    public sealed class LobbyNetworkState : NetworkBehaviour
    {
        private readonly NetworkVariable<int> playerCount = new();
        private readonly NetworkVariable<SessionStatus> status = new(SessionStatus.Lobby);

        public event Action Changed;
        public int PlayerCount => playerCount.Value;
        public SessionStatus Status => status.Value;

        public override void OnNetworkSpawn()
        {
            playerCount.OnValueChanged += OnPlayerCountChanged;
            status.OnValueChanged += OnStatusChanged;
            if (IsServer)
                Publish(NetworkManager.ConnectedClientsIds.Count, SessionStatus.Lobby);
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            playerCount.OnValueChanged -= OnPlayerCountChanged;
            status.OnValueChanged -= OnStatusChanged;
            Changed?.Invoke();
        }

        public void Publish(int count, SessionStatus phase)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host can publish lobby state.");
            playerCount.Value = count;
            status.Value = phase;
        }

        private void OnPlayerCountChanged(int previous, int current) => Changed?.Invoke();
        private void OnStatusChanged(SessionStatus previous, SessionStatus current) => Changed?.Invoke();
    }
}
