using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Automatically opens the book when grabbed and closes it when released.
    /// On grab: animates covers and pages to preset open angles.
    /// On release: animates everything back to 0 (closed).
    /// </summary>
    public class BookAutoOpen : MonoBehaviour
    {
        [Header("Pages")]
        [Tooltip("Pages that flip forward on grab (Cover_Front + inner pages).")]
        [SerializeField] private List<BookPage> forwardPages = new List<BookPage>();

        [Tooltip("Pages that flip backward on grab (Cover_Back).")]
        [SerializeField] private List<BookPage> backwardPages = new List<BookPage>();

        [Header("Angles")]
        [Tooltip("Target angle for forward pages when open.")]
        [SerializeField] private float forwardAngle = 70f;

        [Tooltip("Target angle for backward pages when open.")]
        [SerializeField] private float backwardAngle = -70f;

        [Header("Animation")]
        [Tooltip("SmoothDamp smooth time in seconds. Lower = faster animation.")]
        [SerializeField] private float smoothTime = 0.25f;

        private const float ConvergenceThreshold = 0.5f;

        private enum State { Idle, Opening, Closing }

        private PsycheGrabbable _grabbable;
        private PageManager _pageManager;
        private State _state = State.Idle;
        private float[] _velocities;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();
            _pageManager = GetComponent<PageManager>();
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
            int totalPages = forwardPages.Count + backwardPages.Count;
            if (totalPages == 0) return;

            _velocities = new float[totalPages];
            _state = State.Opening;
            _pageManager.IsAutoOpening = true;

            // Force-disable pages now in case PageManager's listener already
            // ran RefreshInteractablePages before IsAutoOpening was set.
            _pageManager.RefreshInteractablePages();
        }

        private void OnBookReleased(SelectExitEventArgs args)
        {
            int totalPages = forwardPages.Count + backwardPages.Count;
            if (totalPages == 0) return;

            _velocities = new float[totalPages];
            _state = State.Closing;
            _pageManager.IsAutoOpening = false;
        }

        private void Update()
        {
            if (_state == State.Idle) return;

            bool isOpening = _state == State.Opening;
            float fwdTarget = isOpening ? forwardAngle : 0f;
            float bwdTarget = isOpening ? backwardAngle : 0f;

            bool allDone = true;
            int idx = 0;

            for (int i = 0; i < forwardPages.Count; i++, idx++)
            {
                if (!AnimatePage(forwardPages[i], fwdTarget, idx))
                    allDone = false;
            }

            for (int i = 0; i < backwardPages.Count; i++, idx++)
            {
                if (!AnimatePage(backwardPages[i], bwdTarget, idx))
                    allDone = false;
            }

            if (allDone)
            {
                if (isOpening)
                {
                    _pageManager.IsAutoOpening = false;
                    _pageManager.RefreshInteractablePages();
                }
                _state = State.Idle;
            }
        }

        private bool AnimatePage(BookPage page, float target, int velocityIndex)
        {
            float current = page.CurrentAngle;
            if (Mathf.Abs(current - target) <= ConvergenceThreshold)
            {
                page.SetAngle(target);
                return true;
            }

            float vel = _velocities[velocityIndex];
            float newAngle = Mathf.SmoothDamp(current, target, ref vel, smoothTime);
            _velocities[velocityIndex] = vel;
            page.SetAngle(newAngle);
            return false;
        }
    }
}
