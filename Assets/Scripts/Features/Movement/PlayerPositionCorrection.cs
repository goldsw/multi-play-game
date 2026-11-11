using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerPositionCorrection : NetworkBehaviour
    {
        private Rigidbody body;
        private bool hasSequence;
        private bool correctionPending;
        private uint latestSequence;
        private Vector3 pendingPosition;
        private Vector3 pendingNormal;
        public uint LastAppliedSequence { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void SendCorrection(Vector3 position, Vector3 normal, uint sequence)
        {
            if (IsSpawned && IsServer)
            {
                CorrectPositionRpc(position, normal, sequence);
            }
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void CorrectPositionRpc(Vector3 position, Vector3 normal, uint sequence)
        {
            if (!IsSpawned || !IsOwner ||
                (hasSequence && unchecked((int)(sequence - latestSequence)) <= 0))
            {
                return;
            }

            latestSequence = sequence;
            hasSequence = true;
            pendingPosition = position;
            pendingNormal = normal.normalized;
            correctionPending = true;
        }

        public override void OnNetworkDespawn()
        {
            hasSequence = false;
            correctionPending = false;
            LastAppliedSequence = 0;
        }

        public override void OnLostOwnership()
        {
            hasSequence = false;
            correctionPending = false;
            LastAppliedSequence = 0;
        }

        private void FixedUpdate()
        {
            if (!correctionPending || !IsSpawned || !IsOwner || body.isKinematic)
            {
                return;
            }

            correctionPending = false;
            body.position = pendingPosition;
            LastAppliedSequence = latestSequence;

            var velocity = body.linearVelocity;
            var speedIntoSurface = Vector3.Dot(velocity, pendingNormal);
            if (speedIntoSurface < 0f)
            {
                body.linearVelocity = velocity - speedIntoSurface * pendingNormal;
            }
        }
    }
}
