using System;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectManager))]
    public sealed class EffectBroadcast : NetworkBehaviour
    {
        private EffectManager effects;

        public event Action<EffectApplied> Received;
        public double ServerTime => IsSpawned ? NetworkManager.ServerTime.Time : 0d;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            effects = GetComponent<EffectManager>();
            effects.Applied += Broadcast;
        }

        public override void OnNetworkDespawn()
        {
            if (effects != null)
                effects.Applied -= Broadcast;
            effects = null;
        }

        private void Broadcast(ActiveEffect applied)
        {
            var request = applied.Request;
            if (request.Definition.EffectId <= 0)
                return;

            NotifyRpc(new EffectApplied(request.SourcePlayerId, request.Definition.EffectId,
                NetworkObjectId, applied.ExpiresAt));
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void NotifyRpc(EffectApplied applied)
        {
            if (applied.TargetPlayerId == NetworkObjectId && applied.EndsAtServerTime > ServerTime)
                Received?.Invoke(applied);
        }
    }
}
