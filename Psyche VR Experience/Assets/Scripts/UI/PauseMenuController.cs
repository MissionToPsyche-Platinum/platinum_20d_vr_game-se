using PsycheVR.Modes;
using PsycheVR.VR;
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
        [SerializeField] private float minDistanceFromCamera = 0.45f;
        [SerializeField] private float clearancePadding = 0.12f;
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

        [Header("Admin")]
        [Tooltip("Seconds both grips and both thumbstick clicks must be held, with the menu open, to reveal the admin section. F8 in the editor.")]
        [SerializeField] private float adminHoldSeconds = 4f;
        [Tooltip("Seconds into the hold before the progress ring appears. A brief accidental press shows nothing.")]
        [SerializeField] private float adminIndicatorDelaySeconds = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent onResumeRequested = new UnityEvent();
        [SerializeField] private UnityEvent onRestartRequested = new UnityEvent();
        [SerializeField] private UnityEvent onQuitRequested = new UnityEvent();

        private const float ContentSpacing = 14f;
        private const float AdminLabelHeight = 22f;
        private const float AdminLabelFontSize = 15f;
        private const float AdminRingSize = 34f;
        private const float AdminRingAlpha = 0.7f;
        private static readonly Vector2 AdminRingOffset = new Vector2(-26f, -22f);
        private const int RingTextureSize = 64;
        private const float RingInnerRadiusFraction = 0.36f;
        private const string CurrentModeSuffix = " (current)";

        private static Sprite _ringSprite;

        private GameObject _menuRoot;
        private CanvasGroup _canvasGroup;
        private InputAction _pauseToggleAction;
        private float _timeScaleBeforePause = 1f;

        private AdminCombo _adminCombo;
        private GameObject _adminSection;
        private Image _adminRing;
        private TextMeshProUGUI _storyModeLabel;
        private TextMeshProUGUI _eventModeLabel;
        private GameObject _debugTeleportButton;
        private bool _adminRevealed;

        private void Awake()
        {
            EnsureCameraTransform();
            EnsureEventSystem();
            EnsurePauseToggleAction();
            EnsureAdminCombo();
            BuildMenuIfNeeded();
        }

        private void Start()
        {
            SetMenuVisible(showOnStart);
        }

        private void OnEnable()
        {
            EnsurePauseToggleAction();
            EnsureAdminCombo();
            _adminCombo?.Enable();

            if (_pauseToggleAction == null)
                return;

            _pauseToggleAction.performed += OnPauseTogglePerformed;
            _pauseToggleAction.Enable();
        }

        private void OnDisable()
        {
            _adminCombo?.Disable();

            if (_pauseToggleAction == null)
                return;

            _pauseToggleAction.performed -= OnPauseTogglePerformed;
            _pauseToggleAction.Disable();
        }

        private void OnDestroy()
        {
            if (_adminCombo == null)
                return;

            _adminCombo.Completed -= RevealAdminSection;
            _adminCombo.Dispose();
            _adminCombo = null;
        }

        /// <summary>
        /// Advances the admin combo only while the menu is open. Unscaled time, because
        /// the menu holds the time scale at zero.
        /// </summary>
        private void Update()
        {
            if (_adminCombo == null || _adminRevealed || !IsMenuVisible)
                return;

            _adminCombo.Tick(Time.unscaledDeltaTime);
            UpdateAdminRing();
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

            if (isVisible)
                PlaceMenuInFrontOfCamera();

            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (isVisible)
            {
                PauseGameplay();
            }
            else
            {
                // The admin section never survives a close: the next open starts hidden.
                HideAdminSection();
                _adminCombo?.Reset();
                ResumeGameplay();
            }
        }

        /// <summary>
        /// Places the panel in front of the headset, pulled up short of anything solid.
        ///
        /// A fixed <see cref="distanceFromCamera"/> breaks anywhere the player stands close
        /// to geometry. The bedroom teleport anchor faces a wall about a metre out, so the
        /// world-space canvas materialised inside it and the depth test hid the menu
        /// completely -- gameplay paused with nothing on screen.
        ///
        /// Scale tracks the distance so the panel keeps the same apparent size wherever it
        /// lands, and hits on the rig itself are skipped: the sweep starts inside the
        /// player's own CharacterController, which would otherwise read as a blocker at
        /// zero distance.
        /// </summary>
        private void PlaceMenuInFrontOfCamera()
        {
            if (_menuRoot == null || cameraTransform == null)
                return;

            // Half the panel's short edge, so a wall that covers the panel is caught without
            // a full-width sweep snagging on desks and floors the panel would clear anyway.
            float sweepRadius = panelSize.y * menuScale * 0.5f;
            float distance = distanceFromCamera;

            RaycastHit[] hits = Physics.SphereCastAll(
                cameraTransform.position,
                sweepRadius,
                cameraTransform.forward,
                distanceFromCamera,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform))
                    continue;

                distance = Mathf.Min(distance, hits[i].distance - clearancePadding);
            }

            distance = Mathf.Clamp(distance, minDistanceFromCamera, distanceFromCamera);

            _menuRoot.transform.localPosition = new Vector3(0f, verticalOffset, distance);
            _menuRoot.transform.localRotation = Quaternion.identity;
            _menuRoot.transform.localScale = Vector3.one * (menuScale * (distance / distanceFromCamera));
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
            layout.spacing = ContentSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            BuildHeader(content.transform);

            CreateButton("Resume Button", content.transform, "Resume", resumeButtonTint, OnResumePressed);
            CreateButton("Quit Button", content.transform, "Quit Game", quitButtonTint, OnQuitPressed);

            BuildAdminSection(content.transform);
            BuildAdminRing(card.transform);
        }

        /// <summary>
        /// Staff-only controls, hidden until <see cref="AdminCombo"/> completes. Story Mode
        /// and Event Mode call <see cref="GameModeManager.SwitchTo"/>; pressing the current
        /// mode restarts it. Debug Teleport is only shown in Story mode, and only where a
        /// <see cref="BlinkTeleportRoute"/> exists (the rig prefab is shared with test
        /// scenes that have none). All buttons reuse resumeButtonTint: every button already
        /// uses the same neutral tint, and a new serialized colour would put another
        /// property on the shared rig prefab for no visual gain.
        /// </summary>
        private void BuildAdminSection(Transform parent)
        {
            _adminSection = CreateUiObject("Admin Section", parent);

            VerticalLayoutGroup layout = _adminSection.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ContentSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateLabel(
                "Admin Label",
                _adminSection.transform,
                "ADMIN",
                AdminLabelFontSize,
                FontStyles.Bold,
                outlineTint,
                new Vector2(0f, AdminLabelHeight),
                TextAlignmentOptions.Center);

            GameObject modeRow = CreateUiObject("Mode Row", _adminSection.transform);
            LayoutElement rowLayout = modeRow.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = buttonSize.y;
            rowLayout.minHeight = buttonSize.y;

            HorizontalLayoutGroup rowGroup = modeRow.AddComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = ContentSpacing;
            rowGroup.childControlHeight = true;
            rowGroup.childControlWidth = true;
            rowGroup.childForceExpandHeight = true;
            rowGroup.childForceExpandWidth = true;

            Button storyButton = CreateButton("Story Mode Button", modeRow.transform, "Story Mode", resumeButtonTint, () => OnModePressed(GameMode.Story));
            Button eventButton = CreateButton("Event Mode Button", modeRow.transform, "Event Mode", resumeButtonTint, () => OnModePressed(GameMode.Event));
            _storyModeLabel = storyButton.GetComponentInChildren<TextMeshProUGUI>();
            _eventModeLabel = eventButton.GetComponentInChildren<TextMeshProUGUI>();

            _debugTeleportButton = CreateButton("Debug Teleport Button", _adminSection.transform, "Debug Teleport", resumeButtonTint, OnTeleportPressed).gameObject;

            _adminSection.SetActive(false);
        }

        /// <summary>
        /// Small radial ring in the card's top-right corner. Hidden until the combo has
        /// been held past the indicator delay, fills as the hold completes.
        /// </summary>
        private void BuildAdminRing(Transform parent)
        {
            GameObject ring = CreateUiObject("Admin Progress Ring", parent);
            RectTransform rect = ring.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(AdminRingSize, AdminRingSize);
            rect.anchoredPosition = AdminRingOffset;

            _adminRing = ring.AddComponent<Image>();
            _adminRing.sprite = GetRingSprite();
            _adminRing.type = Image.Type.Filled;
            _adminRing.fillMethod = Image.FillMethod.Radial360;
            _adminRing.fillOrigin = (int)Image.Origin360.Top;
            _adminRing.fillClockwise = true;
            _adminRing.fillAmount = 0f;
            _adminRing.raycastTarget = false;
            _adminRing.color = new Color(outlineTint.r, outlineTint.g, outlineTint.b, AdminRingAlpha);

            ring.SetActive(false);
        }

        private void UpdateAdminRing()
        {
            if (_adminRing == null)
                return;

            bool visible = _adminCombo != null && _adminCombo.IndicatorVisible;
            if (_adminRing.gameObject.activeSelf != visible)
                _adminRing.gameObject.SetActive(visible);

            if (visible)
                _adminRing.fillAmount = _adminCombo.Progress;
        }

        private void RevealAdminSection()
        {
            if (_adminSection == null || _adminRevealed)
                return;

            bool isStory = GameModeManager.IsStory;
            bool showTeleport = isStory && FindFirstObjectByType<BlinkTeleportRoute>() != null;

            if (_storyModeLabel != null)
                _storyModeLabel.text = "Story Mode" + (isStory ? CurrentModeSuffix : string.Empty);
            if (_eventModeLabel != null)
                _eventModeLabel.text = "Event Mode" + (isStory ? string.Empty : CurrentModeSuffix);
            if (_debugTeleportButton != null)
                _debugTeleportButton.SetActive(showTeleport);

            int rows = showTeleport ? 2 : 1;
            float extraHeight = AdminLabelHeight + rows * buttonSize.y + (rows + 1) * ContentSpacing;
            _menuRoot.GetComponent<RectTransform>().sizeDelta = panelSize + new Vector2(0f, extraHeight);

            _adminSection.SetActive(true);
            _adminRevealed = true;

            if (_adminRing != null)
                _adminRing.gameObject.SetActive(false);

            Debug.Log($"PauseMenuController: admin section revealed in {GameModeManager.ActiveMode} mode.", this);
        }

        private void HideAdminSection()
        {
            _adminRevealed = false;

            if (_adminSection != null && _adminSection.activeSelf)
                _adminSection.SetActive(false);

            if (_adminRing != null && _adminRing.gameObject.activeSelf)
                _adminRing.gameObject.SetActive(false);

            if (_menuRoot != null)
                _menuRoot.GetComponent<RectTransform>().sizeDelta = panelSize;
        }

        /// <summary>
        /// Procedural ring so the menu needs no extra sprite asset on the shared rig prefab.
        /// </summary>
        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null)
                return _ringSprite;

            var texture = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Admin Ring",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float center = (RingTextureSize - 1) * 0.5f;
            float outer = RingTextureSize * 0.5f - 1f;
            float inner = RingTextureSize * RingInnerRadiusFraction;
            var pixels = new Color32[RingTextureSize * RingTextureSize];

            for (int y = 0; y < RingTextureSize; y++)
            {
                for (int x = 0; x < RingTextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    // One-pixel soft edge on both sides of the band.
                    float coverage = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
                    pixels[y * RingTextureSize + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, RingTextureSize, RingTextureSize), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.name = "Admin Ring";
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
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

        private void EnsureAdminCombo()
        {
            if (_adminCombo != null)
                return;

            _adminCombo = new AdminCombo(adminHoldSeconds, adminIndicatorDelaySeconds);
            _adminCombo.Completed += RevealAdminSection;
        }

        private void OnPauseTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleMenu();
        }

        /// <summary>
        /// Starts <paramref name="mode"/> from a cold boot by reloading the master scene.
        /// The time scale is restored first: the reload replaces this menu, and the fresh
        /// scene's own pause menu decides whether to start paused.
        /// </summary>
        private void OnModePressed(GameMode mode)
        {
            ResumeGameplay();
            GameModeManager.SwitchTo(mode);
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

        /// <summary>
        /// Switches the player to the other room and closes the menu. HideMenu restores
        /// the time scale on its own, so gameplay resumes as part of the same press.
        /// The blink itself is driven by unscaled time, so it plays correctly across the
        /// pause-to-resume handoff.
        /// </summary>
        private void OnTeleportPressed()
        {
            var route = FindFirstObjectByType<BlinkTeleportRoute>();
            if (route == null)
            {
                Debug.LogWarning("PauseMenuController: no BlinkTeleportRoute in the scene; ignoring teleport.", this);
                return;
            }

            route.Trigger();
            HideMenu();
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
