using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] private float distanceFromCamera = 1.1f;
        [SerializeField] private float verticalOffset = -0.05f;
        [SerializeField] private float menuScale = 0.0015f;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(1000f, 680f);
        [SerializeField] private Vector2 buttonSize = new Vector2(0f, 96f);

        [Header("Events")]
        [SerializeField] private UnityEvent onResumeRequested = new UnityEvent();
        [SerializeField] private UnityEvent onRestartRequested = new UnityEvent();
        [SerializeField] private UnityEvent onQuitRequested = new UnityEvent();

        private GameObject _menuRoot;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            EnsureCameraTransform();
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

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Sprite uiSprite = Resources.GetBuiltinResource<Sprite>("UISprite.psd");
            Sprite backgroundSprite = Resources.GetBuiltinResource<Sprite>("Background.psd");

            _menuRoot = CreateUiObject("Pause Menu", cameraTransform);
            _menuRoot.transform.localPosition = new Vector3(0f, verticalOffset, distanceFromCamera);
            _menuRoot.transform.localRotation = Quaternion.identity;
            _menuRoot.transform.localScale = Vector3.one * menuScale;

            Canvas canvas = _menuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cameraTransform.GetComponent<Camera>();
            _menuRoot.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20f;
            _menuRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

            _canvasGroup = _menuRoot.AddComponent<CanvasGroup>();

            RectTransform menuRect = _menuRoot.GetComponent<RectTransform>();
            menuRect.sizeDelta = panelSize;

            GameObject dimmer = CreateUiObject("Dimmer", _menuRoot.transform);
            RectTransform dimmerRect = dimmer.GetComponent<RectTransform>();
            StretchToFill(dimmerRect);
            Image dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.sprite = backgroundSprite;
            dimmerImage.type = Image.Type.Sliced;
            dimmerImage.color = new Color(0.04f, 0.07f, 0.11f, 0.94f);

            GameObject card = CreateUiObject("Card", _menuRoot.transform);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            StretchToFill(cardRect, 48f, 48f, 48f, 48f);
            Image cardImage = card.AddComponent<Image>();
            cardImage.sprite = uiSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.11f, 0.15f, 0.2f, 0.98f);

            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 56, 56);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = card.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            CreateLabel(
                "Title",
                card.transform,
                font,
                "PAUSED",
                56,
                FontStyle.Bold,
                new Color(0.95f, 0.98f, 1f, 1f),
                78f);

            CreateLabel(
                "Subtitle",
                card.transform,
                font,
                "Take a breath, then jump back in when you're ready.",
                28,
                FontStyle.Normal,
                new Color(0.72f, 0.8f, 0.88f, 1f),
                64f);

            CreateButton("Resume Button", card.transform, font, uiSprite, "Resume", new Color(0.17f, 0.52f, 0.42f, 1f), OnResumePressed);
            CreateButton("Restart Button", card.transform, font, uiSprite, "Restart Scene", new Color(0.82f, 0.47f, 0.16f, 1f), OnRestartPressed);
            CreateButton("Quit Button", card.transform, font, uiSprite, "Quit Game", new Color(0.66f, 0.22f, 0.24f, 1f), OnQuitPressed);
        }

        private void EnsureCameraTransform()
        {
            if (cameraTransform != null)
                return;

            Camera rigCamera = GetComponentInChildren<Camera>(true);
            if (rigCamera != null)
                cameraTransform = rigCamera.transform;
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

        private Button CreateButton(string objectName, Transform parent, Font font, Sprite sprite, string label, Color baseColor, UnityAction onPressed)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = buttonSize;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = buttonSize.y;

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
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
                34,
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
