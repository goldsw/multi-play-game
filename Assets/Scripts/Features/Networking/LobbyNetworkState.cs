using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    public sealed class LobbyNetworkState : NetworkBehaviour
    {
        private readonly NetworkVariable<SessionStatus> status = new(SessionStatus.Lobby);
        private NetworkList<LobbySlotData> slots;
        private SlotManager slotManager;

        public event Action Changed;
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
            }
            slots.OnListChanged += OnSlotChanged;
            status.OnValueChanged += OnStatusChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            slots.OnListChanged -= OnSlotChanged;
            status.OnValueChanged -= OnStatusChanged;
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
        private void OnStatusChanged(SessionStatus previous, SessionStatus current) => Changed?.Invoke();
    }
}
