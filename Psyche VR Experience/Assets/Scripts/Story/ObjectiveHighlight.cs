using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Temporary objective ring, pending the final glow prefab.</summary>
    public sealed class ObjectiveHighlight : MonoBehaviour
    {
        [SerializeField] private Color color = Color.cyan;
        [SerializeField, Min(0.01f)] private float radius = 0.15f;
        [SerializeField, Min(0)] private float padding = 0.025f;
        private Renderer[] targetRenderers;
        private LineRenderer ring;
        private Material ringMaterial;
        public Transform Target { get; private set; }

        public void Show(Transform target)
        {
            Hide();
            Target = target;
            if (target == null)
                return;

            targetRenderers = target.GetComponentsInChildren<Renderer>();

            if (ring == null)
            {
                var go = new GameObject("Story objective ring");
                go.transform.SetParent(transform, false);
                ring = go.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 48;
                ring.widthMultiplier = 0.005f;
                ringMaterial = new Material(Shader.Find("Sprites/Default"));
                ring.sharedMaterial = ringMaterial;
                ring.startColor = ring.endColor = color;
            }
            FitRing();
            ring.enabled = true;
        }

        private void LateUpdate()
        {
            if (Target != null && ring != null)
                FitRing();
            else if (ring != null)
                ring.enabled = false;
        }

        private void FitRing()
        {
            Bounds bounds = new Bounds(Target.position, Vector3.zero);
            bool found = false;
            foreach (var renderer in targetRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                    renderer.GetComponent<TMPro.TMP_Text>() != null)
                    continue;
                if (!found) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            float x = found ? bounds.extents.x + padding : radius;
            float z = found ? bounds.extents.z + padding : radius;
            // Circumscribe the footprint so the ring clears rectangular solar panels.
            x = Mathf.Max(0.01f, x) * (found ? Mathf.Sqrt(2) : 1);
            z = Mathf.Max(0.01f, z) * (found ? Mathf.Sqrt(2) : 1);
            ring.transform.SetPositionAndRotation(found
                ? new Vector3(bounds.center.x, bounds.min.y + 0.01f, bounds.center.z)
                : Target.position, Quaternion.identity);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * x, 0, Mathf.Sin(angle) * z));
            }
        }

        public void Hide()
        {
            Target = null;
            if (ring != null)
                ring.enabled = false;
        }

        private void OnDestroy()
        {
            if (ringMaterial != null)
            {
                if (Application.isPlaying) Destroy(ringMaterial);
                else DestroyImmediate(ringMaterial);
            }
        }
    }
}
