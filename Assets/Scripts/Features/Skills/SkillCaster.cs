using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectManager))]
    public sealed class SkillCaster : NetworkBehaviour
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private SkillDefinition alternateSkill;

        private NetworkList<double> rechargeEndsAt;
        private readonly NetworkVariable<int> placedCount = new(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> useAlternateSkill = new();

        private EffectObjectManager objects;
        private LobbyNetworkState lobby;
        private EffectManager effects;
        private Collider playerCollider;
        private RigidbodyMovement movement;
        private bool startWithAlternateSkill;

        public SkillDefinition Skill => useAlternateSkill.Value ? alternateSkill : skill;
        public Vector2 Facing => movement != null ? movement.Facing : Vector2.up;
        public int PlacedCount => placedCount.Value;
        public double ServerTime => IsSpawned ? NetworkManager.ServerTime.Time : 0d;

        public double GetRechargeEnd(int chargeIndex) =>
            chargeIndex >= 0 && chargeIndex < rechargeEndsAt.Count ? rechargeEndsAt[chargeIndex] : 0d;

        private void Awake()
        {
            effects = GetComponent<EffectManager>();
            playerCollider = GetComponent<Collider>();
            movement = GetComponent<RigidbodyMovement>();
            rechargeEndsAt = new NetworkList<double>(readPerm: NetworkVariableReadPermission.Owner);
        }

        public void Configure(SkillDefinition definition, SkillDefinition alternate)
        {
            skill = definition;
            alternateSkill = alternate;
        }

        public void Initialize(EffectObjectManager manager, LobbyNetworkState state, bool useAlternate)
        {
            objects = manager;
            lobby = state;
            startWithAlternateSkill = useAlternate;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                useAlternateSkill.Value = startWithAlternateSkill && alternateSkill != null;

            if (IsOwner && TryGetComponent<SkillRangePreview>(out var preview))
                preview.SetSkill(Skill);

            var skill = Skill;
            if (!IsServer || skill == null)
                return;

            rechargeEndsAt.Clear();
            for (int index = 0; index < skill.Charges; index++)
                rechargeEndsAt.Add(0d);
            placedCount.Value = 0;
            if (objects != null)
                objects.CountChanged += UpdatePlacedCount;
        }

        public override void OnNetworkDespawn()
        {
            if (objects != null)
                objects.CountChanged -= UpdatePlacedCount;
        }

        public void RequestCast()
        {
            if (IsSpawned && IsOwner)
                CastRpc(Facing);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void CastRpc(Vector2 facing)
        {
            var skill = Skill;
            if (skill == null || lobby == null || lobby.Status != SessionStatus.Playing)
                return;
            if (!float.IsFinite(facing.sqrMagnitude) || facing.sqrMagnitude < 0.01f)
                return;

            var direction = new Vector3(facing.x, 0f, facing.y).normalized;
            double now = ServerTime;
            for (int charge = 0; charge < rechargeEndsAt.Count; charge++)
            {
                if (now < rechargeEndsAt[charge])
                    continue;

                bool cast = skill.SpawnPrefab != null
                    ? objects != null && objects.TrySpawn(skill, NetworkObject, playerCollider, direction)
                    : effects.TryEnqueue(new EffectRequest(skill.Effect, sourcePlayerId: NetworkObjectId));
                if (cast)
                    rechargeEndsAt[charge] = now + skill.CooldownSeconds;
                return;
            }
        }

        private void UpdatePlacedCount(ulong casterId, string skillId, int count)
        {
            if (IsSpawned && casterId == OwnerClientId && skillId == Skill.SkillId)
                placedCount.Value = count;
        }
    }
}
