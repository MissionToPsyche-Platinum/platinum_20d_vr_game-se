using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Retractable-pen click. While the pen is held, pressing whichever of the two
    /// hand controls is <em>not</em> holding it toggles the nib between retracted and
    /// extended. Grab with the grip and the trigger clicks; grab with the trigger and
    /// the grip clicks. Nothing has to be learned or remembered either way.
    ///
    /// This reads the two analog inputs rather than the interactor's activate event.
    /// The project's digital Select action is bound to both the grip button and the
    /// trigger button, so it cannot tell them apart, and the digital Activate action
    /// has no bindings at all. The analog readers can: the interactor's select input
    /// is bound only to the grip axis and its activate input only to the trigger axis.
    ///
    /// The button at the top dips and springs back on every click; the nib stays where
    /// the click left it. Both are driven by offsets from wherever the model author
    /// placed them, so re-exporting the pen mesh does not require touching these
    /// numbers. The authored position of the nib is the retracted state: the mesh
    /// ships with the nib parked inside the nose.
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    public class PenClicker : MonoBehaviour
    {
        [Header("Moving Parts")]
        [Tooltip("The clicker button at the top of the pen.")]
        [SerializeField] private Transform clickButton;

        [Tooltip("The nib that pops out of the writing end.")]
        [SerializeField] private Transform nib;

        [Header("Travel")]
        [Tooltip("Offset the button moves by while pressed, in the pen's local axes. The pen model runs along local Z.")]
        [SerializeField] private Vector3 buttonPressedOffset = new Vector3(0f, 0f, -0.004f);

        [Tooltip("Offset the nib moves by when extended. The model parks the nib inside the nose, so this is how far the point sticks out.")]
        [SerializeField] private Vector3 nibExtendedOffset = new Vector3(0f, 0f, -0.010f);

        [Tooltip("Seconds for either part to reach its target. Short values read as a snap.")]
        [SerializeField, Range(0.01f, 0.3f)] private float travelDuration = 0.06f;

        [Header("Click Input")]
        [Tooltip("How far the clicking control must be squeezed to count as a press.")]
        [SerializeField, Range(0.1f, 1f)] private float pressThreshold = 0.6f;

        [Tooltip("How far it must fall back before another click registers. Below the press threshold, so a shaky finger cannot chatter.")]
        [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.35f;

        [Header("Haptics")]
        [Tooltip("Haptic intensity of the click itself. Deliberately lighter than the grab pulse.")]
        [SerializeField, Range(0f, 1f)] private float clickHapticIntensity = 0.25f;

        [Tooltip("Duration of the click pulse (seconds).")]
        [SerializeField] private float clickHapticDuration = 0.05f;

        private PsycheGrabbable _grabbable;
        private XRBaseInputInteractor _holder;
        private bool _clickIsTrigger;
        private bool _clickHeld;
        private Vector3 _buttonRestPosition;
        private Vector3 _nibRetractedPosition;
        private bool _isExtended;
        private Coroutine _buttonRoutine;
        private Coroutine _nibRoutine;

        /// <summary>True while the nib is out.</summary>
        public bool IsExtended => _isExtended;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();

            if (clickButton == null || nib == null)
            {
                Debug.LogError("[PenClicker] Click button and nib must both be assigned!", this);
                enabled = false;
                return;
            }

            _buttonRestPosition = clickButton.localPosition;
            _nibRetractedPosition = nib.localPosition;
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

        /// <summary>
        /// Works out which control did the grabbing, so the other one becomes the
        /// clicker. Whichever analog reads higher at the moment of the grab is the
        /// one the hand is squeezing.
        /// </summary>
        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject as XRBaseInputInteractor;
            if (_holder == null)
                return;

            float grip = _holder.selectInput.ReadValue();
            float trigger = _holder.activateInput.ReadValue();

            _clickIsTrigger = grip >= trigger;
            _clickHeld = ReadClickValue() >= pressThreshold;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _holder = null;
            _clickHeld = false;
        }

        private void Update()
        {
            if (_holder == null)
                return;

            float value = ReadClickValue();

            if (!_clickHeld && value >= pressThreshold)
            {
                _clickHeld = true;
                Click();
                _holder.SendHapticImpulse(clickHapticIntensity, clickHapticDuration);
            }
            else if (_clickHeld && value <= releaseThreshold)
            {
                _clickHeld = false;
            }
        }

        private float ReadClickValue()
        {
            if (_holder == null)
                return 0f;

            return _clickIsTrigger
                ? _holder.activateInput.ReadValue()
                : _holder.selectInput.ReadValue();
        }

        /// <summary>
        /// Toggles the nib and dips the button. Public so a future desk reset can put
        /// the pen back in its starting state.
        /// </summary>
        public void Click()
        {
            _isExtended = !_isExtended;

            Vector3 nibTarget = _isExtended
                ? _nibRetractedPosition + nibExtendedOffset
                : _nibRetractedPosition;

            Restart(ref _nibRoutine, MoveTo(nib, nibTarget));
            Restart(ref _buttonRoutine, PressButton());
        }

        private void Restart(ref Coroutine routine, IEnumerator next)
        {
            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(next);
        }

        private IEnumerator PressButton()
        {
            yield return MoveTo(clickButton, _buttonRestPosition + buttonPressedOffset);
            yield return MoveTo(clickButton, _buttonRestPosition);
        }

        private IEnumerator MoveTo(Transform part, Vector3 target)
        {
            Vector3 start = part.localPosition;
            float elapsed = 0f;

            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                part.localPosition = Vector3.Lerp(start, target, elapsed / travelDuration);
                yield return null;
            }

            part.localPosition = target;
        }
    }
}
