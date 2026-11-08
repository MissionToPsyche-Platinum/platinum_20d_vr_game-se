using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using PsycheVR.Kiosk;

namespace PsycheVR.Story
{
    /// <summary>
    /// The Story mode date card (TG-342): a short line such as "October 2023" or "2003" that floats at eye
    /// height in front of the player when a phase begins, so they know when they are. It fades in, holds,
    /// and fades out by itself; <see cref="Hide"/> ends it early. It follows the head like the Event cards
    /// (<see cref="HeadFollow"/>): it stays put through small movements and glides back after a large turn.
    ///
    /// The StoryDirector calls <see cref="Show"/> and <see cref="Hide"/>; nothing else drives it. Put the
    /// StoryDateCard prefab (Assets/Prefabs/Story) in the scene once. It starts hidden.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StoryDateCard : MonoBehaviour
    {
        /// <summary>Card units are millimetres; the root is scaled by this to metres at the reference distance.</summary>
        private const float MillimetresPerMetre = 1000f;

        [Tooltip("Text that shows the date.")]
        [SerializeField] private TMP_Text dateText;

        [Tooltip("Headset camera the card floats in front of. Unset: the XR Origin's camera, then Camera.main.")]
        [SerializeField] private Transform head;

        [Tooltip("Metres in front of the player when nothing is in the way.")]
        [SerializeField, Min(0.5f)] private float distance = 1.6f;

        [Tooltip("Card height in millimetres, used to keep the card out of walls.")]
        [SerializeField, Min(1f)] private float cardHeight = 260f;

        [Tooltip("Seconds to fade in and to fade out.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.6f;

        [Tooltip("Seconds the card stays fully visible. 0 keeps it up until Hide is called.")]
        [SerializeField, Min(0f)] private float holdSeconds = 4f;

        private CanvasGroup _group;
        private HeadFollow _follow;
        private Vector3 _baseScale;
        private float _alpha;
        private float _shownAt = -1f;
        private bool _hiding;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/> or the end of the hold; false while fading out.</summary>
        public bool IsVisible => _shownAt >= 0f && !_hiding && gameObject.activeSelf;

        /// <summary>The text passed to the last <see cref="Show"/>, or null before the first one.</summary>
        public string CurrentText { get; private set; }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _baseScale = transform.localScale;
            if (_shownAt < 0f) SetHidden();
        }

        /// <summary>
        /// Shows <paramref name="text"/> in front of the player and fades the card in. Calling it again while
        /// the card is up changes the text and restarts the hold.
        /// </summary>
        /// <param name="text">The date line, for example "October 2023".</param>
        public void Show(string text)
        {
            if (_group == null) Awake();
            CurrentText = text;
            if (dateText != null) dateText.text = text;

            bool wasHidden = !gameObject.activeSelf || _alpha <= 0f;
            gameObject.SetActive(true);
            _shownAt = Time.time;
            _hiding = false;

            if (wasHidden) Place();
        }

        /// <summary>Fades the card out now. Does nothing when it is already hidden.</summary>
        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            _hiding = true;
        }

        private void Update()
        {
            float step = Time.deltaTime / fadeSeconds;
            if (!_hiding && holdSeconds > 0f && _alpha >= 1f && Time.time - _shownAt >= fadeSeconds + holdSeconds)
                _hiding = true;

            _alpha = Mathf.Clamp01(_alpha + (_hiding ? -step : step));
            _group.alpha = _alpha;
            if (_hiding && _alpha <= 0f) SetHidden();
        }

        /// <summary>Puts the card straight ahead of the head at once and starts following it.</summary>
        private void Place()
        {
            Transform camera = ResolveHead();
            if (camera == null) return;
            if (_follow == null) _follow = gameObject.AddComponent<HeadFollow>();
            // HeadFollow keeps the scale it finds as its base, so hand it the prefab's own scale every time.
            transform.localScale = _baseScale;
            _follow.Init(camera, distance, cardHeight * 0.5f / MillimetresPerMetre);
        }

        private Transform ResolveHead()
        {
            if (head != null) return head;
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null) head = origin.Camera.transform;
            else if (Camera.main != null) head = Camera.main.transform;
            return head;
        }

        private void SetHidden()
        {
            _alpha = 0f;
            _group.alpha = 0f;
            _hiding = false;
            _shownAt = -1f;
            gameObject.SetActive(false);
        }
    }
}
