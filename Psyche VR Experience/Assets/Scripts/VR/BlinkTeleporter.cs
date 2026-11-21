using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PsycheVR.Data;

namespace PsycheVR.VR
{
    [DisallowMultipleComponent]
    public class BlinkTeleporter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Fade overlay parented to the HMD camera.")]
        [SerializeField] private BlinkFadeOverlay fadeOverlay;

        [Tooltip("The XR Rig root. Auto-filled to this transform if empty.")]
        [SerializeField] private Transform rigRoot;

        [Tooltip("Optional CharacterController on the rig. Disabled across the frame of the move to prevent fighting with SetPositionAndRotation.")]
        [SerializeField] private CharacterController rigCharacterController;

        [Tooltip("Left-hand interactor. Any active selection is force-released at the start of a teleport.")]
        [SerializeField] private NearFarInteractor leftHandInteractor;

        [Tooltip("Right-hand interactor. Any active selection is force-released at the start of a teleport.")]
        [SerializeField] private NearFarInteractor rightHandInteractor;

        [Header("Timing")]
        [Tooltip("Fade-out duration in seconds (unscaled time).")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Tooltip("Hold-at-black duration in seconds (unscaled time).")]
        [SerializeField] private float holdDuration = 0.05f;

        [Tooltip("Fade-in duration in seconds (unscaled time).")]
        [SerializeField] private float fadeInDuration = 0.35f;

        private Coroutine _activeRoutine;
        private readonly List<IXRSelectInteractable> _releaseBuffer = new List<IXRSelectInteractable>();

        /// <summary>
        /// World position of the rig root. Lets callers decide where to send the
        /// player without reaching into this component's serialized fields.
        /// </summary>
        public Vector3 RigPosition => rigRoot != null ? rigRoot.position : transform.position;

        private void Reset()
        {
            rigRoot = transform;
            rigCharacterController = GetComponent<CharacterController>();
        }

        public void TeleportTo(BlinkTeleportAnchor anchor)
        {
            TeleportTo(anchor, fadeOutDuration, holdDuration, fadeInDuration);
        }

        /// <summary>
        /// Teleports with per-trip durations in unscaled seconds without changing
        /// the normal blink settings. Non-positive durations skip that stage.
        /// </summary>
        public void TeleportTo(BlinkTeleportAnchor anchor, float fadeOutDuration,
            float holdDuration, float fadeInDuration)
        {
            TeleportTo(anchor, fadeOutDuration, holdDuration, fadeInDuration, null);
        }

        /// <summary>Runs a trip and invokes onArrived after the fade-in completes.
        /// A cancelled or superseded trip never invokes its callback.</summary>
        public Coroutine TeleportTo(BlinkTeleportAnchor anchor, float fadeOutDuration,
            float holdDuration, float fadeInDuration, Action onArrived)
        {
            if (anchor == null)
            {
                Debug.LogWarning("BlinkTeleporter: TeleportTo called with null anchor; ignoring.", this);
                return null;
            }

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            SessionEvents.Interaction("teleported", anchor);
            _activeRoutine = StartCoroutine(TeleportRoutine(anchor,
                fadeOutDuration, holdDuration, fadeInDuration, onArrived));
            return _activeRoutine;
        }

        /// <summary>Cancels only the specified active trip and clears its fade.</summary>
        public void CancelTeleport(Coroutine trip)
        {
            if (trip == null || trip != _activeRoutine) return;
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
            if (fadeOverlay != null) fadeOverlay.SetAlpha(0f);
        }

        private void OnDisable() => CancelTeleport(_activeRoutine);

        private IEnumerator TeleportRoutine(BlinkTeleportAnchor anchor,
            float fadeOutDuration, float holdDuration, float fadeInDuration, Action onArrived)
        {
            ForceReleaseBothHands();

            float startAlpha = fadeOverlay != null ? fadeOverlay.CurrentAlpha : 0f;
            yield return FadeTo(1f, fadeOutDuration, startAlpha);

            MoveRig(anchor);

            yield return WaitUnscaled(holdDuration);

            yield return FadeTo(0f, fadeInDuration, 1f);

            _activeRoutine = null;
            onArrived?.Invoke();
        }

        private void ForceReleaseBothHands()
        {
            ForceRelease(leftHandInteractor);
            ForceRelease(rightHandInteractor);
        }

        private void ForceRelease(NearFarInteractor interactor)
        {
            if (interactor == null || !interactor.hasSelection)
                return;

            var manager = interactor.interactionManager;
            if (manager == null)
                return;

            // Snapshot because XRInteractionManager.SelectExit mutates interactablesSelected.
            _releaseBuffer.Clear();
            _releaseBuffer.AddRange(interactor.interactablesSelected);

            for (int i = 0; i < _releaseBuffer.Count; i++)
            {
                manager.SelectExit((IXRSelectInteractor)interactor, _releaseBuffer[i]);
            }

            _releaseBuffer.Clear();
        }

        private void MoveRig(BlinkTeleportAnchor anchor)
        {
            if (rigRoot == null)
                rigRoot = transform;

            Quaternion rotation = Quaternion.Euler(0f, anchor.Yaw, 0f);

            bool hadCharacterController = rigCharacterController != null && rigCharacterController.enabled;
            if (hadCharacterController)
                rigCharacterController.enabled = false;

            rigRoot.SetPositionAndRotation(anchor.Position, rotation);
            Physics.SyncTransforms();

            if (hadCharacterController)
                rigCharacterController.enabled = true;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration, float startAlpha)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("BlinkTeleporter: fadeOverlay not assigned; skipping fade.", this);
                yield break;
            }

            if (duration <= 0f)
            {
                fadeOverlay.SetAlpha(targetAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeOverlay.SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            fadeOverlay.SetAlpha(targetAlpha);
        }

        private IEnumerator WaitUnscaled(float duration)
        {
            if (duration <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
