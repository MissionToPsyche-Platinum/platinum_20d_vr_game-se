using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [SerializeField] private Vector2 panelSize = new Vector2(760f, 560f);
        [SerializeField] private Vector2 buttonSize = new Vector2(0f, 58f);

        [Header("Theme")]
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private Sprite roundedPanelSprite;
        [SerializeField] private Sprite roundedOutlineSprite;
        [SerializeField] private Color overlayTint = new Color(0.03f, 0.04f, 0.06f, 0.36f);
        [SerializeField] private Color shadowTint = new Color(0.01f, 0.01f, 0.02f, 0.22f);
        [SerializeField] private Color outlineTint = new Color(0.66f, 0.72f, 0.8f, 0.35f);
        [SerializeField] private Color panelTint = new Color(0.1f, 0.12f, 0.16f, 0.96f);
        [SerializeField] private Color titleTint = new Color(0.95f, 0.96f, 0.98f, 1f);
        [SerializeField] private Color resumeButtonTint = new Color(0.18f, 0.22f, 0.27f, 1f);
        [SerializeField] private Color quitButtonTint = new Color(0.18f, 0.22f, 0.27f, 1f);
        [SerializeField] private Color buttonTextTint = new Color(0.95f, 0.96f, 0.98f, 1f);

        [Header("Events")]
        [SerializeField] private UnityEvent onResumeRequested = new UnityEvent();
        [SerializeField] private UnityEvent onRestartRequested = new UnityEvent();
        [SerializeField] private UnityEvent onQuitRequested = new UnityEvent();

        private GameObject _menuRoot;
        private CanvasGroup _canvasGroup;
        private InputAction _pauseToggleAction;
        private float _timeScaleBeforePause = 1f;

        private void Awake()
        {
            EnsureCameraTransform();
            EnsureEventSystem();
            EnsurePauseToggleAction();
            BuildMenuIfNeeded();
        }

        private void Start()
        {
            SetMenuVisible(showOnStart);
        }

        private void OnEnable()
        {
            EnsurePauseToggleAction();

            if (_pauseToggleAction == null)
                return;

            _pauseToggleAction.performed += OnPauseTogglePerformed;
            _pauseToggleAction.Enable();
        }

        private void OnDisable()
        {
            if (_pauseToggleAction == null)
                return;

            _pauseToggleAction.performed -= OnPauseTogglePerformed;
            _pauseToggleAction.Disable();
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
            get { return _canvasGroup != null && _canvasGroup.alpha > 0.99f; }
        }

        private void SetMenuVisible(bool isVisible)
        {
            BuildMenuIfNeeded();

            if (_menuRoot == null)
                return;

            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (isVisible)
                PauseGameplay();
            else
                ResumeGameplay();
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
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            RectTransform menuRect = _menuRoot.GetComponent<RectTransform>();
            menuRect.sizeDelta = panelSize;

            GameObject dimmer = CreateUiObject("Dimmer", _menuRoot.transform);
            StretchToFill(dimmer.GetComponent<RectTransform>());
            Image dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = overlayTint;

            GameObject shadow = CreateUiObject("Shadow", _menuRoot.transform);
            RectTransform shadowRect = shadow.GetComponent<RectTransform>();
            StretchToFill(shadowRect, 54f, 42f, 40f, 28f);
            shadowRect.anchoredPosition = new Vector2(6f, -8f);
            Image shadowImage = shadow.AddComponent<Image>();
            ApplySlicedSprite(shadowImage, roundedPanelSprite);
            shadowImage.color = shadowTint;

            GameObject outline = CreateUiObject("Outline", _menuRoot.transform);
            StretchToFill(outline.GetComponent<RectTransform>(), 42f, 42f, 28f, 28f);
            Image outlineImage = outline.AddComponent<Image>();
            ApplySlicedSprite(outlineImage, roundedOutlineSprite);
            outlineImage.color = outlineTint;

            GameObject card = CreateUiObject("Card", _menuRoot.transform);
            StretchToFill(card.GetComponent<RectTransform>(), 48f, 48f, 34f, 34f);
            Image cardImage = card.AddComponent<Image>();
            ApplySlicedSprite(cardImage, roundedPanelSprite);
            cardImage.color = panelTint;

            GameObject topSheen = CreateUiObject("Top Sheen", card.transform);
            RectTransform topSheenRect = topSheen.GetComponent<RectTransform>();
            topSheenRect.anchorMin = new Vector2(0f, 1f);
            topSheenRect.anchorMax = new Vector2(1f, 1f);
            topSheenRect.pivot = new Vector2(0.5f, 1f);
            topSheenRect.sizeDelta = new Vector2(-42f, 4f);
            topSheenRect.anchoredPosition = new Vector2(0f, -16f);
            Image topSheenImage = topSheen.AddComponent<Image>();
            topSheenImage.color = new Color(1f, 1f, 1f, 0.03f);

            GameObject content = CreateUiObject("Content", card.transform);
            StretchToFill(content.GetComponent<RectTransform>(), 42f, 42f, 34f, 34f);

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            BuildHeader(content.transform);

            CreateButton("Resume Button", content.transform, "Resume", resumeButtonTint, OnResumePressed);
            CreateButton("Quit Button", content.transform, "Quit Game", quitButtonTint, OnQuitPressed);
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = CreateUiObject("Header", parent);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 48f;

            CreateLabel(
                "Title",
                header.transform,
                "PAUSED",
                26f,
                FontStyles.Bold,
                titleTint,
                new Vector2(0f, 40f),
                TextAlignmentOptions.Center);
        }

        private void EnsureCameraTransform()
        {
            if (cameraTransform != null)
                return;

            Camera rigCamera = GetComponentInChildren<Camera>(true);
            if (rigCamera != null)
                cameraTransform = rigCamera.transform;
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

        private void EnsurePauseToggleAction()
        {
            if (_pauseToggleAction != null)
                return;

            _pauseToggleAction = new InputAction("Pause Toggle", InputActionType.Button);
            _pauseToggleAction.AddBinding("<Keyboard>/escape");
            _pauseToggleAction.AddBinding("<Gamepad>/start");
            _pauseToggleAction.AddBinding("<XRController>{LeftHand}/menuButton");
            _pauseToggleAction.AddBinding("<XRController>{RightHand}/menuButton");
        }

        private void OnPauseTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleMenu();
        }

        private void PauseGameplay()
        {
            if (Mathf.Approximately(Time.timeScale, 0f))
                return;

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }

        private void ResumeGameplay()
        {
            if (!Mathf.Approximately(Time.timeScale, 0f))
                return;

            Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
        }

        private void OnResumePressed()
        {
            HideMenu();
            onResumeRequested.Invoke();
        }

        private void OnQuitPressed()
        {
            onQuitRequested.Invoke();
            ResumeGameplay();

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private Button CreateButton(string objectName, Transform parent, string label, Color baseColor, UnityAction onPressed)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = buttonSize;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = buttonSize.y;
            layoutElement.minHeight = buttonSize.y;

            Image image = buttonObject.AddComponent<Image>();
            ApplySlicedSprite(image, roundedPanelSprite);
            image.color = baseColor;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.08f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.08f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
            button.colors = colors;
            button.onClick.AddListener(onPressed);

            CreateLabel(
                "Label",
                buttonObject.transform,
                label,
                19f,
                FontStyles.Bold,
                buttonTextTint,
                Vector2.zero,
                TextAlignmentOptions.Center);

            return button;
        }

        private TextMeshProUGUI CreateLabel(
            string objectName,
            Transform parent,
            string content,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            Vector2 preferredSize,
            TextAlignmentOptions alignment)
        {
            GameObject labelObject = CreateUiObject(objectName, parent);
            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();

            if (preferredSize.y > 0f)
            {
                LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
                if (preferredSize.x > 0f)
                    layoutElement.preferredWidth = preferredSize.x;

                layoutElement.preferredHeight = preferredSize.y;
                layoutElement.minHeight = preferredSize.y;
            }
            else
            {
                StretchToFill(rectTransform);
            }

            TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(8f, 0f, 8f, 0f);
            text.characterSpacing = 0.5f;
            text.lineSpacing = -4f;

            return text;
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

        private static void ApplySlicedSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
    }
}
