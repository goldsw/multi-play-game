using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class EffectManager : NetworkBehaviour
    {
        private readonly Queue<EffectRequest> pending = new();
        private readonly List<ActiveEffect> active = new();

        private NetworkTickSystem tickSystem;

        public IReadOnlyList<ActiveEffect> ActiveEffects { get; private set; }
        public event Action ActiveEffectsChanged;

        private void Awake()
        {
            ActiveEffects = active.AsReadOnly();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            tickSystem = NetworkManager.NetworkTickSystem;
            tickSystem.Tick += UpdateEffects;
        }

        public override void OnNetworkDespawn()
        {
            if (tickSystem != null)
            {
                tickSystem.Tick -= UpdateEffects;
                tickSystem = null;
            }

            pending.Clear();
            active.Clear();
        }

        public bool TryEnqueue(EffectRequest request)
        {
            var definition = request.Definition;
            if (!IsServer || !IsSpawned || definition == null ||
                !float.IsFinite(definition.DurationSeconds) || definition.DurationSeconds <= 0f ||
                !definition.Accepts(request))
            {
                return false;
            }

            pending.Enqueue(request);
            return true;
        }

        private void UpdateEffects()
        {
            if (active.Count == 0 && pending.Count == 0)
            {
                return;
            }

            double now = tickSystem.ServerTime.Time;
            bool changed = pending.Count != 0;
            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].ExpiresAt <= now)
                {
                    active.RemoveAt(index);
                    changed = true;
                }
            }

            while (pending.TryDequeue(out var request))
            {
                var definition = request.Definition;
                if (definition.Reapplication == EffectReapplication.Refresh)
                {
                    active.RemoveAll(effect => effect.Request.Definition == definition);
                }

                active.Add(new ActiveEffect(request, now + definition.DurationSeconds));
            }

            if (changed)
            {
                ActiveEffectsChanged?.Invoke();
            }
        }
    }
}
