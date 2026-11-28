using System;
using Unity.Netcode;

namespace MushroomSurvival
{
    // Host-confirmed winner shown to every peer before the Result scene. ResultAt is host (server) time.
    public struct WinnerAnnouncement : INetworkSerializable, IEquatable<WinnerAnnouncement>
    {
        public ulong ClientId;
        public double ResultAt;

        public WinnerAnnouncement(ulong clientId, double resultAt)
        {
            ClientId = clientId;
            ResultAt = resultAt;
        }

        public bool IsActive => ResultAt > 0d;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref ResultAt);
        }

        public bool Equals(WinnerAnnouncement other) => ClientId == other.ClientId && ResultAt == other.ResultAt;
    }
}
