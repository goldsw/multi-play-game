using System;

namespace MushroomSurvival
{
    public sealed class SlotManager
    {
        private readonly ulong?[] reservations = new ulong?[LobbySettings.PlayerCount];
        private readonly LobbySlotData[] slots = new LobbySlotData[LobbySettings.PlayerCount];

        public int FindNextSlot() => Array.IndexOf(reservations, null);

        public int GetSlotIndex(ulong clientId) => Array.IndexOf(reservations, (ulong?)clientId);

        public bool TryReserveSlot(ulong clientId, out int index)
        {
            index = GetSlotIndex(clientId);
            if (index >= 0)
                return true;
            index = FindNextSlot();
            if (index < 0)
                return false;
            reservations[index] = clientId;
            return true;
        }

        public LobbySlotData InitSlot(int index, ulong clientId, int characterId)
        {
            if (reservations[index] != clientId)
                throw new InvalidOperationException("This slot belongs to another connection.");
            slots[index] = new LobbySlotData(clientId, characterId);
            return slots[index];
        }

        public LobbySlotData GetSlot(int index) => slots[index];

        public int ReleaseSlot(ulong clientId)
        {
            int index = GetSlotIndex(clientId);
            if (index >= 0)
            {
                reservations[index] = null;
                slots[index] = default;
            }
            return index;
        }

        public void Clear()
        {
            Array.Clear(reservations, 0, reservations.Length);
            Array.Clear(slots, 0, slots.Length);
        }
    }
}
