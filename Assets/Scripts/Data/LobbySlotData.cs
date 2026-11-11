using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    public struct LobbySlotData : INetworkSerializable, IEquatable<LobbySlotData>
    {
        public bool Occupied;
        public ulong ClientId;
        public int CharacterId;

        public LobbySlotData(ulong clientId, int characterId)
        {
            Occupied = true;
            ClientId = clientId;
            CharacterId = characterId;
        }

        public bool Equals(LobbySlotData other) =>
            Occupied == other.Occupied && ClientId == other.ClientId && CharacterId == other.CharacterId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Occupied);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref CharacterId);
        }
    }
}
