using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MushroomSurvival.Editor
{
    internal static class LobbyCanvasSetup
    {
        private static readonly Color Panel = new(0.12f, 0.13f, 0.13f, 0.95f);
        private static readonly Color Label = new(0.95f, 0.96f, 0.95f);
        private static readonly Color SecondaryLabel = new(0.82f, 0.86f, 0.86f);
        private static readonly string[] Characters = { "고양이", "오리", "강아지", "토끼", "다람쥐" };

        public static void Configure(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var view = roots.Select(root => root.GetComponent<LobbyView>()).Single(component => component != null);
            if (view.transform.Find("Lobby Canvas") == null)
                CreateCanvas(view);

            if (!roots.Any(root => root.GetComponentInChildren<EventSystem>(true) != null))
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(events, scene);
            }
        }

        private static void CreateCanvas(LobbyView view)
        {
            var root = new GameObject("Lobby Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(view.transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            var control = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var fontResources = new DefaultControls.Resources { standard = control, background = background };

            var status = CreatePanel(root.transform, "Room Status", new Vector2(0, 1), new Vector2(20, -20),
                new Vector2(420, 92), Panel, background);
            var heading = CreateText(status, "Heading", "방 코드", new Vector2(16, -8),
                new Vector2(388, 42), font, 24, Label, FontStyle.Bold);
            var playerCount = CreateText(status, "Player Count", "인원  0 / 4", new Vector2(16, -50),
                new Vector2(388, 30), font, 18, SecondaryLabel);
            var messagePanel = CreatePanel(root.transform, "Session Message", new Vector2(0, 1),
                new Vector2(20, -124), new Vector2(420, 82), Panel, background);
            var message = CreateText(messagePanel, "Message", "", new Vector2(16, -8),
                new Vector2(388, 66), font, 18, Label);
            messagePanel.gameObject.SetActive(false);

            var room = CreateButton(root.transform, "Room Action", "방 찾기", new Vector2(0, 0),
                new Vector2(20, 20), new Vector2(172, 50), font, fontResources);
            var start = CreateButton(root.transform, "Start Game", "게임 시작", new Vector2(1, 0),
                new Vector2(-20, 20), new Vector2(172, 50), font, fontResources);
            var characters = new List<Button>(Characters.Length);
            var row = CreateRect(root.transform, "Character Selection", new Vector2(0.5f, 0),
                new Vector2(0, 22), new Vector2(520, 46));
            for (int index = 0; index < Characters.Length; index++)
            {
                characters.Add(CreateButton(row, Characters[index], Characters[index], new Vector2(0, 0),
                    new Vector2(index * 105, 0), new Vector2(100, 46), font, fontResources));
            }

            var overlay = new GameObject("Join Room Popup", typeof(RectTransform), typeof(Image));
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.SetParent(root.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.24f);

            var dialog = CreatePanel(overlayRect, "Join Dialog", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(420, 200), Panel, background);
            CreateText(dialog, "Dialog Title", "참가할 방 코드", new Vector2(24, -14),
                new Vector2(372, 42), font, 24, Label, FontStyle.Bold);

            var inputObject = DefaultControls.CreateInputField(fontResources);
            inputObject.name = "Join Code Input";
            Place(inputObject.GetComponent<RectTransform>(), dialog, new Vector2(0, 1),
                new Vector2(24, -65), new Vector2(372, 42));
            inputObject.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.14f);
            var input = inputObject.GetComponent<InputField>();
            input.characterLimit = 32;
            input.lineType = InputField.LineType.SingleLine;
            input.textComponent.font = font;
            input.textComponent.fontSize = 22;
            input.textComponent.color = Label;
            var placeholder = (Text)input.placeholder;
            placeholder.font = font;
            placeholder.text = "";
            input.customCaretColor = true;
            input.caretColor = Label;

            var cancel = CreateButton(dialog, "Cancel", "취소", new Vector2(0, 0),
                new Vector2(24, 25), new Vector2(178, 45), font, fontResources);
            var join = CreateButton(dialog, "Join", "참가", new Vector2(1, 0),
                new Vector2(-24, 25), new Vector2(178, 45), font, fontResources);
            join.interactable = false;
            overlay.SetActive(false);
            view.ConfigureCanvas(canvas, heading, playerCount, message, room, start, characters.ToArray(),
                overlay, input, cancel, join);
            EditorUtility.SetDirty(view);
        }

        private static RectTransform CreateRect(Transform parent, string name,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            Place(rect, parent, anchor, position, size);
            return rect;
        }

        private static RectTransform CreatePanel(Transform parent, string name,
            Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var rect = CreateRect(parent, name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 position,
            Vector2 size, Font font, int fontSize, Color color, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateRect(parent, name, new Vector2(0, 1), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = value;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string value,
            Vector2 anchor, Vector2 position, Vector2 size, Font font, DefaultControls.Resources resources)
        {
            var root = DefaultControls.CreateButton(resources);
            root.name = name;
            Place(root.GetComponent<RectTransform>(), parent, anchor, position, size);
            var button = root.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.23f, 0.26f, 0.26f);
            colors.highlightedColor = new Color(0.35f, 0.41f, 0.40f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.16f, 0.19f, 0.19f);
            colors.disabledColor = new Color(0.29f, 0.32f, 0.32f, 0.65f);
            button.colors = colors;
            var label = root.GetComponentInChildren<Text>();
            label.font = font;
            label.fontSize = 20;
            label.color = Label;
            label.text = value;
            label.raycastTarget = false;
            return button;
        }

        private static void Place(RectTransform rect, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
