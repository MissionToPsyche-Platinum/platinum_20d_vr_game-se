using Unity.XR.CoreUtils;
using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>
    /// The Story mode objective marker (TG-342): a Mustard chevron that hovers and bobs above the object the
    /// player should use next, such as the model spacecraft in phase 1. It turns about the vertical to face
    /// the player and fades in and out.
    ///
    /// The StoryDirector calls <see cref="Show"/> with the target and <see cref="Hide"/> when the player has
    /// used it. Put the ObjectiveHighlight prefab (Assets/Prefabs/Story) in the scene once. It starts hidden,
    /// is never parented to the target, and only reads the target's mesh renderers to find its top. It centres
    /// over the meshes, not the target's pivot, because a model's pivot can sit well off the model.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ObjectiveHighlight : MonoBehaviour
    {
        [Tooltip("Headset camera the chevron turns toward. Unset: the XR Origin's camera, then Camera.main.")]
        [SerializeField] private Transform head;

        [Tooltip("Metres between the top of the target and the tip of the chevron.")]
        [SerializeField, Min(0f)] private float hoverHeight = 0.08f;

        [Tooltip("Metres the chevron bobs up and down.")]
        [SerializeField, Min(0f)] private float bobHeight = 0.02f;

        [Tooltip("Bobs per second.")]
        [SerializeField, Min(0f)] private float bobRate = 0.8f;

        [Tooltip("Seconds to fade in and to fade out.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;

        private CanvasGroup _group;
        private Transform _target;
        private Vector3 _localTop;
        private float _alpha;
        private bool _hiding;

        /// <summary>The object being marked, or null once <see cref="Hide"/> is called.</summary>
        public Transform Target => _hiding ? null : _target;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>; false while fading out.</summary>
        public bool IsVisible => gameObject.activeSelf && _target != null && !_hiding;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            if (_target == null) SetHidden();
        }

        /// <summary>Marks <paramref name="target"/>. Calling it again moves the marker to the new target.</summary>
        /// <param name="target">The object to mark. Null hides the marker.</param>
        public void Show(Transform target)
        {
            if (_group == null) Awake();
            if (target == null) { Hide(); return; }

            _target = target;
            _localTop = target.InverseTransformPoint(TopCentre(target));
            _hiding = false;
            gameObject.SetActive(true);
            Follow();
        }

        /// <summary>Fades the marker out. Does nothing when it is already hidden.</summary>
        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            _hiding = true;
        }

        private void LateUpdate()
        {
            if (_target == null) { SetHidden(); return; }   // the target was destroyed

            _alpha = Mathf.Clamp01(_alpha + (_hiding ? -1f : 1f) * Time.deltaTime / fadeSeconds);
            _group.alpha = _alpha;
            if (_hiding && _alpha <= 0f) { SetHidden(); return; }
            Follow();
        }

        /// <summary>Hovers above the centre of the target's top and turns about the vertical toward the head.</summary>
        private void Follow()
        {
            float bob = bobHeight * 0.5f * (1f + Mathf.Sin(Time.time * bobRate * 2f * Mathf.PI));
            transform.position = _target.TransformPoint(_localTop) + Vector3.up * (hoverHeight + bob);

            Transform camera = ResolveHead();
            if (camera == null) return;
            Vector3 away = transform.position - camera.position;
            away.y = 0f;
            // A world-space canvas reads from its -Z side, so face away from the head.
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        /// <summary>
        /// World point at the top centre of the target's visible meshes (text and effects are ignored); the
        /// target's own position when it has none.
        /// </summary>
        public static Vector3 TopCentre(Transform target)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (found) bounds.Encapsulate(r.bounds); else bounds = r.bounds;
                found = true;
            }
            return found ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : target.position;
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
            _target = null;
            _alpha = 0f;
            _hiding = false;
            if (_group != null) _group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
