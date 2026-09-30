using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Rewards a ball landing in a basket. Sits on a trigger collider low inside the waste can
    /// or just under a hoop's rim. A crumpled sheet, or a basketball, that is no longer held
    /// pulses the controller that threw it. Flat sheets, and balls carried in by hand, get
    /// nothing. One pulse per visit: leaving the trigger re-arms that ball, so fishing one out
    /// and dropping it back works.
    ///
    /// No sound yet: the landing clip belongs to the second audio pass (TG-265).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BasketCatch : MonoBehaviour
    {
        [Header("Haptics")]
        [Tooltip("Haptic intensity on the throwing controller when a ball lands. Lighter than the grab pulse.")]
        [SerializeField, Range(0f, 1f)] private float landingHapticIntensity = 0.25f;

        [Tooltip("Duration of the landing pulse (seconds).")]
        [SerializeField] private float landingHapticDuration = 0.08f;

        [Header("Audio")]
        [Tooltip("Optional. Left empty until TG-265 supplies a landing clip.")]
        [SerializeField] private AudioSource landingAudio;

        private readonly HashSet<Rigidbody> _inside = new HashSet<Rigidbody>();
        private bool _valid;

        private void Awake()
        {
            _valid = GetComponent<Collider>().isTrigger;
            if (!_valid)
                Debug.LogError("[BasketCatch] The collider must be a trigger.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_valid)
                return;

            Rigidbody body = other.attachedRigidbody;
            XRBaseInputInteractor thrower;
            if (body == null || !IsScoringBall(body, out thrower))
                return;

            if (!_inside.Add(body))
                return;

            if (thrower != null)
                thrower.SendHapticImpulse(landingHapticIntensity, landingHapticDuration);

            if (landingAudio != null && landingAudio.clip != null)
                landingAudio.Play();
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body != null)
                _inside.Remove(body);
        }

        /// <summary>
        /// A crumpled, unheld sheet or an unheld basketball scores; anything else does not.
        /// </summary>
        private static bool IsScoringBall(Rigidbody body, out XRBaseInputInteractor thrower)
        {
            thrower = null;

            PaperCrumple paper = body.GetComponent<PaperCrumple>();
            if (paper != null)
            {
                if (!paper.IsCrumpled || paper.IsHeld)
                    return false;
                thrower = paper.LastHolder;
                return true;
            }

            PsycheGrabbable grabbable = body.GetComponent<PsycheGrabbable>();
            if (grabbable == null || grabbable.Profile != GrabProfileKind.Ball || grabbable.isSelected)
                return false;

            thrower = grabbable.LastHolder;
            return true;
        }
    }
}
