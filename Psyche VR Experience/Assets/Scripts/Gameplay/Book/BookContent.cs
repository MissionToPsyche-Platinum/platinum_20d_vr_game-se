using UnityEngine;
using UnityEngine.Video;

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

        [Header("Page video (optional)")]
        [Tooltip("Loops, without sound, in a window on one page face while the book is held (BookPageVideo). " +
                 "The page image shows a still in the same window the rest of the time.")]
        public VideoClip pageVideo;

        [Tooltip("Inner page (1-4) that carries the video.")]
        [Range(1, 4)] public int videoPage = 1;

        [Tooltip("True: the video is on that page's front face; false: its back face.")]
        public bool videoOnFront = true;

        [Tooltip("The video window in the page image, in UV space: 0..1, origin bottom left.")]
        public Rect videoWindow;

        /// <summary>The material of the face that carries the video, or null without one.</summary>
        public Material VideoFaceMaterial
        {
            get
            {
                if (pageVideo == null || pages == null || videoPage < 1 || videoPage > pages.Length) return null;
                var page = pages[videoPage - 1];
                return page == null ? null : videoOnFront ? page.front : page.back;
            }
        }
    }
}
