using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;

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
        [Header("Settings")]
        [Tooltip("Shared tuning asset. Create via Assets > Create > Psyche VR > Chalkboard Settings.")]
        [SerializeField] private ChalkboardSettings settings;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private TextMeshProUGUI progressText;
        [Tooltip("Optional image element for component icons.")]
        [SerializeField] private Image iconImage;

        [Header("Default State")]
        [SerializeField] private string defaultName = "Psyche Spacecraft";
        [TextArea(2, 5)]
        [SerializeField] private string defaultInfo = "Pick up a component to learn about it.";

        [Header("Completion")]
        [SerializeField] private string completionName = "Assembly Complete!";
        [TextArea(2, 5)]
        [SerializeField] private string completionInfo =
            "All components are in place. The Psyche spacecraft is ready for its mission to explore asteroid 16 Psyche \u2014 a metal-rich world that may be the exposed core of an early planet.";

        [Header("Audio")]
        [Tooltip("Sound played when a component is snapped.")]
        [SerializeField] private AudioClip snapSound;
        [Tooltip("Sound played when all components are placed.")]
        [SerializeField] private AudioClip completionSound;

        [Header("Events")]
        public UnityEvent onAllComponentsPlaced;

        private CanvasGroup _canvasGroup;
        private AudioSource _audioSource;
        private int _totalComponents;
        private int _snappedCount;
        private Coroutine _fadeRoutine;
        private Coroutine _punchRoutine;
        private Vector3 _nameOriginalScale;

        public int SnappedCount => _snappedCount;
        public int TotalComponents => _totalComponents;
        public bool IsComplete => _totalComponents > 0 && _snappedCount >= _totalComponents;
        public ChalkboardSettings Settings => settings;

        private float FadeSpeed => settings != null ? settings.FadeSpeed : 0.25f;
        private float PunchScale => settings != null ? settings.SnapPunchScale : 1.15f;

        private void Awake()
        {
            _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                if (canvas != null)
                    _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;

            if (nameText != null)
                _nameOriginalScale = nameText.transform.localScale;

            if (settings == null)
                Debug.LogWarning("[Chalkboard] No ChalkboardSettings assigned. Using defaults.", this);
        }

        private void Start()
        {
            _totalComponents = FindObjectsByType<ComponentInfo>(FindObjectsSortMode.None).Length;
            UpdateIcon(null);
            ShowDefault();
            UpdateProgress();
        }

        public void ShowComponentInfo(ComponentData data)
        {
            if (data == null) return;
            UpdateIcon(data.icon);
            SetText(data.DisplayTitle, data.description);
        }

        public void ShowSnappedInfo(ComponentData data)
        {
            if (data == null) return;
            _snappedCount++;

            if (snapSound != null)
                _audioSource.PlayOneShot(snapSound);

            UpdateProgress();
            UpdateIcon(data.icon);

            if (_snappedCount >= _totalComponents)
            {
                SetText(completionName, completionInfo);

                if (completionSound != null)
                    _audioSource.PlayOneShot(completionSound);

                onAllComponentsPlaced?.Invoke();
            }
            else
            {
                SetText(data.DisplayTitle + "  \u2714", data.description);
            }

            PunchName();
        }

        public void ShowDefault()
        {
            if (IsComplete) return;
            UpdateIcon(null);
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
                    t -= Time.deltaTime / FadeSpeed;
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
                    t += Time.deltaTime / FadeSpeed;
                    _canvasGroup.alpha = Mathf.Min(1f, t);
                    yield return null;
                }
            }

            _fadeRoutine = null;
        }

        private void PunchName()
        {
            if (nameText == null) return;

            if (_punchRoutine != null)
                StopCoroutine(_punchRoutine);

            _punchRoutine = StartCoroutine(PunchAnimation());
        }

        private IEnumerator PunchAnimation()
        {
            var tf = nameText.transform;
            tf.localScale = _nameOriginalScale * PunchScale;

            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                tf.localScale = Vector3.Lerp(
                    _nameOriginalScale * PunchScale,
                    _nameOriginalScale,
                    t / 0.3f);
                yield return null;
            }

            tf.localScale = _nameOriginalScale;
            _punchRoutine = null;
        }

        private void UpdateIcon(Sprite icon)
        {
            if (iconImage == null) return;

            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        private void UpdateProgress()
        {
            if (progressText != null && _totalComponents > 0)
                progressText.text = $"{_snappedCount} / {_totalComponents} components placed";
        }
    }
}
