using Unity.Netcode;

namespace MushroomSurvival
{
    public struct EffectApplied : INetworkSerializable
    {
        public const ulong NoSourcePlayer = ulong.MaxValue;

        public ulong SourcePlayerId { get; private set; }
        public int EffectId { get; private set; }
        public ulong TargetPlayerId { get; private set; }
        public double EndsAtServerTime { get; private set; }

        public EffectApplied(ulong sourcePlayerId, int effectId, ulong targetPlayerId, double endsAtServerTime)
        {
            SourcePlayerId = sourcePlayerId;
            EffectId = effectId;
            TargetPlayerId = targetPlayerId;
            EndsAtServerTime = endsAtServerTime;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            ulong source = SourcePlayerId;
            int effect = EffectId;
            ulong target = TargetPlayerId;
            double endsAt = EndsAtServerTime;
            serializer.SerializeValue(ref source);
            serializer.SerializeValue(ref effect);
            serializer.SerializeValue(ref target);
            serializer.SerializeValue(ref endsAt);
            if (serializer.IsReader)
                this = new EffectApplied(source, effect, target, endsAt);
        }
    }
}
