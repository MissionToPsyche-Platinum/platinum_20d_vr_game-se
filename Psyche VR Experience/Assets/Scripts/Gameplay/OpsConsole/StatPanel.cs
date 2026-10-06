using TMPro;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A stat block filling one monitor panel: up to three stats stacked top to bottom, evenly spaced,
    /// each a large white value in the title font with a light grey label under it.
    /// </summary>
    public class StatPanel : MonoBehaviour
    {
        private const int MaxStats = 3;
        private const float ValueSize = 20f;           // mm
        private const float LabelSize = 8f;            // mm
        private const float Split = 0.45f;             // share of a slot below the value's baseline
        private const float Pad = 4f;                  // mm

        [SerializeField] private RectTransform[] slots;
        [SerializeField] private TMP_Text[] values;
        [SerializeField] private TMP_Text[] labels;

        /// <summary>Creates an empty stat panel stretched over <paramref name="parent"/>.</summary>
        public static StatPanel Create(RectTransform parent, MonitorContent content)
        {
            var go = new GameObject("StatPanel", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * Pad; rt.offsetMax = -Vector2.one * Pad;
            var panel = go.AddComponent<StatPanel>();
            panel.slots = new RectTransform[MaxStats];
            panel.values = new TMP_Text[MaxStats];
            panel.labels = new TMP_Text[MaxStats];
            for (int i = 0; i < MaxStats; i++)
            {
                var slot = new GameObject($"Stat{i}", typeof(RectTransform));
                slot.layer = go.layer;
                panel.slots[i] = (RectTransform)slot.transform;
                panel.slots[i].SetParent(rt, false);
                panel.values[i] = MonitorScreen.Text(panel.slots[i], "Value", content != null ? content.titleFont : null,
                    ValueSize, TextAlignmentOptions.Bottom, MonitorPalette.White);
                Fit(panel.values[i].rectTransform, Split, 1f);
                panel.labels[i] = MonitorScreen.Text(panel.slots[i], "Label", content != null ? content.bodyFont : null,
                    LabelSize, TextAlignmentOptions.Top, MonitorPalette.LightGrey);
                Fit(panel.labels[i].rectTransform, 0f, Split);
                slot.SetActive(false);
            }
            return panel;
        }

        /// <summary>
        /// Shows up to three stats spaced evenly down the panel. Null entries are skipped (no empty slot);
        /// stats past the third are ignored.
        /// </summary>
        public void Set(MonitorContent.Stat[] stats)
        {
            var shown = stats == null ? new MonitorContent.Stat[0] : System.Array.FindAll(stats, s => s != null);
            int n = Mathf.Min(shown.Length, MaxStats);
            for (int i = 0; i < MaxStats; i++)
            {
                bool used = i < n;
                slots[i].gameObject.SetActive(used);
                if (!used) continue;
                Fit(slots[i], 1f - (i + 1f) / n, 1f - (float)i / n);
                values[i].text = shown[i].value ?? "";
                labels[i].text = shown[i].label ?? "";
            }
        }

        private static void Fit(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
