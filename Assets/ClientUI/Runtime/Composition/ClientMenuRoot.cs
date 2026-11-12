using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MushroomSurvival.ClientUI
{
    /// <summary>Composition root for the isolated, client-only menu demonstration scene.</summary>
    public sealed class ClientMenuRoot : MonoBehaviour
    {
        private const string ResourceCatalogName = "ClientMenuCatalog";
        [SerializeField] private ClientMenuCatalog catalog;

        private ClientMenuCatalog fallbackCatalog;

        private void Awake()
        {
            if (catalog == null) catalog = Resources.Load<ClientMenuCatalog>(ResourceCatalogName);
            if (catalog == null)
            {
                fallbackCatalog = ClientMenuCatalog.CreateFallback();
                catalog = fallbackCatalog;
            }

            EnsureEventSystem();
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (fallbackCatalog != null) Destroy(fallbackCatalog);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var eventSystemObject = new GameObject("ClientMenuEventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("ClientMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var profileStore = new ClientProfileStore(catalog);
            AudioListener.volume = profileStore.Profile.masterVolume;
            var economy = new ClientEconomyService(catalog, profileStore);
            var controller = canvasObject.AddComponent<ClientMenuController>();
            controller.Initialize(catalog, profileStore, economy);
        }
    }
}
