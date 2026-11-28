using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    public sealed class SessionNetworkState : NetworkBehaviour
    {
        private readonly NetworkVariable<SessionStatus> status = new(SessionStatus.Lobby);
        private readonly NetworkVariable<WinnerAnnouncement> winner = new();
        private NetworkList<LobbySlotData> slots;
        private SlotManager slotManager;

        public event Action Changed;
        // Raised on every peer when the winner announcement is published or cleared.
        public event Action WinnerChanged;
        public int PlayerCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < slots.Count; index++)
                    if (slots[index].Occupied)
                        count++;
                return count;
            }
        }
        public SessionStatus Status => status.Value;
        public WinnerAnnouncement Winner => IsSpawned ? winner.Value : default;
        public int LocalCharacterId
        {
            get
            {
                if (IsSpawned)
                {
                    foreach (var slot in slots)
                    {
                        if (slot.Occupied && slot.ClientId == NetworkManager.LocalClientId)
                            return slot.CharacterId;
                    }
                }
                return LobbySettings.DefaultCharacterId;
            }
        }

        private void Awake() => slots = new NetworkList<LobbySlotData>();

        public void Initialize(SlotManager manager) => slotManager = manager;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                slots.Clear();
                for (int index = 0; index < LobbySettings.PlayerCount; index++)
                    slots.Add(slotManager.GetSlot(index));
                status.Value = SessionStatus.Lobby;
                winner.Value = default;
            }
            slots.OnListChanged += OnSlotChanged;
            status.OnValueChanged += OnStatusChanged;
            winner.OnValueChanged += OnWinnerChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            slots.OnListChanged -= OnSlotChanged;
            status.OnValueChanged -= OnStatusChanged;
            winner.OnValueChanged -= OnWinnerChanged;
            Changed?.Invoke();
        }

        public LobbySlotData[] GetSlots()
        {
            var snapshot = new LobbySlotData[LobbySettings.PlayerCount];
            for (int index = 0; index < slots.Count; index++)
                snapshot[index] = slots[index];
            return snapshot;
        }

        public void PublishSlot(int index, LobbySlotData slot)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host can publish slots.");
            slots[index] = slot;
        }

        public void PublishStatus(SessionStatus phase)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host can publish lobby state.");
            status.Value = phase;
        }

        public void PublishWinner(WinnerAnnouncement announcement)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host can publish the winner.");
            winner.Value = announcement;
        }

        public void SelectCharacter(int characterId)
        {
            if (IsSpawned && IsClient && Status == SessionStatus.Lobby)
                SelectCharacterRpc(characterId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SelectCharacterRpc(int characterId, RpcParams rpc = default)
        {
            if (Status != SessionStatus.Lobby || !Enum.IsDefined(typeof(RunnerCharacter), characterId))
                return;

            ulong sender = rpc.Receive.SenderClientId;
            int index = slotManager.GetSlotIndex(sender);
            if (index >= 0 && slots[index].Occupied && slots[index].ClientId == sender)
                PublishSlot(index, slotManager.InitSlot(index, sender, characterId));
        }

        private void OnSlotChanged(NetworkListEvent<LobbySlotData> change) => Changed?.Invoke();
        private void OnWinnerChanged(WinnerAnnouncement previous, WinnerAnnouncement current) => WinnerChanged?.Invoke();
        private void OnStatusChanged(SessionStatus previous, SessionStatus current) => Changed?.Invoke();
    }
}
