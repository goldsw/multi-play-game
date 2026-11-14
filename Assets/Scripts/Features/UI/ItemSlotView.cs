using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    public sealed class ItemSlotView : MonoBehaviour
    {
        private PlayerItemInventory inventory;
        private Image highlight;
        private Image[] icons;

        public void Configure(Image stroke, Image water, Image apple, Image banana)
        {
            highlight = stroke;
            icons = new[] { water, apple, banana };
            for (int index = 0; index < icons.Length; index++)
                icons[index].sprite = ItemIcon.Create((ItemKind)(index + 1));
            Render();
        }

        public void Bind(PlayerItemInventory player)
        {
            inventory = player;
            Render();
        }

        public void Render()
        {
            var held = inventory != null ? inventory.Held : ItemKind.None;
            for (int index = 0; index < icons.Length; index++)
                icons[index].gameObject.SetActive((int)held == index + 1);
            highlight.enabled = held != ItemKind.None || (inventory != null && inventory.InUse);
        }

        private void OnDestroy()
        {
            if (icons == null)
                return;

            foreach (var icon in icons)
            {
                if (icon != null && icon.sprite != null)
                {
                    Destroy(icon.sprite.texture);
                    Destroy(icon.sprite);
                }
            }
        }
    }
}
