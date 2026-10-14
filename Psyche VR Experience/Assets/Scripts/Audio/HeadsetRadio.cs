using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using PsycheVR.Data;

namespace PsycheVR.Audio
{
    /// <summary>
    /// The mission headset's audio, Psyche's radio signal as Quindar beeps over light static on
    /// loop: silent on the desk, starting from the top on each pick-up, faint at arm's length and
    /// full when the headset reaches the visitor's ears. The ramp is even in loudness (decibels), so
    /// the audio keeps growing all the way in. Fades rather than cuts, except that a new pick-up
    /// restarts the clip.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class HeadsetRadio : MonoBehaviour
    {
        [Tooltip("The grab that counts as holding the headset. Found on this object if empty.")]
        [SerializeField] private XRBaseInteractable grabbable;

        [Tooltip("The visitor's head. Uses the main camera if empty.")]
        [SerializeField] private Transform head;

        [Tooltip("Volume while held at arm's length.")]
        [SerializeField, Range(0.001f, 1f)] private float heldVolume = 0.03f;

        [Tooltip("Volume when the headset is at the ear.")]
        [SerializeField, Range(0.001f, 1f)] private float nearHeadVolume = 1f;

        [Tooltip("At or within this head distance (m) the loop plays at the near-head volume.")]
        [SerializeField] private float nearDistance = 0.12f;

        [Tooltip("At or beyond this head distance (m) the loop plays at the held volume.")]
        [SerializeField] private float farDistance = 0.7f;

        [Tooltip("Volume change per second, so grabbing and letting go fade instead of cutting.")]
        [SerializeField] private float fadePerSecond = 2.5f;

        /// <summary>Closeness (0 at arm's length, 1 at the ear) that counts as putting the headset on.</summary>
        private const float AtEarCloseness = 0.85f;

        private AudioSource _source;
        private bool _wasHeld;
        private bool _loggedAtEar;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.volume = 0f;
            if (grabbable == null)
                grabbable = GetComponent<XRBaseInteractable>();
        }

        private void OnDisable()
        {
            _wasHeld = false;
            if (_source != null)
            {
                _source.Stop();
                _source.volume = 0f;
            }
        }

        private void Update()
        {
            bool held = grabbable != null && grabbable.isSelected;
            if (held && !_wasHeld)
            {
                _source.Stop();   // each pick-up starts the transmissions from the first one
                _loggedAtEar = false;
            }
            _wasHeld = held;

            float target = TargetVolume();
            _source.volume = Mathf.MoveTowards(_source.volume, target, fadePerSecond * Time.deltaTime);

            if (_source.volume > 0f && !_source.isPlaying)
                _source.Play();
            else if (_source.volume <= 0f && _source.isPlaying)
                _source.Stop();
        }

        private float TargetVolume()
        {
            if (grabbable == null || !grabbable.isSelected)
                return 0f;

            var headTransform = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
            if (headTransform == null)
                return heldVolume;

            float distance = Vector3.Distance(transform.position, headTransform.position);
            float closeness = Mathf.InverseLerp(farDistance, nearDistance, distance);
            if (closeness >= AtEarCloseness && !_loggedAtEar)
            {
                _loggedAtEar = true;
                SessionEvents.Interaction("headset_to_ear", this);
            }
            // Equal steps in decibels: linear volume would jump early and flatten out near the ear.
            return heldVolume * Mathf.Pow(nearHeadVolume / heldVolume, closeness);
        }
    }
}
