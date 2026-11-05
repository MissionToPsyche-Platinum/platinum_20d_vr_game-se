using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Temporary objective ring, pending the final glow prefab.</summary>
    public sealed class ObjectiveHighlight : MonoBehaviour
    {
        [SerializeField] private Color color = Color.cyan;
        [SerializeField, Min(0.01f)] private float radius = 0.15f;
        private LineRenderer ring;
        private Material ringMaterial;
        public Transform Target { get; private set; }

        public void Show(Transform target)
        {
            Hide();
            Target = target;
            if (target == null)
                return; // The spacecraft assignment is intentionally pending.

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
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                }
            }
            ring.transform.position = target.position;
            ring.enabled = true;
        }

        private void LateUpdate()
        {
            if (Target != null && ring != null)
                ring.transform.position = Target.position;
            else if (ring != null)
                ring.enabled = false;
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
