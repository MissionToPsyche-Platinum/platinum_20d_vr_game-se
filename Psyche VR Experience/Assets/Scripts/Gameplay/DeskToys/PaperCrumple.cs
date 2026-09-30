using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Crumples a held sheet of paper into a ball. Squeeze whichever hand control is
    /// <em>not</em> holding the sheet: grab with the grip and the trigger crumples, grab
    /// with the trigger and the grip crumples (the PenClicker pattern, reading the analog
    /// inputs because the project's digital Select action is bound to both controls).
    ///
    /// Crumpling is one-way. It swaps the visual children, swaps which root collider is
    /// enabled, and hands the Rigidbody from the Paper grab profile (drops near the hand)
    /// to the CrumpledPaper profile (throws hard) through PsycheGrabbable.SetProfile.
    /// The kiosk reset (scene reload) is what un-crumples everything.
    ///
    /// Both colliders must live on the root object: XRBaseInteractable.Awake gathers its
    /// colliders once with GetComponentsInChildren, which skips the inactive ball child.
    ///
    /// No sound yet: the crumple clip belongs to the second audio pass (TG-265).
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    public class PaperCrumple : MonoBehaviour
    {
        [Header("Sheet State")]
        [Tooltip("Visual root of the flat sheet. Active until crumpled.")]
        [SerializeField] private GameObject sheetVisual;

        [Tooltip("Root collider matching the flat sheet. Enabled until crumpled.")]
        [SerializeField] private Collider sheetCollider;

        [Header("Ball State")]
        [Tooltip("Visual root of the crumpled ball. Inactive until crumpled.")]
        [SerializeField] private GameObject ballVisual;

        [Tooltip("Root collider matching the ball. Disabled until crumpled.")]
        [SerializeField] private Collider ballCollider;

        [Header("Squeeze Input")]
        [Tooltip("How far the free control must be squeezed to crumple.")]
        [SerializeField, Range(0.1f, 1f)] private float pressThreshold = 0.6f;

        [Tooltip("How far it must fall back before a squeeze can register again. Below the press threshold so a shaky finger cannot chatter.")]
        [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.35f;

        [Header("Feedback")]
        [Tooltip("Haptic intensity of the crumple. A little stronger than the pen click.")]
        [SerializeField, Range(0f, 1f)] private float crumpleHapticIntensity = 0.3f;

        [Tooltip("Duration of the crumple pulse (seconds).")]
        [SerializeField] private float crumpleHapticDuration = 0.05f;

        [Tooltip("Optional. Left empty until TG-265 supplies a crumple clip.")]
        [SerializeField] private AudioSource crumpleAudio;

        /// <summary>
        /// Contact offset for the sheet collider. The 4 mm sheet is thinner than the 1 cm
        /// default contact offset, which makes a stack of sheets pop apart.
        /// </summary>
        private const float SheetContactOffset = 0.002f;

        private PsycheGrabbable _grabbable;
        private XRBaseInputInteractor _holder;
        private bool _squeezeIsTrigger;
        private bool _squeezeHeld;

        /// <summary>True once the sheet has become a ball.</summary>
        public bool IsCrumpled { get; private set; }

        /// <summary>True while a hand holds this object.</summary>
        public bool IsHeld => _holder != null;

        /// <summary>The last controller that held this object, for landing feedback after a throw.</summary>
        public XRBaseInputInteractor LastHolder { get; private set; }

        /// <summary>Raised once, when the sheet becomes a ball.</summary>
        public event Action<PaperCrumple> Crumpled;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();

            if (sheetVisual == null || sheetCollider == null || ballVisual == null || ballCollider == null)
            {
                Debug.LogError("[PaperCrumple] Sheet and ball visuals and colliders must all be assigned!", this);
                enabled = false;
                return;
            }

            sheetCollider.contactOffset = SheetContactOffset;

            // Force the authored start state so a prefab left mid-edit still starts flat.
            ApplyState(crumpled: false);
        }

        private void OnEnable()
        {
            _grabbable.selectEntered.AddListener(OnGrabbed);
            _grabbable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            _grabbable.selectEntered.RemoveListener(OnGrabbed);
            _grabbable.selectExited.RemoveListener(OnReleased);
            _holder = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            releaseThreshold = Mathf.Min(releaseThreshold, pressThreshold);
        }
#endif

        /// <summary>
        /// Works out which control did the grabbing so the other one becomes the squeeze.
        /// Whichever analog reads higher at the moment of the grab is the one holding on.
        /// </summary>
        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject as XRBaseInputInteractor;
            if (_holder == null)
                return;

            LastHolder = _holder;

            float grip = _holder.selectInput.ReadValue();
            float trigger = _holder.activateInput.ReadValue();

            _squeezeIsTrigger = grip >= trigger;

            // If the free control is already squeezed on pickup, demand a release first,
            // so grabbing with both fingers clenched does not crumple by itself.
            _squeezeHeld = ReadSqueezeValue() >= pressThreshold;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _holder = null;
            _squeezeHeld = false;
        }

        private void Update()
        {
            if (_holder == null || IsCrumpled)
                return;

            float value = ReadSqueezeValue();

            if (!_squeezeHeld && value >= pressThreshold)
            {
                _squeezeHeld = true;
                Crumple();
            }
            else if (_squeezeHeld && value <= releaseThreshold)
            {
                _squeezeHeld = false;
            }
        }

        private float ReadSqueezeValue()
        {
            if (_holder == null)
                return 0f;

            return _squeezeIsTrigger
                ? _holder.activateInput.ReadValue()
                : _holder.selectInput.ReadValue();
        }

        /// <summary>
        /// Turns the sheet into a ball. Public so a designer event or test can force it.
        /// Does nothing if already crumpled, or if the component disabled itself in Awake
        /// because a reference was missing.
        /// </summary>
        public void Crumple()
        {
            if (!enabled)
                return;

            if (IsCrumpled)
                return;

            IsCrumpled = true;
            ApplyState(crumpled: true);

            if (_holder != null)
                _holder.SendHapticImpulse(crumpleHapticIntensity, crumpleHapticDuration);

            if (crumpleAudio != null && crumpleAudio.clip != null)
                crumpleAudio.Play();

            Crumpled?.Invoke(this);
        }

        private void ApplyState(bool crumpled)
        {
            // The profile swap carries the physics: Paper drops near the hand, Ball throws
            // hard. PsycheGrabbable keeps gravity off while held and XRI restores it on release.
            _grabbable.SetProfile(crumpled ? GrabProfileKind.CrumpledPaper : GrabProfileKind.Paper);

            sheetCollider.enabled = !crumpled;
            ballCollider.enabled = crumpled;
            sheetVisual.SetActive(!crumpled);
            ballVisual.SetActive(crumpled);
        }
    }
}
