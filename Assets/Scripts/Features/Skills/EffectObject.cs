using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public abstract class EffectObject : NetworkBehaviour
    {
        private EffectObjectManager manager;
        private NetworkTickSystem ticks;

        public ulong CasterId { get; private set; }
        protected ulong SourcePlayerId { get; private set; }
        protected SkillDefinition Definition { get; private set; }
        public string SkillId => Definition.SkillId;

        internal abstract bool TryGetSpawnPose(SkillDefinition skill, Collider caster, Collider floor,
            Vector3 direction, out Vector3 position, out Quaternion rotation);

        internal void Initialize(EffectObjectManager owner, SkillDefinition skill, NetworkObject caster)
        {
            manager = owner;
            Definition = skill;
            CasterId = caster.OwnerClientId;
            SourcePlayerId = caster.NetworkObjectId;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            ticks = NetworkManager.NetworkTickSystem;
            OnEffectStart(ticks.ServerTime.Time);
            ticks.Tick += Tick;
        }

        public override void OnNetworkDespawn()
        {
            if (ticks != null)
            {
                ticks.Tick -= Tick;
                ticks = null;
                OnEffectEnd();
            }

            if (manager != null)
                manager.Unregister(this);
        }

        protected virtual void OnEffectStart(double now) { }
        protected abstract void OnEffectUpdate(double now);
        protected virtual void OnEffectEnd() { }

        protected bool TryApplyTo(EffectManager target, Vector2 direction = default) =>
            target != null && target.TryEnqueue(new EffectRequest(Definition.Effect, direction, SourcePlayerId));

        protected static bool IsEligibleTarget(EffectManager target) =>
            target != null && target.IsSpawned && target.NetworkObject.IsPlayerObject &&
            (!target.TryGetComponent<CharacterStateMachine>(out var state) || !state.IsEliminated);

        private void Tick() => OnEffectUpdate(ticks.ServerTime.Time);
    }
}
