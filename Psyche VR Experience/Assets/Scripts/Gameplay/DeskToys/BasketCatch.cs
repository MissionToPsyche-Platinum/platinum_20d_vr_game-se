using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Rewards a crumpled sheet landing in the waste basket. Sits on a trigger collider low
    /// inside the can. A ball that is crumpled and no longer held pulses the controller that
    /// threw it. Flat sheets, and balls carried in by hand, get nothing. One pulse per visit:
    /// leaving the trigger re-arms that ball, so fishing one out and dropping it back works.
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

        private readonly HashSet<PaperCrumple> _inside = new HashSet<PaperCrumple>();
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

            PaperCrumple paper = FindPaper(other);
            if (paper == null || !paper.IsCrumpled || paper.IsHeld)
                return;

            if (!_inside.Add(paper))
                return;

            XRBaseInputInteractor thrower = paper.LastHolder;
            if (thrower != null)
                thrower.SendHapticImpulse(landingHapticIntensity, landingHapticDuration);

            if (landingAudio != null && landingAudio.clip != null)
                landingAudio.Play();
        }

        private void OnTriggerExit(Collider other)
        {
            PaperCrumple paper = FindPaper(other);
            if (paper != null)
                _inside.Remove(paper);
        }

        private static PaperCrumple FindPaper(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<PaperCrumple>() : null;
        }
    }
}
