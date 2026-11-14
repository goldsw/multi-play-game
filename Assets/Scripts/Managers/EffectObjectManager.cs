using System;
using System.Collections.Generic;
using Unity.Netcode;
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

        public bool TrySpawn(SkillDefinition skill, NetworkObject caster, Collider player, Vector3 direction)
        {
            ulong casterId = caster.OwnerClientId;
            if (player == null || skill.Effect == null || skill.SkillRange <= 0f ||
                (skill.ObjectLimit > 0 && CountFor(casterId, skill.SkillId) >= skill.ObjectLimit) ||
                !skill.SpawnPrefab.TryGetSpawnPose(skill, player, floor, direction, out var position, out var rotation))
                return false;

            var instance = Instantiate(skill.SpawnPrefab, position, rotation);
            SceneManager.MoveGameObjectToScene(instance.gameObject, gameObject.scene);
            instance.Initialize(this, skill, caster);
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
