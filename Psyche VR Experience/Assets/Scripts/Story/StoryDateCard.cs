using TMPro;
using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Minimal world-space date label, pending the final story UI prefab.</summary>
    public sealed class StoryDateCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional scene anchor for the card. Otherwise place it relative to the current camera on each Show.")]
        [SerializeField] private Transform placementAnchor;
        [SerializeField] private Vector3 viewOffset = new Vector3(0, 0.25f, 1.5f);
        public string CurrentText { get; private set; }
        public bool IsVisible { get; private set; }

        public void Show(string text)
        {
            CurrentText = text;
            if (label == null)
            {
                var go = new GameObject("Story date label");
                go.transform.SetParent(transform, false);
                label = go.AddComponent<TextMeshPro>();
                label.rectTransform.sizeDelta = new Vector2(1.2f, 0.25f);
                label.fontSize = 0.4f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
            }
            // Reposition on every entry, including after a teleport or restart.
            if (placementAnchor != null)
                label.transform.SetPositionAndRotation(placementAnchor.position, placementAnchor.rotation);
            else if (Camera.main != null)
                label.transform.SetPositionAndRotation(Camera.main.transform.TransformPoint(viewOffset), Camera.main.transform.rotation);
            label.text = text;
            label.gameObject.SetActive(true);
            IsVisible = true;
        }

        public void Hide()
        {
            if (label != null)
                label.gameObject.SetActive(false);
            IsVisible = false;
        }
    }
}
