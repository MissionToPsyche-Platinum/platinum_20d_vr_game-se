using UnityEngine;

namespace PsycheVR.VR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public class BlinkFadeOverlay : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MeshRenderer _meshRenderer;
        private Material _materialInstance;
        private Color _cachedColor = Color.black;

        public float CurrentAlpha => _cachedColor.a;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _materialInstance = _meshRenderer.material; // Instance; cleaned up in OnDestroy.
            _cachedColor = Color.black;
            _cachedColor.a = 0f;
            _materialInstance.SetColor(ColorId, _cachedColor);
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
        }

        public void SetAlpha(float alpha)
        {
            _cachedColor.a = Mathf.Clamp01(alpha);
            _materialInstance.SetColor(ColorId, _cachedColor);
        }
    }
}
