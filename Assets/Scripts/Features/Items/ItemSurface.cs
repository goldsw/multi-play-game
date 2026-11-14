using UnityEngine;

namespace MushroomSurvival
{
    public enum ItemSurfaceKind
    {
        Ground,
        Bridge,
        Border,
        Other
    }

    public sealed class ItemSurface : MonoBehaviour
    {
        [SerializeField] private ItemSurfaceKind kind;

        public ItemSurfaceKind Kind => kind;

        public void Configure(ItemSurfaceKind value) => kind = value;
    }
}
