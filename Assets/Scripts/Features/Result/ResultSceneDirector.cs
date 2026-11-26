using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival
{
    // Builds the podium locally from MatchResultStore on every peer and plays the rank motions.
    // No network spawn: the result is host-confirmed data and the models are pure presentation.
    public sealed class ResultSceneDirector : MonoBehaviour
    {
        [SerializeField] private ResultPodium podium;
        [SerializeField] private VictoryPoseCatalog catalog;
        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private GameObject placeholderModel;

        private readonly List<GameObject> spawned = new();
        private MatchResultStore store;

        private void Start()
        {
            store = FindFirstObjectByType<MatchResultStore>();
            if (store == null)
            {
                Debug.LogWarning("ResultSceneDirector: MatchResultStore not found; podium stays empty.");
                return;
            }
            store.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (store != null)
                store.Changed -= Refresh;
            ClearModels();
        }

        private void Refresh()
        {
            if (store == null || !store.IsReady)
                ClearModels();
            else
                Show(store.Entries);
        }

        public void Show(IReadOnlyList<MatchResultEntry> entries)
        {
            ClearModels();
            foreach (var entry in entries)
                Build(entry);
        }

        private void Build(MatchResultEntry entry)
        {
            var slot = podium != null ? podium.GetSlot(entry.Rank) : null;
            if (slot == null)
            {
                Debug.LogWarning($"ResultSceneDirector: no podium slot for rank {entry.Rank}.");
                return;
            }

            var definition = FindDefinition(entry.CharacterId);
            if (definition == null)
                Debug.LogWarning($"ResultSceneDirector: no CharacterDefinition for character {entry.CharacterId}.");

            var prefab = definition != null && definition.ResultModel != null ? definition.ResultModel : placeholderModel;
            if (prefab == null)
            {
                Debug.LogWarning($"ResultSceneDirector: no model for character {entry.CharacterId}.");
                return;
            }

            var model = Instantiate(prefab, slot.position, slot.rotation, slot);
            model.name = $"Rank{entry.Rank}_Character{entry.CharacterId}";
            spawned.Add(model);

            var clip = ResolveClip(entry.Rank, definition);
            if (!model.AddComponent<ResultMotionPlayer>().Play(clip))
                Debug.LogWarning($"ResultSceneDirector: no motion for rank {entry.Rank}; staying idle.");
        }

        private AnimationClip ResolveClip(int rank, CharacterDefinition definition)
        {
            if (catalog == null)
                return null;
            if (rank != 1)
                return catalog.DeathMotion;
            int index = definition != null ? definition.VictoryPoseIndex : 0;
            return catalog.TryGetVictoryPose(index, out var pose) ? pose : null;
        }

        private CharacterDefinition FindDefinition(int characterId)
        {
            if (characters != null)
                foreach (var definition in characters)
                    if (definition != null && definition.CharacterId == characterId)
                        return definition;
            return null;
        }

        private void ClearModels()
        {
            foreach (var model in spawned)
                if (model != null)
                    Destroy(model);
            spawned.Clear();
        }
    }
}
