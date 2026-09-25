using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The text, and optionally the paper look, for one scrapped mission plan sheet.
    /// Assign one to the ScrappedPlanContentApplier on a ScrappedPlan instance to give
    /// that sheet its own fun fact while every sheet shares the same crumple behaviour,
    /// the way BookContent swaps a book's pages without touching its interaction.
    /// </summary>
    [CreateAssetMenu(menuName = "Psyche/Scrapped Plan Content", fileName = "NewScrappedPlanContent")]
    public class ScrappedPlanContent : ScriptableObject
    {
        [Tooltip("Large title line at the top of the sheet.")]
        public string headline = "Old Mission Notes";

        [Tooltip("Body text under the headline. A few short lines; it is read at arm's length in VR.")]
        [TextArea(3, 8)]
        public string body = "Do NOT Crumple";

        [Tooltip("Optional paper material override for this sheet. Empty leaves the sheet's current material unchanged.")]
        public Material sheetMaterial;
    }
}
