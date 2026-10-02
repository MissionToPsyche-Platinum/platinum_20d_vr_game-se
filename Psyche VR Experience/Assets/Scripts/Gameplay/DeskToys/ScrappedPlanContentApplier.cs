using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Pushes a ScrappedPlanContent asset onto a sheet: headline and body text, plus the
    /// paper material when the content overrides it. Lives on the ScrappedPlan prefab
    /// root and runs in edit mode too, so changing the content asset in the inspector
    /// updates the sheet immediately (the BookContentApplier pattern).
    /// </summary>
    [ExecuteAlways]
    public class ScrappedPlanContentApplier : MonoBehaviour
    {
        [Tooltip("The fact this sheet shows.")]
        [SerializeField] private ScrappedPlanContent content;

        [Header("Targets")]
        [Tooltip("Headline text on the top face of the sheet.")]
        [SerializeField] private TMP_Text headlineText;

        [Tooltip("Body text under the headline.")]
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("The sheet's renderer, for the optional material override.")]
        [SerializeField] private Renderer sheetRenderer;

        public ScrappedPlanContent Content
        {
            get => content;
            set { content = value; Apply(); }
        }

        private void OnEnable()
        {
            // Defer in the editor so the hierarchy is fully ready.
#if UNITY_EDITOR
            EditorApplication.delayCall += DelayedApply;
#else
            Apply();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall += DelayedApply;
        }

        private void DelayedApply()
        {
            // The object may have been destroyed between the delay and the call.
            if (this == null) return;
            Apply();
        }
#endif

        [ContextMenu("Apply Content")]
        public void Apply()
        {
            if (content == null)
            {
                Debug.LogWarning($"[ScrappedPlanContentApplier] No content assigned on '{name}'.", this);
                return;
            }

            // Unlike BookContentApplier this does not record the changes as prefab overrides
            // (no Undo/SetDirty): the text is re-applied on every enable, so the scene file
            // stays free of per-instance text churn and edits to the content asset propagate.
            if (headlineText != null) headlineText.text = content.headline;
            if (bodyText != null) bodyText.text = content.body;

            if (sheetRenderer != null && content.sheetMaterial != null)
                sheetRenderer.sharedMaterial = content.sheetMaterial;
        }
    }
}
