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
            if (scene.name == InGameSceneLoader.SceneName && lobbyPresentation != null)
                lobbyPresentation.SetActive(false);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == InGameSceneLoader.SceneName && lobbyPresentation != null)
                lobbyPresentation.SetActive(true);
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
