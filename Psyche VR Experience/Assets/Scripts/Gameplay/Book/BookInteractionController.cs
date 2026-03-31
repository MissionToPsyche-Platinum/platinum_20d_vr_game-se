using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Bridges XRI grab events to the Dandarawy BookPageCurl system.
    /// Tracks page state, manages interaction layer separation between
    /// grab and page-flip inputs, and exposes a public API for page navigation.
    /// Attach to the root book GameObject alongside PsycheGrabbable.
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    public class BookInteractionController : MonoBehaviour
    {
        [Header("Dandarawy References")]
        [Tooltip("AutoFlip component on the Book child object.")]
        [SerializeField] private AutoFlip autoFlip;

        [Tooltip("Book component on the Book child object.")]
        [SerializeField] private Book book;

        [Header("Interaction Layers")]
        [Tooltip("The BookUI interaction layer used for page-flip ray interaction.")]
        [SerializeField] private InteractionLayerMask bookUILayer;

        [Header("GoToPage Settings")]
        [Tooltip("Delay in seconds between sequential page flips when using GoToPage.")]
        [SerializeField] private float goToPageFlipDelay = 0.4f;

        private PsycheGrabbable _grabbable;
        private int _currentPage;
        private Coroutine _goToPageCoroutine;
        private IXRSelectInteractor _holdingInteractor;
        private InteractionLayerMask _holdingInteractorOriginalLayers;

        /// <summary>Current page index (0-based). Synced from Dandarawy Book on each flip.</summary>
        public int CurrentPage => _currentPage;

        /// <summary>Total number of page sprites in the book.</summary>
        public int PageCount => book != null ? book.bookPages.Length : 0;

        /// <summary>Whether the book is currently held by a hand.</summary>
        public bool IsHeld { get; private set; }

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();

            if (autoFlip == null)
            {
                Debug.LogError("[BookInteractionController] AutoFlip reference is not assigned!", this);
                enabled = false;
                return;
            }

            if (book == null)
            {
                Debug.LogError("[BookInteractionController] Book reference is not assigned!", this);
                enabled = false;
                return;
            }

            autoFlip.AutoStartFlip = false;
        }

        private void OnEnable()
        {
            _grabbable.selectEntered.AddListener(OnGrabbed);
            _grabbable.selectExited.AddListener(OnReleased);
            book.OnFlip.AddListener(OnPageFlipped);
        }

        private void OnDisable()
        {
            _grabbable.selectEntered.RemoveListener(OnGrabbed);
            _grabbable.selectExited.RemoveListener(OnReleased);
            book.OnFlip.RemoveListener(OnPageFlipped);
        }

        /// <summary>Flip to the next page. No-op if already on the last page.</summary>
        public void NextPage()
        {
            if (_currentPage >= PageCount - 1)
                return;

            autoFlip.FlipRightPage();
        }

        /// <summary>Flip to the previous page. No-op if already on the first page.</summary>
        public void PrevPage()
        {
            if (_currentPage <= 0)
                return;

            autoFlip.FlipLeftPage();
        }

        /// <summary>
        /// Flip sequentially to the target page. Clamped to valid range.
        /// Cancels any in-progress GoToPage operation.
        /// </summary>
        public void GoToPage(int targetPage)
        {
            targetPage = Mathf.Clamp(targetPage, 0, PageCount - 1);

            if (targetPage == _currentPage)
                return;

            if (_goToPageCoroutine != null)
                StopCoroutine(_goToPageCoroutine);

            _goToPageCoroutine = StartCoroutine(GoToPageSequence(targetPage));
        }

        private IEnumerator GoToPageSequence(int targetPage)
        {
            var wait = new WaitForSeconds(goToPageFlipDelay);

            while (_currentPage != targetPage)
            {
                if (targetPage > _currentPage)
                    autoFlip.FlipRightPage();
                else
                    autoFlip.FlipLeftPage();

                yield return wait;
            }

            _goToPageCoroutine = null;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            IsHeld = true;
            _holdingInteractor = args.interactorObject;

            // Save the holding interactor's original layer mask, then exclude
            // BookUI so it cannot trigger page flips while holding the book.
            if (_holdingInteractor is XRBaseInteractor baseInteractor)
            {
                _holdingInteractorOriginalLayers = baseInteractor.interactionLayers;
                baseInteractor.interactionLayers &= ~bookUILayer;
            }

            // Enable BookUI layer on the grab interactable so the free hand's
            // ray interactor can reach the canvas hotspots.
            _grabbable.interactionLayers |= bookUILayer;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            IsHeld = false;

            // Restore the holding interactor's original layer mask.
            if (_holdingInteractor is XRBaseInteractor baseInteractor)
            {
                baseInteractor.interactionLayers = _holdingInteractorOriginalLayers;
            }

            // Remove BookUI layer from the grab interactable.
            _grabbable.interactionLayers &= ~bookUILayer;

            _holdingInteractor = null;
        }

        private void OnPageFlipped()
        {
            _currentPage = book.currentPage;
        }
    }
}
