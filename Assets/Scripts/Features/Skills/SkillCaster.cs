using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectManager))]
    public sealed class SkillCaster : NetworkBehaviour
    {
        [SerializeField] private SkillDefinition[] runnerSkills;
        [SerializeField] private SkillDefinition taggerSkill;

        private NetworkList<double> rechargeEndsAt;
        private readonly NetworkVariable<int> placedCount = new(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> runnerSkillIndex = new();
        private readonly NetworkVariable<bool> tagger = new();
        private readonly NetworkVariable<double> activeEndsAt = new(
            0d, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private EffectObjectManager objects;
        private LobbyNetworkState lobby;
        private EffectManager effects;
        private Collider playerCollider;
        private RigidbodyMovement movement;
        private CharacterStateMachine character;
        private int initialSkillIndex;

        public SkillDefinition Skill => tagger.Value ? taggerSkill : GetRunnerSkill(runnerSkillIndex.Value);
        public bool IsTagger => tagger.Value;
        public double ActiveEndsAt => activeEndsAt.Value;
        public Vector2 Facing => movement != null ? movement.Facing : Vector2.up;
        public int PlacedCount => placedCount.Value;
        public double ServerTime => IsSpawned ? NetworkManager.ServerTime.Time : 0d;

        public double GetRechargeEnd(int chargeIndex) =>
            chargeIndex >= 0 && chargeIndex < rechargeEndsAt.Count ? rechargeEndsAt[chargeIndex] : 0d;

        public int GetReadyChargeCount(double serverTime)
        {
            int ready = 0;
            for (int index = 0; index < rechargeEndsAt.Count; index++)
            {
                if (rechargeEndsAt[index] <= serverTime)
                    ready++;
            }
            return ready;
        }

        private void Awake()
        {
            effects = GetComponent<EffectManager>();
            playerCollider = GetComponent<Collider>();
            movement = GetComponent<RigidbodyMovement>();
            character = GetComponent<CharacterStateMachine>();
            rechargeEndsAt = new NetworkList<double>(readPerm: NetworkVariableReadPermission.Owner);
        }

        public void Configure(SkillDefinition[] definitions, SkillDefinition sharedTaggerSkill)
        {
            runnerSkills = definitions;
            taggerSkill = sharedTaggerSkill;
        }

        public void Initialize(EffectObjectManager manager, LobbyNetworkState state, int skillIndex)
        {
            objects = manager;
            lobby = state;
            initialSkillIndex = skillIndex;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                runnerSkillIndex.Value = GetRunnerSkill(initialSkillIndex) != null ? initialSkillIndex : 0;
                ResetCharges();
                if (objects != null)
                    objects.CountChanged += UpdatePlacedCount;
            }
            runnerSkillIndex.OnValueChanged += OnRunnerSkillChanged;
            tagger.OnValueChanged += OnRoleChanged;
            RefreshPreview();
        }

        public override void OnNetworkDespawn()
        {
            if (objects != null)
                objects.CountChanged -= UpdatePlacedCount;
            runnerSkillIndex.OnValueChanged -= OnRunnerSkillChanged;
            tagger.OnValueChanged -= OnRoleChanged;
        }

        public bool SetRunnerSkill(string skillId)
        {
            if (!IsServer || !IsSpawned || runnerSkills == null)
                return false;

            for (int index = 0; index < runnerSkills.Length; index++)
            {
                if (runnerSkills[index] == null || runnerSkills[index].SkillId != skillId)
                    continue;
                if (runnerSkillIndex.Value != index)
                {
                    var previous = Skill;
                    runnerSkillIndex.Value = index;
                    if (!IsTagger)
                        ResetForSkillChange(previous);
                }
                return true;
            }
            return false;
        }

        internal void SetTagger(bool value)
        {
            if (tagger.Value == value)
                return;

            var previous = Skill;
            tagger.Value = value;
            ResetForSkillChange(previous);
        }

        private void ResetForSkillChange(SkillDefinition previous)
        {
            if (previous != null)
            {
                objects?.DespawnOwned(OwnerClientId, previous.SkillId);
                effects.RemoveSelfEffect(previous.Effect);
            }
            ResetCharges();
        }

        private void ResetCharges()
        {
            rechargeEndsAt.Clear();
            if (Skill != null)
            {
                for (int index = 0; index < Skill.Charges; index++)
                    rechargeEndsAt.Add(0d);
            }
            activeEndsAt.Value = 0d;
            placedCount.Value = 0;
        }

        private SkillDefinition GetRunnerSkill(int index) =>
            runnerSkills != null && index >= 0 && index < runnerSkills.Length ? runnerSkills[index] : null;

        private void OnRunnerSkillChanged(int previous, int current) => RefreshPreview();
        private void OnRoleChanged(bool previous, bool current) => RefreshPreview();

        private void RefreshPreview()
        {
            if (IsOwner && TryGetComponent<SkillRangePreview>(out var preview))
                preview.SetSkill(Skill);
        }

        public void RequestCast()
        {
            if (IsSpawned && IsOwner && character?.AllowsPlayerActions != false)
                CastRpc(Facing);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void CastRpc(Vector2 facing)
        {
            var skill = Skill;
            if (skill == null || lobby == null || lobby.Status != SessionStatus.Playing ||
                (character != null && !character.AllowsPlayerActions))
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
                {
                    double activeEnd = skill.CooldownAfterEffect ? now + skill.Effect.DurationSeconds : now;
                    activeEndsAt.Value = skill.CooldownAfterEffect ? activeEnd : 0d;
                    rechargeEndsAt[charge] = activeEnd + skill.CooldownSeconds;
                    character?.PlayCast();
                }
                return;
            }
        }

        private void UpdatePlacedCount(ulong casterId, string skillId, int count)
        {
            if (IsSpawned && casterId == OwnerClientId && skillId == Skill?.SkillId)
                placedCount.Value = count;
        }
    }
}
