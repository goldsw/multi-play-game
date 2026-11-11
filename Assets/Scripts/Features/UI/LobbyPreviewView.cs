using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyPreviewView : MonoBehaviour
    {
        [SerializeField] private GameObject[] previews;

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
