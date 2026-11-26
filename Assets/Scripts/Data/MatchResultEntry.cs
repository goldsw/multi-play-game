using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    [Serializable]
    public struct MatchResultEntry : INetworkSerializable, IEquatable<MatchResultEntry>
    {
        public ulong ClientId;
        public int CharacterId;
        public int Rank;

        public MatchResultEntry(ulong clientId, int characterId, int rank)
        {
            ClientId = clientId;
            CharacterId = characterId;
            Rank = rank;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref CharacterId);
            serializer.SerializeValue(ref Rank);
        }

        public bool Equals(MatchResultEntry other) =>
            ClientId == other.ClientId && CharacterId == other.CharacterId && Rank == other.Rank;
    }
}
