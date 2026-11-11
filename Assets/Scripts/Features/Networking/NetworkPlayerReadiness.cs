using Unity.Netcode;

namespace MushroomSurvival
{
    public sealed class NetworkPlayerReadiness : NetworkBehaviour
    {
        public bool IsReady { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                ConfirmReadyRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ConfirmReadyRpc()
        {
            IsReady = true;
        }
    }
}
