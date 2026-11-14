using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class SkillRangePreview : NetworkBehaviour
    {
        private const float SurfaceOffset = 0.06f;

        [SerializeField] private SkillDefinition skill;
        [SerializeField] private Material rangeMaterial;

        private Collider playerCollider;
        private SkillInput input;
        private SkillCaster caster;
        private CharacterStateMachine character;
        private SkillRangeView view;

        private void Awake()
        {
            playerCollider = GetComponent<Collider>();
            input = GetComponent<SkillInput>();
            caster = GetComponent<SkillCaster>();
            character = GetComponent<CharacterStateMachine>();
            view = new SkillRangeView(transform, rangeMaterial);
        }

        public void Configure(SkillDefinition initialSkill, Material material)
        {
            skill = initialSkill;
            rangeMaterial = material;
        }

        public void SetSkill(SkillDefinition definition) => skill = definition;

        public override void OnLostOwnership() => view.Hide();
        public override void OnNetworkDespawn() => view.Hide();

        private void OnDisable() => view?.Hide();

        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || input?.IsHeld != true ||
                skill == null || skill.RangeDisplay == SkillRangeEnum.None ||
                character?.AllowsPlayerActions == false)
            {
                view.Hide();
                return;
            }

            var origin = transform.position;
            if (playerCollider != null)
                origin.y = playerCollider.bounds.min.y;

            var surfaceLift = Vector3.up * SurfaceOffset;
            if (skill.RangeDisplay == SkillRangeEnum.SelfCircle)
            {
                view.ShowCircle(origin + surfaceLift, skill.SkillRange);
                return;
            }

            if (skill.SpawnPrefab is SkillProjectile && caster != null)
            {
                var facing = caster.Facing;
                var end = origin + new Vector3(facing.x, 0f, facing.y) * skill.SkillRange;
                view.ShowLine(origin + surfaceLift, end + surfaceLift);
                return;
            }

            if (!TryGetAimPoint(origin, out var point))
            {
                view.Hide();
                return;
            }

            var offset = point - origin;
            offset.y = 0f;
            var target = origin + Vector3.ClampMagnitude(offset, skill.SkillRange);
            switch (skill.RangeDisplay)
            {
                case SkillRangeEnum.TargetCircle:
                    view.ShowCircle(target + surfaceLift, skill.TargetCircleRadius);
                    break;
                case SkillRangeEnum.DirectionLine:
                    view.ShowLine(origin + surfaceLift, target + surfaceLift);
                    break;
                default:
                    view.Hide();
                    break;
            }
        }

        private static bool TryGetAimPoint(Vector3 origin, out Vector3 point)
        {
            point = default;
            var camera = Camera.main;
            var mouse = Mouse.current;
            if (camera == null || mouse == null)
                return false;

            var screenPoint = mouse.position.ReadValue();
            if (!camera.pixelRect.Contains(screenPoint))
                return false;

            var ray = camera.ScreenPointToRay(screenPoint);
            var ground = new Plane(Vector3.up, origin);
            if (!ground.Raycast(ray, out float distance))
                return false;

            point = ray.GetPoint(distance);
            return true;
        }
    }
}
