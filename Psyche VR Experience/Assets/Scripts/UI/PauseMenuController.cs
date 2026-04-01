using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PsycheVR.UI
{
    [DisallowMultipleComponent]
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private bool showOnStart;
        [SerializeField] private float distanceFromCamera = 1.35f;
        [SerializeField] private float verticalOffset = -0.02f;
        [SerializeField] private float menuScale = 0.0011f;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(760f, 520f);
        [SerializeField] private Vector2 buttonSize = new Vector2(0f, 72f);

        [Header("Events")]
        [SerializeField] private UnityEvent onResumeRequested = new UnityEvent();
        [SerializeField] private UnityEvent onRestartRequested = new UnityEvent();
        [SerializeField] private UnityEvent onQuitRequested = new UnityEvent();

        private GameObject _menuRoot;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            EnsureCameraTransform();
            EnsureEventSystem();
            BuildMenuIfNeeded();
        }

        private void Start()
        {
            SetMenuVisible(showOnStart);
        }

        public void ToggleMenu()
        {
            SetMenuVisible(!IsMenuVisible);
        }

        public void ShowMenu()
        {
            SetMenuVisible(true);
        }

        public void HideMenu()
        {
            SetMenuVisible(false);
        }

        public bool IsMenuVisible
        {
            get { return _menuRoot != null && _menuRoot.activeSelf; }
        }

        private void SetMenuVisible(bool isVisible)
        {
            BuildMenuIfNeeded();

            if (_menuRoot == null)
                return;

            _menuRoot.SetActive(isVisible);

            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;
        }

        private void BuildMenuIfNeeded()
        {
            if (_menuRoot != null)
                return;

            EnsureCameraTransform();

            if (cameraTransform == null)
            {
                Debug.LogWarning("PauseMenuController could not find a camera transform.");
                return;
            }

            Font font = LoadBuiltInFont();
            _menuRoot = CreateUiObject("Pause Menu", cameraTransform);
            _menuRoot.transform.localPosition = new Vector3(0f, verticalOffset, distanceFromCamera);
            _menuRoot.transform.localRotation = Quaternion.identity;
            _menuRoot.transform.localScale = Vector3.one * menuScale;

            Canvas canvas = _menuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cameraTransform.GetComponent<Camera>();
            _menuRoot.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 24f;
            _menuRoot.AddComponent<GraphicRaycaster>();
            _menuRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

            _canvasGroup = _menuRoot.AddComponent<CanvasGroup>();

            RectTransform menuRect = _menuRoot.GetComponent<RectTransform>();
            menuRect.sizeDelta = panelSize;

            GameObject dimmer = CreateUiObject("Dimmer", _menuRoot.transform);
            RectTransform dimmerRect = dimmer.GetComponent<RectTransform>();
            StretchToFill(dimmerRect);
            Image dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = new Color(0.02f, 0.04f, 0.08f, 0.78f);

            GameObject card = CreateUiObject("Card", _menuRoot.transform);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            StretchToFill(cardRect, 62f, 62f, 58f, 58f);
            Image cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.09f, 0.12f, 0.17f, 0.96f);

            GameObject accent = CreateUiObject("Accent", card.transform);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 10f);
            accentRect.anchoredPosition = Vector2.zero;
            Image accentImage = accent.AddComponent<Image>();
            accentImage.color = new Color(0.27f, 0.71f, 0.86f, 1f);

            GameObject content = CreateUiObject("Content", card.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            StretchToFill(contentRect, 34f, 34f, 42f, 28f);

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 28, 22);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            CreateLabel(
                "Title",
                content.transform,
                font,
                "PAUSED",
                44,
                FontStyle.Bold,
                new Color(0.95f, 0.98f, 1f, 1f),
                58f);

            CreateLabel(
                "Subtitle",
                content.transform,
                font,
                "Take a breath, then jump back in when you're ready.",
                20,
                FontStyle.Normal,
                new Color(0.72f, 0.8f, 0.88f, 1f),
                46f);

            CreateButton("Resume Button", content.transform, font, "Resume", new Color(0.14f, 0.46f, 0.33f, 1f), OnResumePressed);
            CreateButton("Restart Button", content.transform, font, "Restart Scene", new Color(0.73f, 0.38f, 0.12f, 1f), OnRestartPressed);
            CreateButton("Quit Button", content.transform, font, "Quit Game", new Color(0.56f, 0.16f, 0.18f, 1f), OnQuitPressed);
        }

        private void EnsureCameraTransform()
        {
            if (cameraTransform != null)
                return;

            Camera rigCamera = GetComponentInChildren<Camera>(true);
            if (rigCamera != null)
                cameraTransform = rigCamera.transform;
        }

        private static Font LoadBuiltInFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (font == null)
                Debug.LogWarning("PauseMenuController could not load LegacyRuntime.ttf.");

            return font;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();

                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystemObject);
        }

        private void OnResumePressed()
        {
            HideMenu();
            onResumeRequested.Invoke();
        }

        private void OnRestartPressed()
        {
            onRestartRequested.Invoke();
        }

        private void OnQuitPressed()
        {
            onQuitRequested.Invoke();
        }

        private Button CreateButton(string objectName, Transform parent, Font font, string label, Color baseColor, UnityAction onPressed)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = buttonSize;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = buttonSize.y;

            Image image = buttonObject.AddComponent<Image>();
            image.color = baseColor;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
            button.colors = colors;
            button.onClick.AddListener(onPressed);

            CreateLabel(
                "Label",
                buttonObject.transform,
                font,
                label,
                26,
                FontStyle.Bold,
                Color.white,
                0f);

            return button;
        }

        private GameObject CreateLabel(
            string objectName,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            float preferredHeight)
        {
            GameObject labelObject = CreateUiObject(objectName, parent);
            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();

            if (preferredHeight > 0f)
            {
                LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = preferredHeight;
                layoutElement.minHeight = preferredHeight;
            }
            else
            {
                StretchToFill(rectTransform);
            }

            Text text = labelObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return labelObject;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void StretchToFill(RectTransform rectTransform)
        {
            StretchToFill(rectTransform, 0f, 0f, 0f, 0f);
        }

        private static void StretchToFill(RectTransform rectTransform, float left, float right, float top, float bottom)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
