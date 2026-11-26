using System;
using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    internal sealed class CooldownHudLayout : MonoBehaviour
    {
        private const int SpriteSize = 96;
        private Sprite disc;
        private Sprite ring;

        public static GameObject Create(Transform parent, Action onSkillPressed, Action onItemPressed,
            out CooldownSlot skill, out CooldownSlot item, out ItemSlotView itemView)
        {
            var root = new GameObject("In Game Cooldowns", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var layout = root.AddComponent<CooldownHudLayout>();
            layout.disc = CreateSprite("Slot Disc", (x, y) => Coverage(0.46f - new Vector2(x, y).magnitude));
            layout.ring = CreateSprite("Cooldown Ring", (x, y) =>
            {
                float distance = new Vector2(x, y).magnitude;
                return Coverage(Mathf.Min(distance - 0.41f, 0.49f - distance));
            });
            skill = layout.CreateSlot("Skill Slot", 76f, 105f, onSkillPressed);
            item = layout.CreateSlot("Item Slot", 60f, 39f, onItemPressed);
            var itemRect = (RectTransform)item.transform;
            var stroke = AddImage(itemRect, "Item Held or in Use", layout.ring, new Color32(208, 26, 50, 255), 1f);
            itemView = item.gameObject.AddComponent<ItemSlotView>();
            itemView.Configure(stroke,
                AddImage(itemRect, "Water Icon", null, Color.white, 0.62f),
                AddImage(itemRect, "Apple Icon", null, Color.white, 0.62f),
                AddImage(itemRect, "Banana Icon", null, Color.white, 0.62f));
            return root;
        }

        private CooldownSlot CreateSlot(string name, float size, float right, Action onPressed)
        {
            var slot = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)slot.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, 52f);
            rect.sizeDelta = Vector2.one * size;

            AddImage(rect, "Disc", disc, new Color32(251, 251, 247, 255), 1f).raycastTarget = true;
            slot.AddComponent<CooldownSlotPress>().Configure(onPressed);
            AddImage(rect, "Ring Track", ring, new Color32(91, 92, 96, 255), 1f);
            var fill = AddImage(rect, "Cooldown Fill", ring, new Color32(208, 26, 50, 255), 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            var icon = AddImage(rect, "Icon", null, Color.white, 0.58f);
            icon.preserveAspect = true;

            slot.AddComponent<CooldownTimer>().Configure(fill);
            var view = slot.AddComponent<CooldownSlot>();
            view.Configure(icon);
            return view;
        }

        private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color, float scale)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.one * ((1f - scale) * 0.5f);
            rect.anchorMax = Vector2.one - rect.anchorMin;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static float Coverage(float distance) => Mathf.Clamp01(distance * SpriteSize + 0.5f);

        private static Sprite CreateSprite(string name, Func<float, float, float> coverage)
        {
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false) { name = name };
            var pixels = new Color32[SpriteSize * SpriteSize];
            for (int y = 0; y < SpriteSize; y++)
            for (int x = 0; x < SpriteSize; x++)
            {
                float alpha = coverage((x + 0.5f) / SpriteSize - 0.5f, (y + 0.5f) / SpriteSize - 0.5f);
                pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), Vector2.one * 0.5f);
        }

        private void OnDestroy()
        {
            DestroySprite(disc);
            DestroySprite(ring);
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            Destroy(sprite.texture);
            Destroy(sprite);
        }
    }
}
