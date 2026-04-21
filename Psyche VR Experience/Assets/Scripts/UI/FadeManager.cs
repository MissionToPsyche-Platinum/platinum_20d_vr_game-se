using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PsycheVR.UI
{
    [DisallowMultipleComponent]
    public class FadeManager : MonoBehaviour
    {
        public static FadeManager Instance { get; private set; }

        [Header("Defaults")]
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private float defaultFadeOutDuration = 0.35f;
        [SerializeField] private float defaultHoldDuration = 0.05f;
        [SerializeField] private float defaultFadeInDuration = 0.35f;
        [SerializeField] private int sortingOrder = 1000;

        private CanvasGroup _canvasGroup;
        private Image _overlayImage;
        private Coroutine _activeFadeRoutine;

        public static FadeManager GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            FadeManager existingManager = FindAnyObjectByType<FadeManager>();
            if (existingManager != null)
                return existingManager;

            GameObject managerObject = new GameObject("Fade Manager");
            return managerObject.AddComponent<FadeManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlayIfNeeded();
        }

        public void LoadSceneWithFade(string sceneName)
        {
            LoadSceneWithFade(sceneName, defaultFadeOutDuration, defaultHoldDuration, defaultFadeInDuration);
        }

        public void LoadSceneWithFade(string sceneName, float fadeOutDuration, float holdDuration, float fadeInDuration)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("FadeManager received an empty scene name.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"FadeManager could not load scene '{sceneName}'. Make sure it is added to Build Settings.");
                StartManagedFade(FadePulseRoutine(fadeOutDuration, holdDuration, fadeInDuration));
                return;
            }

            StartManagedFade(LoadSceneRoutine(sceneName, fadeOutDuration, holdDuration, fadeInDuration));
        }

        public void LoadSceneWithFade(int buildIndex, float fadeOutDuration, float holdDuration, float fadeInDuration)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning($"FadeManager received an invalid build index: {buildIndex}");
                return;
            }

            StartManagedFade(LoadSceneRoutine(buildIndex, fadeOutDuration, holdDuration, fadeInDuration));
        }

        public void FadePulse()
        {
            FadePulse(defaultFadeOutDuration, defaultHoldDuration, defaultFadeInDuration);
        }

        public void FadePulse(float fadeOutDuration, float holdDuration, float fadeInDuration)
        {
            StartManagedFade(FadePulseRoutine(fadeOutDuration, holdDuration, fadeInDuration));
        }

        private void StartManagedFade(IEnumerator routine)
        {
            BuildOverlayIfNeeded();

            if (_activeFadeRoutine != null)
                StopCoroutine(_activeFadeRoutine);

            _activeFadeRoutine = StartCoroutine(routine);
        }

        private IEnumerator LoadSceneRoutine(string sceneName, float fadeOutDuration, float holdDuration, float fadeInDuration)
        {
            yield return FadeRoutine(1f, fadeOutDuration);
            yield return WaitForSecondsRealtimeSafe(holdDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
            if (loadOperation == null)
            {
                Debug.LogWarning($"FadeManager failed to start async load for scene '{sceneName}'.");
                yield return FadeRoutine(0f, fadeInDuration);
                _activeFadeRoutine = null;
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            yield return FadeRoutine(0f, fadeInDuration);
            _activeFadeRoutine = null;
        }

        private IEnumerator LoadSceneRoutine(int buildIndex, float fadeOutDuration, float holdDuration, float fadeInDuration)
        {
            yield return FadeRoutine(1f, fadeOutDuration);
            yield return WaitForSecondsRealtimeSafe(holdDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(buildIndex);
            if (loadOperation == null)
            {
                Debug.LogWarning($"FadeManager failed to start async load for build index '{buildIndex}'.");
                yield return FadeRoutine(0f, fadeInDuration);
                _activeFadeRoutine = null;
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            yield return FadeRoutine(0f, fadeInDuration);
            _activeFadeRoutine = null;
        }

        private IEnumerator FadePulseRoutine(float fadeOutDuration, float holdDuration, float fadeInDuration)
        {
            yield return FadeRoutine(1f, fadeOutDuration);
            yield return WaitForSecondsRealtimeSafe(holdDuration);
            yield return FadeRoutine(0f, fadeInDuration);
            _activeFadeRoutine = null;
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            BuildOverlayIfNeeded();

            float startAlpha = _canvasGroup.alpha;
            if (duration <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private IEnumerator WaitForSecondsRealtimeSafe(float duration)
        {
            if (duration <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void BuildOverlayIfNeeded()
        {
            if (_canvasGroup != null)
                return;

            GameObject canvasObject = new GameObject("Fade Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject("Fade Image", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            _overlayImage = imageObject.GetComponent<Image>();
            _overlayImage.color = fadeColor;
            _overlayImage.raycastTarget = false;
        }
    }
}
