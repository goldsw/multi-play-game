using UnityEngine;

namespace MushroomSurvival
{
    public static class ItemIcon
    {
        private const int Size = 48;

        public static Sprite Create(ItemKind kind, bool readable = false)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = kind + " Icon" };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = ColorAt(kind, new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size));
            texture.SetPixels32(pixels);
            texture.Apply(false, !readable);
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), Vector2.one * 0.5f);
        }

        private static Color32 ColorAt(ItemKind kind, Vector2 point)
        {
            switch (kind)
            {
                case ItemKind.Water:
                    if (point.y is > 0.26f and < 0.72f &&
                        Mathf.Abs(point.x - 0.5f) < Mathf.Lerp(0.17f, 0.32f, (point.y - 0.26f) / 0.46f))
                        return new Color32(35, 149, 207, 255);
                    if (Mathf.Abs(Vector2.Distance(point, new Vector2(0.5f, 0.65f)) - 0.23f) < 0.03f &&
                        point.y > 0.68f)
                        return new Color32(54, 70, 80, 255);
                    break;
                case ItemKind.Apple:
                    if (Ellipse(point, 0.63f, 0.79f, 0.16f, 0.065f))
                        return new Color32(51, 128, 47, 255);
                    if (Mathf.Abs(point.x - 0.51f) < 0.026f && point.y is > 0.66f and < 0.87f)
                        return new Color32(107, 62, 38, 255);
                    if (Ellipse(point, 0.40f, 0.43f, 0.25f, 0.28f) ||
                        Ellipse(point, 0.59f, 0.43f, 0.25f, 0.28f))
                        return new Color32(232, 41, 52, 255);
                    break;
                case ItemKind.Banana:
                    if (Ellipse(point, 0.48f, 0.51f, 0.39f, 0.34f) &&
                        !Ellipse(point, 0.43f, 0.67f, 0.41f, 0.32f))
                        return new Color32(240, 191, 22, 255);
                    if (Ellipse(point, 0.82f, 0.62f, 0.07f, 0.04f))
                        return new Color32(138, 92, 34, 255);
                    break;
            }
            return new Color32(0, 0, 0, 0);
        }

        private static bool Ellipse(Vector2 point, float x, float y, float width, float height)
        {
            var delta = new Vector2((point.x - x) / width, (point.y - y) / height);
            return delta.sqrMagnitude < 1f;
        }
    }
}
