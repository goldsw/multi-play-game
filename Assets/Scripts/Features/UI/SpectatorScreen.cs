using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MushroomSurvival
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class SpectatorScreen : MonoBehaviour
    {
        private GameObject overlay;
        private Text rankLabel;
        private Text watchingLabel;
        private Font koreanFont;
        private Sprite gradient;
        private Volume volume;
        private VolumeProfile profile;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPostProcessing;
        private LayerMask previousVolumeMask;

        public void Show(int rank)
        {
            if (overlay == null)
                CreateOverlay();
            rankLabel.text = $"{rank}등";
            if (overlay.activeSelf)
                return;

            if (volume == null)
                CreateGrayscaleVolume();
            previousPostProcessing = cameraData.renderPostProcessing;
            previousVolumeMask = cameraData.volumeLayerMask;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = previousVolumeMask.value | (1 << volume.gameObject.layer);
            volume.enabled = true;
            overlay.SetActive(true);
        }

        public void SetWatching(string text)
        {
            if (overlay == null)
                CreateOverlay();
            watchingLabel.text = text;
        }

        public void Hide()
        {
            if (overlay == null || !overlay.activeSelf)
                return;

            overlay.SetActive(false);
            if (volume != null)
                volume.enabled = false;
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = previousPostProcessing;
                cameraData.volumeLayerMask = previousVolumeMask;
            }
        }

        private void CreateGrayscaleVolume()
        {
            cameraData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            var effects = new GameObject("Spectator Grayscale");
            effects.transform.SetParent(transform, false);
            volume = effects.AddComponent<Volume>();
            volume.enabled = false;
            volume.isGlobal = true;
            volume.priority = 100f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colors = profile.Add<ColorAdjustments>();
            colors.saturation.Override(-100f);
            volume.sharedProfile = profile;
        }

        private void CreateOverlay()
        {
            overlay = new GameObject("Spectator Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
            overlay.SetActive(false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var banner = new GameObject("Rank Gradient", typeof(RectTransform), typeof(Image));
            var bannerRect = banner.GetComponent<RectTransform>();
            bannerRect.SetParent(overlay.transform, false);
            bannerRect.anchorMin = new Vector2(0f, 1f);
            bannerRect.anchorMax = Vector2.one;
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = Vector2.zero;
            bannerRect.sizeDelta = new Vector2(0f, 160f);
            gradient = CreateGradient();
            var image = banner.GetComponent<Image>();
            image.sprite = gradient;
            image.raycastTarget = false;

            var label = new GameObject("Rank", typeof(RectTransform), typeof(Text));
            var rect = label.GetComponent<RectTransform>();
            rect.SetParent(bannerRect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -20f);
            rect.sizeDelta = new Vector2(500f, 68f);
            koreanFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 42);
            rankLabel = label.GetComponent<Text>();
            rankLabel.font = koreanFont != null ? koreanFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rankLabel.fontSize = 42;
            rankLabel.fontStyle = FontStyle.Bold;
            rankLabel.alignment = TextAnchor.MiddleCenter;
            rankLabel.color = Color.white;
            rankLabel.raycastTarget = false;

            var watching = new GameObject("Watching", typeof(RectTransform), typeof(Text));
            var watchingRect = watching.GetComponent<RectTransform>();
            watchingRect.SetParent(bannerRect, false);
            watchingRect.anchorMin = watchingRect.anchorMax = watchingRect.pivot = new Vector2(0.5f, 1f);
            watchingRect.anchoredPosition = new Vector2(0f, -92f);
            watchingRect.sizeDelta = new Vector2(500f, 34f);
            watchingLabel = watching.GetComponent<Text>();
            watchingLabel.font = rankLabel.font;
            watchingLabel.fontSize = 22;
            watchingLabel.alignment = TextAnchor.MiddleCenter;
            watchingLabel.color = new Color(1f, 1f, 1f, 0.85f);
            watchingLabel.raycastTarget = false;
        }

        private static Sprite CreateGradient()
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
                texture.SetPixel(0, y, new Color(0f, 0f, 0f, 0.92f * y / (height - 1)));
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            if (overlay != null)
                Destroy(overlay);
            if (koreanFont != null)
                Destroy(koreanFont);
            if (gradient != null)
            {
                Destroy(gradient.texture);
                Destroy(gradient);
            }
            if (profile != null)
                Destroy(profile);
        }
    }
}
