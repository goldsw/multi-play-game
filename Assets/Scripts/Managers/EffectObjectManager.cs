using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class EffectObjectManager : MonoBehaviour
    {
        [SerializeField] private Collider floor;

        private readonly List<EffectObject> active = new();

        public event Action<ulong, string, int> CountChanged;

        public void Configure(Collider ground) => floor = ground;

        public bool TrySpawnAtFeet(SkillDefinition skill, ulong casterId, Collider player)
        {
            if (floor == null || player == null || skill.Effect == null ||
                skill.ObjectLifetime <= skill.ArmDelay || skill.SkillRange <= 0f ||
                CountFor(casterId, skill.SkillId) >= skill.ObjectLimit)
                return false;

            var bounds = player.bounds;
            var ray = new Ray(bounds.center, Vector3.down);
            if (!floor.Raycast(ray, out var hit, bounds.extents.y + 0.2f))
                return false;

            var instance = Instantiate(skill.SpawnPrefab, hit.point, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance.gameObject, gameObject.scene);
            instance.Initialize(this, skill, casterId);
            active.Add(instance);
            try
            {
                instance.NetworkObject.Spawn(destroyWithScene: true);
            }
            catch (Exception exception)
            {
                active.Remove(instance);
                Destroy(instance.gameObject);
                Debug.LogException(exception);
                return false;
            }

            PublishCount(casterId, skill.SkillId);
            return true;
        }

        internal void Unregister(EffectObject instance)
        {
            if (active.Remove(instance))
                PublishCount(instance.CasterId, instance.SkillId);
        }

        private int CountFor(ulong casterId, string skillId)
        {
            int count = 0;
            foreach (var instance in active)
            {
                if (instance.CasterId == casterId && instance.SkillId == skillId)
                    count++;
            }
            return count;
        }

        private void PublishCount(ulong casterId, string skillId) =>
            CountChanged?.Invoke(casterId, skillId, CountFor(casterId, skillId));
    }
}
