using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class ItemMap : MonoBehaviour
    {
        [SerializeField] private Collider prototypeGround;
        [SerializeField, Min(0.1f)] private float tileSize = 1f;

        private readonly Dictionary<Collider, ItemSurfaceKind> surfaces = new();
        private readonly List<Vector3> groundTiles = new();

        public float TileSize => tileSize;

        private void Awake() => Rebuild();

        public void Configure(Collider ground, float size)
        {
            prototypeGround = ground;
            tileSize = Mathf.Max(0.1f, size);
            if (Application.isPlaying)
                Rebuild();
        }

        public void Rebuild()
        {
            surfaces.Clear();
            groundTiles.Clear();

            foreach (ItemSurface marker in FindObjectsByType<ItemSurface>(FindObjectsSortMode.None))
            {
                if (marker.gameObject.scene != gameObject.scene)
                    continue;

                foreach (Collider collider in marker.GetComponentsInChildren<Collider>())
                {
                    if (collider.GetComponentInParent<ItemSurface>() == marker)
                        surfaces.Add(collider, marker.Kind);
                }
            }

            if (prototypeGround != null && !surfaces.ContainsKey(prototypeGround))
                surfaces.Add(prototypeGround, ItemSurfaceKind.Ground);

            Physics.SyncTransforms();
            foreach (var surface in surfaces)
            {
                if (surface.Value == ItemSurfaceKind.Ground)
                    AddGroundTiles(surface.Key.bounds);
            }
        }

        public bool TryGetSurface(Vector3 world, out Vector3 surface)
        {
            return TryGetSurface(world, out surface, out _);
        }

        public bool TryGetRandomGround(out Vector3 point)
        {
            return TryGetRandomGround(null, out point);
        }

        internal bool TryGetRandomGround(System.Predicate<Vector3> available, out Vector3 point)
        {
            int count = groundTiles.Count;
            int start = Random.Range(0, count);
            for (int offset = 0; offset < count; offset++)
            {
                Vector3 candidate = groundTiles[(start + offset) % count];
                if (TryGetSurface(candidate, out point, out var kind) && kind == ItemSurfaceKind.Ground
                    && (available == null || available(point)))
                    return true;
            }

            point = default;
            return false;
        }

        public bool TryGetPlayerFloor(Collider player, out Vector3 position)
        {
            position = default;
            if (player == null)
                return false;

            Bounds bounds = player.bounds;
            Vector3 feet = new(bounds.center.x, bounds.min.y, bounds.center.z);
            return TryGetSurface(feet, out position)
                && Mathf.Abs(feet.y - position.y) <= Mathf.Min(0.3f, 0.2f * TileSize);
        }

        public bool IsBorder(Collider collider)
        {
            return collider != null && surfaces.TryGetValue(collider, out var kind)
                && kind == ItemSurfaceKind.Border;
        }

        private bool TryGetSurface(Vector3 world, out Vector3 point, out ItemSurfaceKind kind)
        {
            point = default;
            kind = ItemSurfaceKind.Other;
            float reach = Mathf.Min(0.6f, 0.5f * TileSize);
            Ray ray = new(world + Vector3.up * reach, Vector3.down);
            float closest = reach * 2f;
            bool found = false;

            foreach (var surface in surfaces)
            {
                Collider collider = surface.Key;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
                    || !collider.Raycast(ray, out RaycastHit hit, closest) || hit.normal.y < 0.5f)
                    continue;

                ItemSurfaceKind hitKind = surface.Value;
                if (found && Mathf.Approximately(hit.distance, closest)
                    && SurfacePriority(kind) >= SurfacePriority(hitKind))
                    continue;

                found = true;
                closest = hit.distance;
                kind = hitKind;
                point = hit.point;
            }

            return kind == ItemSurfaceKind.Ground || kind == ItemSurfaceKind.Bridge;
        }

        private static int SurfacePriority(ItemSurfaceKind kind)
        {
            return kind switch
            {
                ItemSurfaceKind.Border => 3,
                ItemSurfaceKind.Other => 2,
                ItemSurfaceKind.Bridge => 1,
                _ => 0
            };
        }

        private void AddGroundTiles(Bounds bounds)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / TileSize + 0.01f));
            int rows = Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / TileSize + 0.01f));
            Vector3 first = new(
                bounds.center.x - (columns - 1) * TileSize * 0.5f,
                bounds.max.y,
                bounds.center.z - (rows - 1) * TileSize * 0.5f);

            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++)
                    groundTiles.Add(first + new Vector3(column * TileSize, 0f, row * TileSize));
            }
        }
    }
}
