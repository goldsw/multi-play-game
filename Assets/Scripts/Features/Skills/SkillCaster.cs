using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectManager))]
    public sealed class SkillCaster : NetworkBehaviour
    {
        [SerializeField] private SkillDefinition skill;

        private NetworkList<double> rechargeEndsAt;
        private readonly NetworkVariable<int> placedCount = new(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private EffectObjectManager objects;
        private LobbyNetworkState lobby;
        private EffectManager effects;
        private Collider playerCollider;

        public SkillDefinition Skill => skill;
        public int PlacedCount => placedCount.Value;
        public double ServerTime => IsSpawned ? NetworkManager.ServerTime.Time : 0d;

        public double GetRechargeEnd(int chargeIndex) =>
            chargeIndex >= 0 && chargeIndex < rechargeEndsAt.Count ? rechargeEndsAt[chargeIndex] : 0d;

        private void Awake()
        {
            effects = GetComponent<EffectManager>();
            playerCollider = GetComponent<Collider>();
            rechargeEndsAt = new NetworkList<double>(readPerm: NetworkVariableReadPermission.Owner);
        }

        public void Configure(SkillDefinition definition) => skill = definition;

        public void Initialize(EffectObjectManager manager, LobbyNetworkState state)
        {
            objects = manager;
            lobby = state;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner && TryGetComponent<SkillRangePreview>(out var preview))
                preview.SetSkill(skill);

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
                CastRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void CastRpc()
        {
            if (skill == null || lobby == null || lobby.Status != SessionStatus.Playing)
                return;

            double now = ServerTime;
            for (int charge = 0; charge < rechargeEndsAt.Count; charge++)
            {
                if (now < rechargeEndsAt[charge])
                    continue;

                bool cast = skill.SpawnPrefab != null
                    ? objects != null && objects.TrySpawnAtFeet(skill, OwnerClientId, playerCollider)
                    : effects.TryEnqueue(new EffectRequest(skill.Effect));
                if (cast)
                    rechargeEndsAt[charge] = now + skill.CooldownSeconds;
                return;
            }
        }

        private void UpdatePlacedCount(ulong casterId, string skillId, int count)
        {
            if (IsSpawned && casterId == OwnerClientId && skillId == skill.SkillId)
                placedCount.Value = count;
        }
    }
}
