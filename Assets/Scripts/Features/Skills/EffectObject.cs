using Unity.Netcode;

namespace MushroomSurvival
{
    public abstract class EffectObject : NetworkBehaviour
    {
        private EffectObjectManager manager;

        public ulong CasterId { get; private set; }
        protected SkillDefinition Definition { get; private set; }
        public string SkillId => Definition.SkillId;

        internal void Initialize(EffectObjectManager owner, SkillDefinition skill, ulong casterId)
        {
            manager = owner;
            Definition = skill;
            CasterId = casterId;
        }

        public override void OnNetworkDespawn()
        {
            if (manager != null)
                manager.Unregister(this);
        }
    }
}
