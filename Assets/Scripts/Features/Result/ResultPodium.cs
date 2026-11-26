using UnityEngine;

namespace MushroomSurvival
{
    // Empty Transform slots, one per rank (index 0 = 1st place). Art can move or replace them freely.
    public sealed class ResultPodium : MonoBehaviour
    {
        [SerializeField] private Transform[] rankSlots;

        public Transform GetSlot(int rank) =>
            rankSlots != null && rank >= 1 && rank <= rankSlots.Length ? rankSlots[rank - 1] : null;
    }
}
