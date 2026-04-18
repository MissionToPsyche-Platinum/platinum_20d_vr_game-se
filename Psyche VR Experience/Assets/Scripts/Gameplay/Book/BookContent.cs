using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Holds all materials for a single book instance. Drop one of these on the
    /// BookContentApplier component on an InstructionManualBook prefab to swap
    /// the entire visual content while keeping all interaction behavior intact.
    /// </summary>
    [CreateAssetMenu(menuName = "Psyche/Book Content", fileName = "NewBookContent")]
    public class BookContent : ScriptableObject
    {
        [Header("Cover")]
        [Tooltip("Solid cover material (used on all non-textured cover faces).")]
        public Material cover;

        [Tooltip("Textured back-cover outer face (the back-of-book art).")]
        public Material coverPage;

        [Header("Spine")]
        [Tooltip("Solid spine material (5 of the 6 spine faces).")]
        public Material spineFace;

        [Tooltip("Textured spine outward face (book title etc.).")]
        public Material spineText;

        [Header("Pages")]
        [Tooltip("Shared paper-edge material applied to the thickness of all pages.")]
        public Material pageEdges;

        [System.Serializable]
        public class PageMats
        {
            public Material front;
            public Material back;
        }

        [Tooltip("Material pair for each of the 4 inner pages.")]
        public PageMats[] pages = new PageMats[4];
    }
}
