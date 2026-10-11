using UnityEngine;

namespace PsycheVR.Audio
{
    /// <summary>
    /// Plays an <see cref="InteractionSound"/> when this rigidbody hits something, louder the harder
    /// the hit. Only the speed into the surface counts, so rolling and sliding stay silent, and one
    /// bounce plays once: the physics engine can report a single bounce as several contacts in a
    /// row, so hits inside <see cref="minInterval"/> of the last sound are dropped.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ImpactSound : MonoBehaviour
    {
        [SerializeField] private InteractionSound sound = InteractionSound.BallBounce;

        [Tooltip("Hits slower than this into the surface (m/s) make no sound.")]
        [SerializeField] private float minSpeed = 0.6f;

        [Tooltip("Hits at or above this speed into the surface (m/s) play at the library volume.")]
        [SerializeField] private float fullSpeed = 5f;

        [Tooltip("Shortest time (s) between two sounds, so one bounce never plays as a burst.")]
        [SerializeField] private float minInterval = 0.25f;

        private float _nextAllowed;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < _nextAllowed || collision.contactCount == 0)
                return;

            var contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < minSpeed)
                return;

            _nextAllowed = Time.time + minInterval;
            InteractionAudio.Play(sound, contact.point, Mathf.InverseLerp(minSpeed, fullSpeed, speed) * 0.65f + 0.35f);
        }
    }
}
