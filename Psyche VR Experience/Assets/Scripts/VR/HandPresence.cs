using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PsycheVR.Gameplay;

namespace PsycheVR.VR
{
    /// <summary>
    /// Job Simulator hands: the hand disappears while it holds an object, and so does its
    /// collider. The rule is by type. Selecting any
    /// <see cref="XRGrabInteractable"/> (every PsycheGrabbable, the book spine, the mug)
    /// hides the hand. A drawer handle keeps the hand in its normal fist. Selecting anything
    /// else (today only <c>BookPage</c>) keeps the hand and drives the pinch pose, and the hand model slides so its pinch sits on the middle
    /// of the page's outer edge, so a page flip reads as the hand turning it. When the
    /// interactor's last selection ends everything comes back. The dwell line belongs to
    /// <see cref="GrabTargeting"/>, which hides it whenever the
    /// hand holds or pinches something.
    /// Place on each controller GameObject in the XR rig, next to PsycheHandSetup.
    /// </summary>
    public class HandPresence : MonoBehaviour
    {
        [Tooltip("This hand's interactor. Auto-found in children if empty.")]
        [SerializeField] private NearFarInteractor interactor;

        [Tooltip("The hand mesh. Auto-found in children if empty.")]
        [SerializeField] private SkinnedMeshRenderer handRenderer;

        [Tooltip("Drives the pinch pose. Auto-found in children if empty.")]
        [SerializeField] private HandAnimator handAnimator;

        [Tooltip("The hand's collider proxy. Auto-found in children if empty.")]
        [SerializeField] private HandPhysics handPhysics;

        [Header("Hide On Grab")]
        [Tooltip("The glove hides once its fist is at least this closed, so part of the grab is seen.")]
        [SerializeField, Range(0f, 1f)] private float hideAtFist = 0.4f;

        [Tooltip("Seconds the glove stays visible after a grab at the least.")]
        [SerializeField] private float minHideDelay = 0.1f;

        [Tooltip("Seconds after a grab when the glove hides however far the fist has closed.")]
        [SerializeField] private float maxHideDelay = 0.3f;

        [Tooltip("Seconds the hand model takes to slide onto a pinched page's edge, and back.")]
        [SerializeField] private float pinchSnapSeconds = 0.1f;

        /// <summary>How far past the distal joint the fingertip sits, in metres (glove scale).</summary>
        private const float FingertipLength = 0.02f;

        private Transform _handModel;
        private Vector3 _handRestLocalPosition;
        private Transform _indexDistal, _indexIntermediate, _thumbDistal, _thumbProximal;
        private BookPage _pinchedPage;
        private float _pinchWeight;
        private bool _hidePending;
        private float _grabTime;
        private int _lastWeightFrame = -1;
        private Vector3 _edgePoint;

        /// <summary>True while the hand is hidden because it holds a grabbable.</summary>
        public bool IsHidden { get; private set; }

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInChildren<NearFarInteractor>(true);
            if (handAnimator == null) handAnimator = GetComponentInChildren<HandAnimator>(true);
            if (handPhysics == null) handPhysics = GetComponentInChildren<HandPhysics>(true);
            // The glove lives under the HandAnimator. A plain search from the controller would find
            // the poke interactor's pointer mesh first, which is what hid nothing on 2026-09-29.
            if (handRenderer == null && handAnimator != null)
                handRenderer = handAnimator.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (handRenderer == null) handRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (interactor == null || handRenderer == null)
            {
                Debug.LogError($"[HandPresence] Needs a NearFarInteractor and a hand mesh under {gameObject.name}.", this);
                enabled = false;
                return;
            }

            if (handAnimator != null)
            {
                _handModel = handAnimator.transform;
                _handRestLocalPosition = _handModel.localPosition;
                foreach (var t in _handModel.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.EndsWith("IndexDistal")) _indexDistal = t;
                    else if (t.name.EndsWith("IndexIntermediate")) _indexIntermediate = t;
                    else if (t.name.EndsWith("ThumbDistal")) _thumbDistal = t;
                    else if (t.name.EndsWith("ThumbProximal")) _thumbProximal = t;
                }
            }
        }

        private void OnEnable()
        {
            if (interactor == null) return;
            interactor.selectEntered.AddListener(OnSelectEntered);
            interactor.selectExited.AddListener(OnSelectExited);
            // After the tracked pose driver's own before-render update, so the offset lands
            // on the controller pose that is actually drawn.
            Application.onBeforeRender += ApplyPinchOffset;
        }

        private void OnDisable()
        {
            if (interactor == null) return;
            interactor.selectEntered.RemoveListener(OnSelectEntered);
            interactor.selectExited.RemoveListener(OnSelectExited);
            Application.onBeforeRender -= ApplyPinchOffset;
            _hidePending = false;
            SetHidden(false);
            SetPinch(0f);
            _pinchedPage = null;
            _pinchWeight = 0f;
            if (_handModel != null)
                _handModel.localPosition = _handRestLocalPosition;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactableObject is XRGrabInteractable)
            {
                // The collider goes at once so the incoming object does not hit the hand;
                // the glove itself waits in Update until part of the fist is seen.
                if (handPhysics != null)
                    handPhysics.CollisionEnabled = false;
                _hidePending = true;
                _grabTime = Time.time;
            }
            // A drawer handle keeps the visible hand in its normal grip-driven fist, not the pinch.
            else if (DrawerSlide.ForHandle(args.interactableObject) == null)
            {
                SetPinch(1f);
                _pinchedPage = args.interactableObject as BookPage;
            }
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            // XRI has already removed the interactable from the selection list here.
            if (interactor.hasSelection)
                return;

            _hidePending = false;
            _pinchedPage = null;
            SetHidden(false);
            SetPinch(0f);
        }

        private void Update()
        {
            if (!_hidePending)
                return;

            float elapsed = Time.time - _grabTime;
            float fist = handAnimator != null ? handAnimator.CurrentFist : 1f;
            if ((fist >= hideAtFist && elapsed >= minHideDelay) || elapsed >= maxHideDelay)
            {
                _hidePending = false;
                SetHidden(true);
            }
        }

        private void LateUpdate()
        {
            ApplyPinchOffset();
        }

        /// <summary>
        /// Slides the hand model so the point between the thumb and index tips sits on the
        /// pinched page's edge grip point; slides it back after release. Position only: the
        /// wrist keeps the controller's rotation.
        /// </summary>
        private void ApplyPinchOffset()
        {
            if (_handModel == null || _indexDistal == null || _thumbDistal == null)
                return;

            // LateUpdate and onBeforeRender both call this; advance the slide once per frame.
            if (Time.frameCount != _lastWeightFrame)
            {
                float step = pinchSnapSeconds > 0f ? Time.unscaledDeltaTime / pinchSnapSeconds : 1f;
                _pinchWeight = Mathf.MoveTowards(_pinchWeight, _pinchedPage != null ? 1f : 0f, step);
                _lastWeightFrame = Time.frameCount;
            }

            // Keep the last edge point after release so the slide back starts from it.
            if (_pinchedPage != null)
                _edgePoint = _pinchedPage.EdgeGripPoint;

            _handModel.localPosition = _handRestLocalPosition;
            if (_pinchWeight <= 0f)
                return;

            Vector3 pinch = 0.5f * (Fingertip(_indexDistal, _indexIntermediate) + Fingertip(_thumbDistal, _thumbProximal));
            _handModel.position += (_edgePoint - pinch) * _pinchWeight;
        }

        private static Vector3 Fingertip(Transform distal, Transform previous)
        {
            if (previous == null)
                return distal.position;
            Vector3 dir = distal.position - previous.position;
            return dir.sqrMagnitude > 1e-8f ? distal.position + dir.normalized * FingertipLength : distal.position;
        }

        private void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            handRenderer.enabled = !hidden;
            if (handPhysics != null)
                handPhysics.CollisionEnabled = !hidden;
        }

        private void SetPinch(float value)
        {
            if (handAnimator != null)
                handAnimator.Pinch = value;
        }
    }
}
