using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace PsycheVR.UI
{
    /// <summary>
    /// Central display board that shows educational info about spacecraft
    /// components. Updated by ComponentInfo when a piece is grabbed or snapped.
    ///
    /// Tag this GameObject as "Chalkboard" so ComponentInfo can auto-find it.
    /// </summary>
    public class Chalkboard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("Default State")]
        [SerializeField] private string defaultName = "Psyche Spacecraft";
        [TextArea(2, 5)]
        [SerializeField] private string defaultInfo = "Pick up a component to learn about it.";

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 0.25f;

        [Header("Events")]
        public UnityEvent onAllComponentsPlaced;

        private CanvasGroup _canvasGroup;
        private int _totalComponents;
        private int _snappedCount;
        private Coroutine _fadeRoutine;

        public int SnappedCount => _snappedCount;
        public int TotalComponents => _totalComponents;

        private void Awake()
        {
            _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                if (canvas != null)
                    _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            _totalComponents = FindObjectsByType<ComponentInfo>(FindObjectsSortMode.None).Length;
            ShowDefault();
            UpdateProgress();
        }

        /// <summary>
        /// Called by ComponentInfo when a piece is grabbed.
        /// </summary>
        public void ShowComponentInfo(ComponentData data)
        {
            if (data == null) return;
            SetText(data.componentName, data.description);
        }

        /// <summary>
        /// Called by ComponentInfo when a piece is snapped into place.
        /// </summary>
        public void ShowSnappedInfo(ComponentData data)
        {
            if (data == null) return;
            _snappedCount++;
            SetText(data.componentName + "  \u2714", data.description);
            UpdateProgress();

            if (_snappedCount >= _totalComponents)
                onAllComponentsPlaced?.Invoke();
        }

        /// <summary>
        /// Called by ComponentInfo when a piece is released without snapping.
        /// </summary>
        public void ShowDefault()
        {
            SetText(defaultName, defaultInfo);
        }

        private void SetText(string nameStr, string infoStr)
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(FadeTransition(nameStr, infoStr));
        }

        private IEnumerator FadeTransition(string nameStr, string infoStr)
        {
            if (_canvasGroup != null)
            {
                float t = _canvasGroup.alpha;
                while (t > 0f)
                {
                    t -= Time.deltaTime / fadeSpeed;
                    _canvasGroup.alpha = Mathf.Max(0f, t);
                    yield return null;
                }
            }

            if (nameText != null) nameText.text = nameStr;
            if (infoText != null) infoText.text = infoStr;

            if (_canvasGroup != null)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / fadeSpeed;
                    _canvasGroup.alpha = Mathf.Min(1f, t);
                    yield return null;
                }
            }

            _fadeRoutine = null;
        }

        private void UpdateProgress()
        {
            if (progressText != null && _totalComponents > 0)
                progressText.text = $"{_snappedCount} / {_totalComponents} components placed";
        }
    }
}
