using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class DuckEgg : EffectObject
    {
        private const float ContactHalfHeight = 0.15f;
        [SerializeField] private Transform shell;

        private readonly NetworkVariable<bool> armed = new();
        private readonly Collider[] overlaps = new Collider[32];
        private NetworkTickSystem ticks;
        private Vector3 shellScale;
        private double armsAt;
        private double expiresAt;

        public bool IsArmed => armed.Value;

        public void Configure(Transform visual) => shell = visual;

        private void Awake() => shellScale = shell.localScale;

        public override void OnNetworkSpawn()
        {
            armed.OnValueChanged += OnArmedChanged;
            OnArmedChanged(false, armed.Value);
            if (IsServer)
            {
                ticks = NetworkManager.NetworkTickSystem;
                double now = ticks.ServerTime.Time;
                armsAt = now + Definition.ArmDelay;
                expiresAt = now + Definition.ObjectLifetime;
                ticks.Tick += Tick;
            }
        }

        public override void OnNetworkDespawn()
        {
            armed.OnValueChanged -= OnArmedChanged;
            if (ticks != null)
            {
                ticks.Tick -= Tick;
                ticks = null;
            }
            base.OnNetworkDespawn();
        }

        private void Tick()
        {
            double now = ticks.ServerTime.Time;
            if (now >= expiresAt)
            {
                NetworkObject.Despawn();
                return;
            }

            if (now < armsAt)
                return;
            if (!armed.Value)
                armed.Value = true;

            var target = FindTarget();
            if (target != null && target.TryEnqueue(new EffectRequest(Definition.Effect)))
                NetworkObject.Despawn();
        }

        private EffectManager FindTarget()
        {
            var origin = transform.position;
            float radius = Definition.SkillRange;
            int count = Physics.OverlapBoxNonAlloc(origin + Vector3.up * ContactHalfHeight,
                new Vector3(radius, ContactHalfHeight, radius), overlaps,
                Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            EffectManager nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                var collider = overlaps[index];
                var candidate = collider.GetComponentInParent<EffectManager>();
                if (candidate == null || !candidate.IsSpawned || !candidate.NetworkObject.IsPlayerObject)
                    continue;

                var offset = collider.ClosestPoint(origin) - origin;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius)
                    continue;

                var center = candidate.transform.position - origin;
                center.y = 0f;
                float distance = center.sqrMagnitude;
                if (distance < nearestDistance ||
                    (distance == nearestDistance && candidate.NetworkObjectId < nearest.NetworkObjectId))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private void OnArmedChanged(bool previous, bool current) =>
            shell.localScale = current ? shellScale : shellScale * 0.7f;
    }
}
