using TMPro;
using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Minimal world-space date label, pending the final story UI prefab.</summary>
    public sealed class StoryDateCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
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
                label.fontSize = 1.5f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                // Place the temporary card in front of the player at entry.
                var camera = Camera.main;
                if (camera != null)
                {
                    go.transform.position = camera.transform.TransformPoint(new Vector3(0, 0.25f, 1.5f));
                    go.transform.rotation = camera.transform.rotation;
                }
            }
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
