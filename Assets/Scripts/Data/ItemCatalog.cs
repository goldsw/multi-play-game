using System;
using UnityEngine;

namespace MushroomSurvival
{
    /// <summary>
    /// Single source for item box content: the host rolls from the weights and every client draws the sprites.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Mushroom Survival/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemKind kind;
            public Sprite sprite;
            [Min(0f)] public float weight;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries.Length;
        public Entry this[int index] => entries[index];

        public bool TryGetSprite(ItemKind kind, out Sprite sprite)
        {
            foreach (var entry in entries)
            {
                if (entry.kind == kind && entry.sprite != null)
                {
                    sprite = entry.sprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }

        /// <summary>Picks an item by weight. <paramref name="unit"/> is a uniform value in [0, 1).</summary>
        public ItemKind Roll(float unit)
        {
            float total = 0f;
            foreach (var entry in entries)
                total += entry.kind != ItemKind.None ? Mathf.Max(0f, entry.weight) : 0f;
            if (total <= 0f)
                return ItemKind.None;

            float target = Mathf.Clamp01(unit) * total;
            ItemKind last = ItemKind.None;
            foreach (var entry in entries)
            {
                float weight = entry.kind != ItemKind.None ? Mathf.Max(0f, entry.weight) : 0f;
                if (weight <= 0f)
                    continue;
                last = entry.kind;
                if (target < weight)
                    return entry.kind;
                target -= weight;
            }

            return last;
        }

        public void Configure(Entry[] values) => entries = values;
    }
}
