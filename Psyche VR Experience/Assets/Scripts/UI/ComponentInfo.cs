using System.Collections;
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
        [Tooltip("Emission intensity when the piece is highlighted.")]
        [SerializeField] private float highlightEmission = 0.3f;

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
        public bool IsSnapped => _isSnapped;

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
            SetEmission(highlightEmission);
        }

        private void OnMouseUp()
        {
            if (_isSnapped || !_isHeld) return;

            _isHeld = false;
            chalkboard?.ShowDefault();
            SetColor(_originalColor);
            SetEmission(0f);
        }

        // --- VR Events ---

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_isSnapped || data == null) return;

            _isHeld = true;
            chalkboard?.ShowComponentInfo(data);
            SetColor(highlightColor);
            SetEmission(highlightEmission);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _isHeld = false;

            if (!_isSnapped)
            {
                chalkboard?.ShowDefault();
                SetColor(_originalColor);
                SetEmission(0f);
            }
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (_isSnapped || _isHeld) return;
            transform.localScale = _originalScale * 1.05f;
            SetEmission(highlightEmission * 0.5f);
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            if (_isSnapped || _isHeld) return;
            transform.localScale = _originalScale;
            SetEmission(0f);
        }

        // --- Snap Detection ---

        private void OnSnapped()
        {
            if (_isSnapped) return;

            _isSnapped = true;
            transform.localScale = _originalScale;
            SetColor(snappedColor);
            SetEmission(0f);
            chalkboard?.ShowSnappedInfo(data);

            StartCoroutine(SnapFlash());
        }

        private IEnumerator SnapFlash()
        {
            SetEmission(0.8f);
            yield return new WaitForSeconds(0.15f);
            SetEmission(0.4f);
            yield return new WaitForSeconds(0.15f);
            SetEmission(0f);
        }

        private void SetColor(Color color)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _renderer.SetPropertyBlock(_propBlock);
        }

        private void SetEmission(float intensity)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_propBlock);
            if (intensity > 0f)
            {
                Color emissionColor = (_isSnapped ? snappedColor : highlightColor) * intensity;
                _propBlock.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                _propBlock.SetColor("_EmissionColor", Color.black);
            }
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
