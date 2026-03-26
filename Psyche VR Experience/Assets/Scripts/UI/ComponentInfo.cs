using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.UI
{
    /// <summary>
    /// Attach to each spacecraft piece. When grabbed, updates the Chalkboard
    /// with this component's educational data. When snapped, shows permanent
    /// confirmation. Uses ScriptableObject ComponentData for content.
    ///
    /// Works with PsycheGrabbable (VR) and mouse click (desktop testing).
    /// The Chalkboard is found automatically by the "Chalkboard" tag.
    /// </summary>
    public class ComponentInfo : MonoBehaviour
    {
        [Header("Component Data")]
        [Tooltip("ScriptableObject with this component's name and info. Create via Assets > Create > Psyche VR > Component Data.")]
        [SerializeField] private ComponentData data;

        [Header("Visual Feedback")]
        [SerializeField] private Color highlightColor = new Color(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color snappedColor = new Color(0.2f, 1f, 0.2f, 1f);

        [Header("References")]
        [Tooltip("Leave empty to auto-find by 'Chalkboard' tag.")]
        [SerializeField] private Chalkboard chalkboard;

        private XRBaseInteractable _interactable;
        private SnappableObject _snappable;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private Color _originalColor;
        private Vector3 _originalScale;
        private bool _isSnapped;
        private bool _isHeld;

        public ComponentData Data => data;

        private void Awake()
        {
            _interactable = GetComponent<XRBaseInteractable>();
            _snappable = GetComponent<SnappableObject>();
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            _originalScale = transform.localScale;

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _originalColor = _propBlock.GetColor("_BaseColor");
                if (_originalColor == Color.clear)
                    _originalColor = Color.gray;
            }

            if (chalkboard == null)
            {
                var boardObj = GameObject.FindGameObjectWithTag("Chalkboard");
                if (boardObj != null)
                    chalkboard = boardObj.GetComponent<Chalkboard>();
            }

            if (data == null)
                Debug.LogError($"[ComponentInfo] No ComponentData assigned on {gameObject.name}", this);
        }

        private void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.AddListener(OnGrabbed);
                _interactable.selectExited.AddListener(OnReleased);
                _interactable.hoverEntered.AddListener(OnHoverEnter);
                _interactable.hoverExited.AddListener(OnHoverExit);
            }

            if (_snappable != null)
                _snappable.onSnapped += OnSnapped;
        }

        private void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnGrabbed);
                _interactable.selectExited.RemoveListener(OnReleased);
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
            }

            if (_snappable != null)
                _snappable.onSnapped -= OnSnapped;
        }

        // --- Desktop Testing (mouse) ---

        private void OnMouseDown()
        {
            if (_isSnapped || data == null) return;

            _isHeld = true;
            chalkboard?.ShowComponentInfo(data);
            SetColor(highlightColor);
        }

        private void OnMouseUp()
        {
            if (_isSnapped || !_isHeld) return;

            _isHeld = false;
            chalkboard?.ShowDefault();
            SetColor(_originalColor);
        }

        // --- VR Events ---

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_isSnapped || data == null) return;

            _isHeld = true;
            chalkboard?.ShowComponentInfo(data);
            SetColor(highlightColor);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _isHeld = false;

            if (!_isSnapped)
            {
                chalkboard?.ShowDefault();
                SetColor(_originalColor);
            }
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (_isSnapped || _isHeld) return;
            transform.localScale = _originalScale * 1.05f;
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            if (_isSnapped || _isHeld) return;
            transform.localScale = _originalScale;
        }

        // --- Snap Detection ---

        private void OnSnapped()
        {
            if (_isSnapped) return;

            _isSnapped = true;
            SetColor(snappedColor);
            transform.localScale = _originalScale;
            chalkboard?.ShowSnappedInfo(data);
        }

        private void SetColor(Color color)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
