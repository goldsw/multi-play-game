using System;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public struct MovementEffectState : INetworkSerializable, IEquatable<MovementEffectState>
    {
        public static MovementEffectState Neutral => new(false, 1f, Vector2.zero);

        private bool stunned;
        private float speedMultiplier;
        private Vector2 forcedPlanarVelocity;

        public bool Stunned => stunned;
        public float SpeedMultiplier => speedMultiplier;
        public Vector2 ForcedPlanarVelocity => forcedPlanarVelocity;
        public bool HasForcedMovement => !forcedPlanarVelocity.Equals(Vector2.zero);

        public MovementEffectState(bool stunned, float speedMultiplier, Vector2 forcedPlanarVelocity)
        {
            this.stunned = stunned;
            this.speedMultiplier = speedMultiplier;
            this.forcedPlanarVelocity = forcedPlanarVelocity;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref stunned);
            serializer.SerializeValue(ref speedMultiplier);
            serializer.SerializeValue(ref forcedPlanarVelocity);
        }

        public bool Equals(MovementEffectState other) =>
            stunned == other.stunned && speedMultiplier == other.speedMultiplier &&
            forcedPlanarVelocity.Equals(other.forcedPlanarVelocity);
    }
}
