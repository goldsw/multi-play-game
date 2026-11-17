using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    public struct EliminationResult : INetworkSerializable, IEquatable<EliminationResult>
    {
        public int Rank;
        public double SpectateAt;

        public EliminationResult(int rank, double spectateAt)
        {
            Rank = rank;
            SpectateAt = spectateAt;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Rank);
            serializer.SerializeValue(ref SpectateAt);
        }

        public bool Equals(EliminationResult other) => Rank == other.Rank && SpectateAt == other.SpectateAt;
    }
}
