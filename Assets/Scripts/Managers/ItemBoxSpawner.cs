using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ItemBoxSpawner : NetworkBehaviour
    {
        private const int BoxesPerWave = 3;
        private const int PoolCapacity = 5;
        private const float WaveInterval = 15f;

        [SerializeField] private ItemBox boxPrefab;

        private readonly List<ItemBox> pool = new(PoolCapacity);
        private ItemMap map;
        private SessionNetworkState sessionState;
        private double nextWave;
        private bool wasPlaying;

        internal bool IsMatchPlaying => sessionState != null && sessionState.Status == SessionStatus.Playing;
        internal bool CanLandAt(Vector3 floor) => map.TryGetSurface(floor, out var point) &&
            Mathf.Abs(point.y - floor.y) <= 0.05f;

        public void Configure(ItemBox prefab) => boxPrefab = prefab;

        public void Initialize(ItemMap itemMap, SessionNetworkState state)
        {
            map = itemMap;
            sessionState = state;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer || boxPrefab == null)
                return;

            for (int index = 0; index < PoolCapacity; index++)
            {
                ItemBox box = Instantiate(boxPrefab);
                SceneManager.MoveGameObjectToScene(box.gameObject, gameObject.scene);
                box.NetworkObject.Spawn(destroyWithScene: true);
                pool.Add(box);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null && NetworkManager.IsListening
                && !NetworkManager.ShutdownInProgress)
            {
                foreach (ItemBox box in pool)
                {
                    if (box != null && box.IsSpawned)
                        box.NetworkObject.Despawn();
                }
            }

            pool.Clear();
            wasPlaying = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || map == null)
                return;

            if (!IsMatchPlaying)
            {
                if (wasPlaying)
                {
                    foreach (ItemBox box in pool)
                        box.Deactivate();
                }

                wasPlaying = false;
                return;
            }

            double now = NetworkManager.ServerTime.Time;
            if (!wasPlaying)
            {
                nextWave = now + WaveInterval;
                wasPlaying = true;
            }

            if (now < nextWave)
                return;

            nextWave = now + WaveInterval;
            SpawnWave();
        }

        private void SpawnWave()
        {
            int spawned = 0;
            foreach (ItemBox box in pool)
            {
                if (spawned == BoxesPerWave)
                    return;

                if (!box.IsActive && map.TryGetRandomGround(IsTileFree, out Vector3 floor))
                {
                    box.Activate(this, floor);
                    spawned++;
                }
            }
        }

        private bool IsTileFree(Vector3 floor)
        {
            float range = map.TileSize * 0.5f;
            foreach (ItemBox box in pool)
            {
                if (box.IsActive && (box.OccupiedPosition - floor).sqrMagnitude < range * range)
                    return false;
            }

            return true;
        }
    }
}
