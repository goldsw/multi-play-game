using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    public sealed class LobbyPreviewView : MonoBehaviour
    {
        [SerializeField] private GameObject[] previews;
        [SerializeField] private GameObject lobbyPresentation;

        public void ConfigurePresentation(GameObject presentation) => lobbyPresentation = presentation;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsMatchScene(scene) && lobbyPresentation != null)
                lobbyPresentation.SetActive(false);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // InGame -> Result unloads InGame while Result is loaded: keep the lobby camera off until both are gone.
            if (IsMatchScene(scene) && lobbyPresentation != null && !IsLoaded(InGameSceneLoader.SceneName, scene) &&
                !IsLoaded(ResultSceneLoader.SceneName, scene))
                lobbyPresentation.SetActive(true);
        }

        private static bool IsMatchScene(Scene scene) =>
            scene.name == InGameSceneLoader.SceneName || scene.name == ResultSceneLoader.SceneName;

        private static bool IsLoaded(string name, Scene ignored)
        {
            var scene = SceneManager.GetSceneByName(name);
            return scene.IsValid() && scene.isLoaded && scene != ignored;
        }

        public void Configure(GameObject[] slotPreviews)
        {
            previews = slotPreviews;
            Show(null);
        }

        public void Show(SessionData state)
        {
            if (previews == null)
                return;

            var slots = state?.Slots;
            bool showPreviews = state?.Status == SessionStatus.Lobby && slots != null;
            for (int index = 0; index < previews.Length; index++)
            {
                if (previews[index] != null)
                    previews[index].SetActive(showPreviews && index < slots.Count && slots[index].Occupied);
            }
        }
    }
}
