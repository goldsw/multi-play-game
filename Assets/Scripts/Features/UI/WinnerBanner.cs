using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MushroomSurvival
{
    // Big centered "우승!" text with a pop-in animation. Owns its own Canvas in the InGame scene.
    [DisallowMultipleComponent]
    public sealed class WinnerBanner : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float popSeconds = 0.45f;

        private GameObject overlay;
        private RectTransform labelRect;
        private Text label;
        private Font koreanFont;
        private float shownAt;

        public bool IsVisible => overlay != null && overlay.activeSelf;

        public void Show(string text)
        {
            if (overlay == null)
                CreateOverlay();
            label.text = text;
            shownAt = Time.unscaledTime;
            Apply(0f);
            overlay.SetActive(true);
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.SetActive(false);
        }

        private void Update()
        {
            if (IsVisible)
                Apply((Time.unscaledTime - shownAt) / popSeconds);
        }

        // Overshoot scale-in (ease-out-back) with a quick fade.
        private void Apply(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            labelRect.localScale = Vector3.one * Mathf.Max(0.01f, eased);
            var color = label.color;
            color.a = Mathf.Clamp01(t * 3f);
            label.color = color;
        }

        private void CreateOverlay()
        {
            overlay = new GameObject("Winner Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
            overlay.SetActive(false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var text = new GameObject("Winner", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelRect = text.GetComponent<RectTransform>();
            labelRect.SetParent(overlay.transform, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(900f, 240f);
            koreanFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 160);
            label = text.GetComponent<Text>();
            label.font = koreanFont != null ? koreanFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 160;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = new Color(1f, 0.86f, 0.2f, 1f);
            label.raycastTarget = false;
            var outline = text.GetComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.1f, 0f, 0.9f);
            outline.effectDistance = new Vector2(6f, -6f);
        }

        private void OnDestroy()
        {
            if (overlay != null)
                Destroy(overlay);
            if (koreanFont != null)
                Destroy(koreanFont);
        }
    }
}
