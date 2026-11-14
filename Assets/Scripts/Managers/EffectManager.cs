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
        private CharacterStateMachine character;

        public IReadOnlyList<ActiveEffect> ActiveEffects { get; private set; }
        public event Action ActiveEffectsChanged;
        public event Action<ActiveEffect> Applied;

        private void Awake()
        {
            ActiveEffects = active.AsReadOnly();
            character = GetComponent<CharacterStateMachine>();
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
            if (!IsServer || !IsSpawned || character?.IsEliminated == true || definition == null ||
                !float.IsFinite(request.DurationSeconds) || request.DurationSeconds <= 0f ||
                !definition.Accepts(request))
            {
                return false;
            }

            pending.Enqueue(request);
            return true;
        }

        internal void ClearEffects()
        {
            pending.Clear();
            active.Clear();
            ActiveEffectsChanged?.Invoke();
        }

        internal void RemoveSelfEffect(EffectDefinition definition) => RemoveEffect(definition, NetworkObjectId);

        internal void RemoveEffect(EffectDefinition definition, ulong sourcePlayerId)
        {
            if (definition == null)
                return;

            int count = pending.Count;
            for (int index = 0; index < count; index++)
            {
                var request = pending.Dequeue();
                if (request.Definition != definition || request.SourcePlayerId != sourcePlayerId)
                    pending.Enqueue(request);
            }

            if (active.RemoveAll(effect => effect.Request.Definition == definition &&
                    effect.Request.SourcePlayerId == sourcePlayerId) > 0)
                ActiveEffectsChanged?.Invoke();
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

                var applied = new ActiveEffect(request, now + request.DurationSeconds);
                active.Add(applied);
                if (request.Notify)
                    Applied?.Invoke(applied);
            }

            if (changed)
            {
                ActiveEffectsChanged?.Invoke();
            }
        }
    }
}
