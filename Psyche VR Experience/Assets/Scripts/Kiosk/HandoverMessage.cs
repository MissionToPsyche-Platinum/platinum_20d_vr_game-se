using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PsycheVR.Gameplay;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// The end-of-session card: "Thanks for playing" with the game's title in quotes and a Mustard call to
    /// action to hand the headset back, in the controller guide's palette and fonts
    /// (<see cref="KioskMessageStyle"/>). It fades in, holds, fades out and destroys itself. Built in code at
    /// the moment it is needed, so no prefab has to carry it and it cannot exist in Story mode by accident.
    ///
    /// The card is opaque on purpose: the message follows the launch video, so the visitor is usually
    /// looking straight at the bright wall screen, where bare white text vanishes.
    ///
    /// It floats rather than being locked to the camera (TG-322): <see cref="HeadFollow"/> holds it at
    /// eye height ahead, lets small head movements pass, glides it back after a large turn or step, keeps
    /// it upright and keeps it out of walls. Camera-locked, it jerked with every head movement and could
    /// sink into a wall the visitor stood near.
    /// </summary>
    public sealed class HandoverMessage : MonoBehaviour
    {
        private const string LogPrefix = "[HandoverMessage]";
        private const string ObjectName = "Handover Message";
        private const float MillimetresPerMetre = 1000f;
        /// <summary>The card is drawn in millimetres at this distance and scaled with it, so it keeps its size in view.</summary>
        private const float ReferenceDistance = 1.35f;
        private const float Width = 960f, Height = 380f;          // mm
        private const float Margin = 60f;                          // mm, left and right of the text
        private const float KickerSize = 18f, TitleSize = 66f, CtaSize = 26f;
        private const string Kicker = "END OF YOUR SESSION";

        private CanvasGroup _group;
        private TMP_Text _title;
        private TMP_Text _action;
        private Material _materialInstance;

        /// <summary>
        /// Creates the message floating in front of <paramref name="cameraTransform"/>, invisible.
        /// </summary>
        /// <param name="cameraTransform">The headset camera; the card follows it lazily.</param>
        /// <param name="distance">Metres in front of the camera when nothing is in the way. Scale follows the distance so the apparent size is constant.</param>
        public static HandoverMessage Spawn(Transform cameraTransform, float distance)
        {
            var style = KioskMessageStyle.Load();
            TMP_FontAsset titleFont = style != null ? style.TitleFont : null;
            TMP_FontAsset bodyFont = style != null ? style.BodyFont : null;

            var root = new GameObject(ObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.layer = cameraTransform.gameObject.layer;
            root.transform.localScale = Vector3.one * (distance / ReferenceDistance / MillimetresPerMetre);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Above the pause menu, which sits at the same distance in front of the camera.
            canvas.sortingOrder = 1;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(Width, Height);

            Fill(rect, "Background", MonitorPalette.Black);
            var band = Fill(rect, "Band", MonitorPalette.DarkPurple);
            Place(band.rectTransform, 0f, Height - 12f, Width, 12f);

            const float left = Margin;
            const float column = Width - 2f * Margin;

            var kicker = Text(rect, "Kicker", bodyFont, KickerSize, TextAlignmentOptions.BottomLeft, MonitorPalette.Mustard, Kicker);
            kicker.characterSpacing = 6f;
            Place(kicker.rectTransform, left, 290f, column, 30f);

            var title = Text(rect, "Title", titleFont, TitleSize, TextAlignmentOptions.BottomLeft, MonitorPalette.White, "");
            title.enableAutoSizing = true; title.fontSizeMin = TitleSize * 0.6f; title.fontSizeMax = TitleSize;
            Place(title.rectTransform, left, 175f, column, 112f);

            var rule = Fill(rect, "Rule", MonitorPalette.Mustard);
            Place(rule.rectTransform, left, 160f, column, 3f);

            var pill = Fill(rect, "Action", MonitorPalette.Mustard);
            Place(pill.rectTransform, left, 58f, column, 72f);
            var action = Text(pill.rectTransform, "Text", titleFont, CtaSize, TextAlignmentOptions.Center, MonitorPalette.Black, "");
            action.enableAutoSizing = true; action.fontSizeMin = CtaSize * 0.6f; action.fontSizeMax = CtaSize;
            MonitorUi.Stretch(action.rectTransform);
            action.rectTransform.offsetMin = new Vector2(16f, 0f); action.rectTransform.offsetMax = new Vector2(-16f, 0f);

            // No project font found: the default font's shared material is black-faced (see ApplyPlainWhite).
            Material materialInstance = titleFont == null ? ApplyPlainWhite(title) : null;

            // Half the card's short edge: a wall that would cover the card is caught without a
            // full-width sweep snagging on desks and floors the card clears anyway.
            root.AddComponent<HeadFollow>().Init(cameraTransform, distance, Height * 0.5f / MillimetresPerMetre * (distance / ReferenceDistance));

            var message = root.AddComponent<HandoverMessage>();
            message._group = root.GetComponent<CanvasGroup>();
            message._group.alpha = 0f;
            message._group.interactable = false;
            message._group.blocksRaycasts = false;
            message._title = title;
            message._action = action;
            message._materialInstance = materialInstance;
            return message;
        }

        /// <summary>Bottom-left anchored rectangle in card millimetres.</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image Fill(RectTransform parent, string name, Color c)
        {
            var rt = MonitorUi.Child(parent, name);
            MonitorUi.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        private static TMP_Text Text(RectTransform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Color colour, string text)
        {
            var t = MonitorUi.Child(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = colour; t.raycastTarget = false; t.text = text;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>
        /// Gives the label its own material so the look does not depend on the project's
        /// default font material. That shared material was changed to a black face with a
        /// white underlay and a dark outline for the mission control test room, which is
        /// why an unstyled label here came out as black text on a white smear. Plain white
        /// face, no outline, no underlay: the panel supplies the contrast. The instance is
        /// returned so the owner can destroy it; TMP does not.
        /// </summary>
        private static Material ApplyPlainWhite(TMP_Text text)
        {
            Material material = text.fontMaterial;
            material.SetColor(ShaderUtilities.ID_FaceColor, MonitorPalette.White);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            material.DisableKeyword(ShaderUtilities.Keyword_Outline);
            material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            material.DisableKeyword(ShaderUtilities.Keyword_Glow);
            // The shared material's outline and underlay had padded every glyph quad;
            // resize the quads for the plain material so nothing is drawn outside the letters.
            text.UpdateMeshPadding();
            return material;
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
        }

        /// <summary>Fades the card in, holds it, fades it out, then destroys this object.</summary>
        /// <param name="text">The first line is the title (Thanks for playing “T-Minus Psyche”); the rest is the call to action, shown in capitals.</param>
        /// <param name="fadeSeconds">Seconds for each fade, in and out.</param>
        /// <param name="holdSeconds">Seconds the text stays fully visible between the fades.</param>
        public void Show(string text, float fadeSeconds, float holdSeconds)
        {
            // Null unless this instance came from Spawn, which is the only supported way in.
            if (_title == null || _action == null || _group == null)
            {
                Debug.LogError($"{LogPrefix} Show called on an instance not created by Spawn.", this);
                return;
            }

            string[] lines = (text ?? "").Split(new[] { '\n' }, 2);
            _title.text = lines[0].Trim().TrimEnd('.');
            _action.text = lines.Length > 1 ? lines[1].Replace('\n', ' ').Trim().TrimEnd('.').ToUpperInvariant() : "";
            _action.transform.parent.gameObject.SetActive(_action.text.Length > 0);
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
