using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// A head-locked line of text that fades in, holds, fades out and destroys itself.
    /// Built in code under the camera at the moment it is needed, so no prefab has to
    /// carry it and it cannot exist in Story mode by accident. Sized like the pause menu
    /// so it reads the same on the headset and on the cast screen.
    ///
    /// The panel is fully head-locked -- pitch and roll as well as yaw -- exactly like the
    /// pause menu, so it stays centred in view however the visitor is looking.
    ///
    /// It also sweeps for clearance before it is placed: the pause menu once materialised
    /// inside a bedroom wall and the depth test hid it completely, so the panel is pulled
    /// in to whatever the camera actually has room for.
    /// </summary>
    public sealed class HandoverMessage : MonoBehaviour
    {
        private const string LogPrefix = "[HandoverMessage]";
        private const string ObjectName = "Handover Message";
        private const float CanvasScale = 0.0011f;
        private const float ReferenceDistance = 1.35f;
        private const float MinDistance = 0.45f;
        private const float ClearancePadding = 0.12f;
        private static readonly Vector2 PanelSize = new Vector2(1200f, 280f);
        private const float Padding = 40f;
        private const float MaxFontSize = 44f;
        private const float MinFontSize = 28f;
        private static readonly Color PanelTint = new Color(0.03f, 0.04f, 0.06f, 0.82f);
        private static readonly Color TextTint = new Color(0.95f, 0.96f, 0.98f, 1f);

        private CanvasGroup _group;
        private TMP_Text _text;

        /// <summary>
        /// Creates the message as a child of <paramref name="cameraTransform"/>, invisible.
        /// </summary>
        /// <param name="cameraTransform">The headset camera; the panel is locked to it.</param>
        /// <param name="distance">Metres in front of the camera, before the clearance sweep. Scale follows the final distance so the apparent size is constant.</param>
        public static HandoverMessage Spawn(Transform cameraTransform, float distance)
        {
            float requestedDistance = distance;
            distance = ClearDistance(cameraTransform, requestedDistance);

            var root = new GameObject(ObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(cameraTransform, false);
            root.layer = cameraTransform.gameObject.layer;
            root.transform.localPosition = new Vector3(0f, 0f, distance);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * (CanvasScale * (distance / ReferenceDistance));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Above the pause menu, which sits at the same distance in front of the camera.
            canvas.sortingOrder = 1;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = PanelSize;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            panel.layer = root.layer;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var image = panel.GetComponent<Image>();
            image.color = PanelTint;
            image.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            label.layer = root.layer;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(Padding, Padding);
            labelRect.offsetMax = new Vector2(-Padding, -Padding);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.color = TextTint;
            text.raycastTarget = false;
            // Long hand-over text shrinks rather than overflowing the panel.
            text.enableAutoSizing = true;
            text.fontSizeMin = MinFontSize;
            text.fontSizeMax = MaxFontSize;

            var message = root.AddComponent<HandoverMessage>();
            message._group = root.GetComponent<CanvasGroup>();
            message._group.alpha = 0f;
            message._group.interactable = false;
            message._group.blocksRaycasts = false;
            message._text = text;
            return message;
        }

        /// <summary>
        /// Metres the panel can actually sit in front of the camera: the requested distance
        /// pulled in to the nearest blocker. Ported from the pause menu so both panels
        /// behave the same. Hits on the rig itself are skipped -- the sweep starts inside
        /// the player's own collider, which would otherwise read as a blocker at zero
        /// distance.
        /// </summary>
        private static float ClearDistance(Transform cameraTransform, float requestedDistance)
        {
            // Half the panel's short edge, so a wall that covers the panel is caught without
            // a full-width sweep snagging on desks and floors the panel would clear anyway.
            float sweepRadius = PanelSize.y * CanvasScale * 0.5f;
            float distance = requestedDistance;

            RaycastHit[] hits = Physics.SphereCastAll(
                cameraTransform.position,
                sweepRadius,
                cameraTransform.forward,
                requestedDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                // The rig must stay at the scene root: nesting it under a container would
                // make .root that container and silently exclude every hit.
                if (hits[i].transform.IsChildOf(cameraTransform.root))
                    continue;

                distance = Mathf.Min(distance, hits[i].distance - ClearancePadding);
            }

            return Mathf.Clamp(distance, MinDistance, requestedDistance);
        }

        /// <summary>Fades the text in, holds it, fades it out, then destroys this object.</summary>
        /// <param name="text">The line to show.</param>
        /// <param name="fadeSeconds">Seconds for each fade, in and out.</param>
        /// <param name="holdSeconds">Seconds the text stays fully visible between the fades.</param>
        public void Show(string text, float fadeSeconds, float holdSeconds)
        {
            // Null unless this instance came from Spawn, which is the only supported way in.
            if (_text == null || _group == null)
            {
                Debug.LogError($"{LogPrefix} Show called on an instance not created by Spawn.", this);
                return;
            }

            _text.text = text;
            Debug.Log($"{LogPrefix} Showing for {holdSeconds:0.#}s.", this);
            StartCoroutine(Run(Mathf.Max(0f, fadeSeconds), Mathf.Max(0f, holdSeconds)));
        }

        private IEnumerator Run(float fadeSeconds, float holdSeconds)
        {
            // Unscaled: the message must finish even if the game is paused underneath it.
            yield return Fade(0f, 1f, fadeSeconds);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f, fadeSeconds);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (seconds <= 0f)
            {
                _group.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
