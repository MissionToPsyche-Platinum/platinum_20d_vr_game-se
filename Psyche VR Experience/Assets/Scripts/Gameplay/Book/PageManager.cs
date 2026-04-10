using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Coordinates book interaction: manages which pages are interactable,
    /// and toggles the book grab collider so the second hand can reach pages.
    /// Pages are only interactable while the book is held.
    /// </summary>
    public class PageManager : MonoBehaviour
    {
        [Tooltip("All pages in flip order: front cover, inner pages, back cover.")]
        [SerializeField] private List<BookPage> pages = new List<BookPage>();

        [Tooltip("The large collider used for grabbing the whole book. " +
                 "Disabled while book is held so the second hand can reach pages.")]
        [SerializeField] private Collider bookGrabCollider;

        private PsycheGrabbable _grabbable;
        private bool _isBookHeld;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();
        }

        private void Start()
        {
            // Pages start disabled — only interactable when book is held.
            SetAllPagesEnabled(false);
        }

        private void OnEnable()
        {
            _grabbable.selectEntered.AddListener(OnBookGrabbed);
            _grabbable.selectExited.AddListener(OnBookReleased);
        }

        private void OnDisable()
        {
            _grabbable.selectEntered.RemoveListener(OnBookGrabbed);
            _grabbable.selectExited.RemoveListener(OnBookReleased);
        }

        private void OnBookGrabbed(SelectEnterEventArgs args)
        {
            _isBookHeld = true;
            // Hide book collider so the second hand can reach page colliders.
            if (bookGrabCollider != null)
                bookGrabCollider.enabled = false;
            RefreshInteractablePages();
        }

        private void OnBookReleased(SelectExitEventArgs args)
        {
            _isBookHeld = false;
            // Re-enable book collider for future grabs.
            if (bookGrabCollider != null)
                bookGrabCollider.enabled = true;
            SetAllPagesEnabled(false);
        }

        /// <summary>Called by BookPage on grab. Hook for future features.</summary>
        public void OnPageGrabbed(BookPage page) { }

        /// <summary>Called by BookPage after release. Refreshes interactable state.</summary>
        public void OnPageReleased(BookPage page)
        {
            RefreshInteractablePages();
        }

        /// <summary>
        /// Enable only the topmost page per side.
        /// First unflipped = right side active. Last flipped = left side active.
        /// </summary>
        public void RefreshInteractablePages()
        {
            if (!_isBookHeld)
            {
                SetAllPagesEnabled(false);
                return;
            }

            BookPage activeRight = null;
            BookPage activeLeft = null;

            for (int i = 0; i < pages.Count; i++)
            {
                if (!pages[i].IsFlipped && activeRight == null)
                    activeRight = pages[i];
                if (pages[i].IsFlipped)
                    activeLeft = pages[i];
            }

            for (int i = 0; i < pages.Count; i++)
                pages[i].enabled = pages[i] == activeRight || pages[i] == activeLeft;
        }

        private void SetAllPagesEnabled(bool enabled)
        {
            for (int i = 0; i < pages.Count; i++)
                pages[i].enabled = enabled;
        }
    }
}
