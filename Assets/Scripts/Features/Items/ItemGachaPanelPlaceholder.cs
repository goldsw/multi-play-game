using System;
using System.Collections;
using UnityEngine;

namespace MushroomSurvival
{
    /// <summary>Temporary panel: shows the result icon right away. Replaced by the scrolling panel.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemGachaPanelPlaceholder : MonoBehaviour, IItemGachaPanel
    {
        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private SpriteRenderer icon;
        [SerializeField, Min(0f)] private float holdSeconds = 0.5f;

        public event Action<ItemKind> Finished;

        public void Configure(ItemCatalog itemCatalog, SpriteRenderer renderer)
        {
            catalog = itemCatalog;
            icon = renderer;
        }

        public void LetGacha(ItemKind result)
        {
            StopAllCoroutines();
            if (icon != null)
            {
                icon.sprite = catalog != null && catalog.TryGetSprite(result, out var sprite) ? sprite : null;
                icon.enabled = icon.sprite != null;
            }
            StartCoroutine(Hold(result));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (icon != null)
                icon.enabled = false;
        }

        private IEnumerator Hold(ItemKind result)
        {
            yield return new WaitForSeconds(holdSeconds);
            Finished?.Invoke(result);
        }
    }
}
