using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    public class BookHighlight : MonoBehaviour
    {
        [Header("Glow Settings")]
        [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private float rimPower = 4f;
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Renderers")]
        [Tooltip("Only these renderers receive the glow. If empty, auto-collects covers and spine.")]
        [SerializeField] private Renderer[] targetRenderers;

        private static readonly int RimColorID = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerID = Shader.PropertyToID("_RimPower");

        private PsycheGrabbable _grabbable;
        private MaterialPropertyBlock _propBlock;
        private Renderer _spineRenderer;
        private bool _isHeld;
        private float _glowTarget;
        private float _glowCurrent;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();
            _propBlock = new MaterialPropertyBlock();

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = CollectCoverAndSpineRenderers();

            var spineTransform = transform.Find("SpineMesh");
            if (spineTransform != null)
                _spineRenderer = spineTransform.GetComponent<Renderer>();
        }

        private Renderer[] CollectCoverAndSpineRenderers()
        {
            var results = new List<Renderer>();
            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                var name = rend.gameObject.name;
                if (name.StartsWith("Cover") || name.StartsWith("Spine"))
                    results.Add(rend);
            }
            return results.ToArray();
        }

        private void OnEnable()
        {
            _grabbable.hoverEntered.AddListener(OnHoverEntered);
            _grabbable.hoverExited.AddListener(OnHoverExited);
            _grabbable.selectEntered.AddListener(OnSelectEntered);
            _grabbable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            _grabbable.hoverEntered.RemoveListener(OnHoverEntered);
            _grabbable.hoverExited.RemoveListener(OnHoverExited);
            _grabbable.selectEntered.RemoveListener(OnSelectEntered);
            _grabbable.selectExited.RemoveListener(OnSelectExited);
        }

        private void Update()
        {
            if (Mathf.Approximately(_glowCurrent, _glowTarget))
                return;

            _glowCurrent = fadeDuration > 0f
                ? Mathf.MoveTowards(_glowCurrent, _glowTarget, Time.deltaTime / fadeDuration)
                : _glowTarget;

            ApplyGlow(_glowCurrent);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!_isHeld)
                _glowTarget = 1f;
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (!_isHeld && _grabbable.interactorsHovering.Count == 0)
                _glowTarget = 0f;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _isHeld = true;
            _glowTarget = 0f;
            if (_spineRenderer != null)
                _spineRenderer.enabled = false;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _isHeld = false;
            if (_spineRenderer != null)
                _spineRenderer.enabled = true;
        }

        private void ApplyGlow(float t)
        {
            var color = Color.Lerp(new Color(0f, 0f, 0f, 0f), glowColor, t);
            foreach (var rend in targetRenderers)
            {
                rend.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(RimColorID, color);
                _propBlock.SetFloat(RimPowerID, rimPower);
                rend.SetPropertyBlock(_propBlock);
            }
        }
    }
}
