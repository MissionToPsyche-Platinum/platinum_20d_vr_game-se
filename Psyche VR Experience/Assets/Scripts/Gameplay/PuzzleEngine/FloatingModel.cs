using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PsycheVR.Data;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The finished spacecraft model hovering over the lowered pedestal (TG-297). <see cref="PedestalSpin"/>
    /// makes it on completion: the placed pieces go under this pivot (at the model's centre), and it glides
    /// off the pedestal by the offset it is given (toward the video screen in the Event room, so it neither
    /// rises into the video nor crowds the message board), banking into the tilt it is given as it goes, then
    /// bobs and drifts round slowly. A grab during the glide keeps whatever tilt it had reached. Grab it and it turns on every axis like a
    /// globe held by its surface: the point under the hand follows the hand round the model's centre
    /// (swing), and a twist of the wrist about that same line turns it about the line (twist). Only the
    /// wrist's twist is added, not its whole rotation, so moving the hand round while the wrist turns with it
    /// does not count twice. The model stays where it is, so letting go leaves it hovering; the bob eases out
    /// while it is held so the centre stays still under the hand. No drag-to-spin: that is the pedestal's.
    /// </summary>
    public class FloatingModel : MonoBehaviour, ISessionInteractable
    {
        private const float RiseSeconds = 1.9f;       // the glide off the pedestal
        private const float BobAmplitude = 0.008f;    // m
        private const float BobPeriod = 3.2f;          // s
        private const float DriftSpeed = 9f;           // degrees per second about the vertical, while not held
        private const float DriftEaseSeconds = 1.5f;   // drift fades back in after a release
        private const float BobEaseSeconds = 0.4f;     // the bob fades out on a grab and back in after
        private const float MinTrackRadius = 0.02f;    // m: a hand this close to the centre turns it by its wrist alone

        private XRSimpleInteractable _grab;
        private IXRSelectInteractor _holder;
        private Quaternion _lastHand;
        private Vector3 _lastHandPos;
        private float _bobWeight = 1f;
        private Vector3 _home, _lift;
        private Quaternion _tilt = Quaternion.identity;
        private float _tiltDone;                       // 0..1: how much of the tilt has been turned in
        private float _born, _released = float.NegativeInfinity;

        /// <summary>Sets the model up once its pieces are children: grab, glow, and the glide from here.</summary>
        /// <param name="grabSettings">Shared grab settings, for the glow.</param>
        /// <param name="lift">World offset (m) from where it was built to where it hovers.</param>
        /// <param name="tilt">World rotation it banks into over the glide.</param>
        public void Init(GrabSettings grabSettings, Vector3 lift, Quaternion tilt)
        {
            _lift = lift;
            _tilt = tilt;
            _home = transform.position + lift;
            _born = Time.time;
            // added after the pieces were parented, so it collects their colliders
            _grab = gameObject.AddComponent<XRSimpleInteractable>();
            _grab.selectEntered.AddListener(OnGrab);
            _grab.selectExited.AddListener(OnRelease);
            if (grabSettings != null)
                gameObject.AddComponent<GrabGlow>().Init(_grab, grabSettings, enabledRenderersOnly: true);
            SessionEvents.Interaction("model_floated", this);
        }

        private void OnDestroy()
        {
            if (_grab == null) return;
            _grab.selectEntered.RemoveListener(OnGrab);
            _grab.selectExited.RemoveListener(OnRelease);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject;
            var attach = _holder.GetAttachTransform(_grab);
            _lastHand = attach.rotation;
            _lastHandPos = attach.position;
            SessionEvents.Interaction("model_grabbed", this);
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactorObject, _holder)) return;
            _holder = null;
            _released = Time.time;
        }

        /// <summary>The part of <paramref name="q"/> that turns about <paramref name="axis"/> (swing-twist split).</summary>
        private static Quaternion TwistAbout(Quaternion q, Vector3 axis)
        {
            Vector3 p = Vector3.Project(new Vector3(q.x, q.y, q.z), axis);
            var twist = new Quaternion(p.x, p.y, p.z, q.w);
            float length = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (length < 1e-6f) return Quaternion.identity;
            return new Quaternion(twist.x / length, twist.y / length, twist.z / length, twist.w / length);
        }

        private void Update()
        {
            if (_grab == null) return;
            float t = Time.time - _born;

            // glide off the pedestal, then bob round the hover spot
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / RiseSeconds));
            _bobWeight = Mathf.MoveTowards(_bobWeight, _holder != null ? 0f : 1f, Time.deltaTime / BobEaseSeconds);
            float bob = t > RiseSeconds ? Mathf.Sin((t - RiseSeconds) * 2f * Mathf.PI / BobPeriod) * BobAmplitude * _bobWeight : 0f;
            transform.position = _home + _lift * (rise - 1f) + Vector3.up * bob;

            // bank in step with the glide, as a turn on top of whatever the hand or the drift has done
            if (_holder == null && _tiltDone < 1f)
            {
                Quaternion step = Quaternion.Slerp(Quaternion.identity, _tilt, rise)
                                  * Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, _tilt, _tiltDone));
                transform.rotation = step * transform.rotation;
                _tiltDone = rise;
            }
            else if (_holder != null) _tiltDone = 1f;

            if (_holder != null)
            {
                var attach = _holder.GetAttachTransform(_grab);
                Vector3 from = _lastHandPos - _home, to = attach.position - _home;
                Quaternion wrist = attach.rotation * Quaternion.Inverse(_lastHand);
                bool track = from.magnitude > MinTrackRadius && to.magnitude > MinTrackRadius;
                Quaternion swing = track ? Quaternion.FromToRotation(from, to) : Quaternion.identity;
                Quaternion twist = track ? TwistAbout(wrist, to.normalized) : wrist;
                transform.rotation = twist * swing * transform.rotation;
                _lastHand = attach.rotation;
                _lastHandPos = attach.position;
                return;
            }

            float drift = DriftSpeed * Mathf.Clamp01((Time.time - _released) / DriftEaseSeconds) * Mathf.Clamp01(t / RiseSeconds);
            transform.Rotate(Vector3.up, drift * Time.deltaTime, Space.World);
        }
    }
}
