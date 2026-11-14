using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class MatchItems : MonoBehaviour
    {
        [SerializeField] private ItemBoxSpawner boxSpawnerPrefab;
        [SerializeField] private ItemMap map;
        private ItemBoxSpawner spawned;

        public void Configure(ItemMap itemMap, ItemBoxSpawner prefab)
        {
            map = itemMap;
            boxSpawnerPrefab = prefab;
        }

        internal void Begin(LobbyNetworkState lobby)
        {
            if (!lobby.IsServer || spawned != null || boxSpawnerPrefab == null || map == null)
                return;

            spawned = Instantiate(boxSpawnerPrefab);
            SceneManager.MoveGameObjectToScene(spawned.gameObject, gameObject.scene);
            spawned.Initialize(map, lobby);
            spawned.NetworkObject.Spawn(destroyWithScene: true);
        }
    }
}
