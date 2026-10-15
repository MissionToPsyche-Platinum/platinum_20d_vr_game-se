using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PsycheVR.Gameplay.MonitorUi;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A photo filling one monitor panel (cropped to cover it) with a caption strip along the bottom: laid
    /// over the photo, or (the small panels) a strip of its own under it, the photo cropped to the rest.
    /// With more than one photo it cross-fades to the next every few seconds, on unscaled time and only
    /// while the panel is active and playing, so a hidden tab never advances. Photos without a sprite
    /// are skipped; with none left the panel hides itself.
    /// </summary>
    public class PhotoPanel : MonoBehaviour
    {
        private const float CaptionShare = 0.18f;
        private const float CaptionAlpha = 0.95f;   // blended in linear space: 0.95 still reads as a dark strip over white
        private const float CaptionSize = 7f;          // mm, the small right-hand panels
        private const float MainCaptionSize = 10f;     // mm, the big main panel: 7 read as tiny there
        private const float CaptionPad = 3f;           // mm
        private const float FadeSeconds = 0.8f;
        private const float DefaultSecondsEach = 6f;

        [SerializeField] private RectTransform photoArea;
        [SerializeField] private bool captionBelow;
        [SerializeField] private Image front;
        [SerializeField] private Image back;
        [SerializeField] private GameObject captionStrip;
        [SerializeField] private TMP_Text caption;

        private MonitorContent.Photo[] _photos = new MonitorContent.Photo[0];
        private float _secondsEach = DefaultSecondsEach;
        private int _index;
        private float _clock;
        private bool _playing;

        /// <summary>
        /// Creates a photo panel stretched over <paramref name="parent"/>. The caption uses the content's
        /// body font; <paramref name="content"/> may be null (default font). With
        /// <paramref name="captionBelow"/> the caption gets an opaque strip of its own under the photo
        /// instead of lying over its bottom edge, so no sliver of photo can show beneath the text.
        /// </summary>
        public static PhotoPanel Create(RectTransform parent, MonitorContent content, bool captionBelow = false)
        {
            var rt = Child(parent, "PhotoPanel");
            var go = rt.gameObject;
            Stretch(rt);
            var panel = go.AddComponent<PhotoPanel>();
            panel.captionBelow = captionBelow;
            // the photos crop to their own area: the whole panel, or above the caption strip when it sits below
            panel.photoArea = Child(rt, "PhotoArea");
            Stretch(panel.photoArea);
            panel.photoArea.gameObject.AddComponent<RectMask2D>();
            panel.back = NewPhoto(panel.photoArea, "PhotoBack");
            panel.front = NewPhoto(panel.photoArea, "PhotoFront");

            var srt = Child(rt, "Caption");
            var strip = srt.gameObject;
            srt.anchorMin = Vector2.zero; srt.anchorMax = new Vector2(1f, CaptionShare);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var bg = strip.AddComponent<Image>();
            bg.color = captionBelow ? MonitorPalette.DarkPurple : new Color(0f, 0f, 0f, CaptionAlpha); bg.raycastTarget = false;
            panel.captionStrip = strip;
            panel.caption = MonitorScreen.Text(srt, "CaptionText", content != null ? content.bodyFont : null,
                captionBelow ? CaptionSize : MainCaptionSize, TextAlignmentOptions.MidlineLeft, MonitorPalette.White);
            var crt = panel.caption.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.one * CaptionPad; crt.offsetMax = -Vector2.one * CaptionPad;
            return panel;
        }

        /// <summary>Sets the photos to show (null sprites are skipped) and the seconds each stays up.</summary>
        public void Set(MonitorContent.Photo[] photos, float secondsEach)
        {
            _photos = System.Array.FindAll(photos ?? new MonitorContent.Photo[0], p => p != null && p.sprite != null);
            _secondsEach = secondsEach > 0f ? secondsEach : DefaultSecondsEach;
            _index = 0;
            _clock = 0f;
            gameObject.SetActive(_photos.Length > 0);
            if (_photos.Length == 0) return;
            Assign(front, _photos[0]);
            SetAlpha(front, 1f);
            back.gameObject.SetActive(false);
            ShowCaption(_photos[0]);
        }

        /// <summary>Starts or stops the cross-fade cycle. Stopping keeps the current photo.</summary>
        public void Play(bool play)
        {
            _playing = play;
            _clock = 0f;
            if (front == null || back == null || !back.gameObject.activeSelf) return;
            // a fade cut short: finish it so the panel never rests half blended
            SetAlpha(front, 1f);
            back.gameObject.SetActive(false);
        }

        /// <summary>
        /// Maps a point on the photo in front, in normalised image coordinates (0..1, origin bottom-left),
        /// to this panel's local coordinates, allowing for the cover crop (the photo is scaled to fill the
        /// panel and centred, so the overflowing sides are cut). False when no photo shows.
        /// </summary>
        public bool TryImageToLocal(Vector2 normalised, out Vector2 local)
        {
            local = Vector2.zero;
            if (front == null || front.sprite == null) return false;
            var area = photoArea != null ? photoArea : (RectTransform)transform;
            Rect panel = area.rect;
            Rect image = front.sprite.rect;
            if (panel.width <= 0f || panel.height <= 0f || image.height <= 0f) return false;
            float aspect = image.width / image.height;
            Vector2 size = panel.width / panel.height > aspect
                ? new Vector2(panel.width, panel.width / aspect)
                : new Vector2(panel.height * aspect, panel.height);
            Vector2 onArea = panel.center + Vector2.Scale(normalised - Vector2.one * 0.5f, size);
            local = transform.InverseTransformPoint(area.TransformPoint(onArea));   // area space to this panel's space
            return true;
        }

        private void Update()
        {
            if (!_playing || _photos.Length < 2) return;
            _clock += Time.unscaledDeltaTime;
            if (_clock < _secondsEach) return;
            float fade = Mathf.Clamp01((_clock - _secondsEach) / FadeSeconds);
            if (!back.gameObject.activeSelf)
            {
                // fade starts: the next photo goes in front and fades in over the current one
                int next = (_index + 1) % _photos.Length;
                Assign(back, _photos[_index]);
                SetAlpha(back, 1f);
                back.gameObject.SetActive(true);
                Assign(front, _photos[next]);
                _index = next;
                ShowCaption(_photos[_index]);
            }
            SetAlpha(front, fade);
            if (fade < 1f) return;
            back.gameObject.SetActive(false);
            _clock = 0f;
        }

        private void ShowCaption(MonitorContent.Photo photo)
        {
            bool has = !string.IsNullOrEmpty(photo.caption);
            captionStrip.SetActive(has);
            if (captionBelow) photoArea.anchorMin = new Vector2(0f, has ? CaptionShare : 0f);   // no caption: the photo fills the panel
            caption.text = has ? photo.caption : "";
        }

        private static void Assign(Image image, MonitorContent.Photo photo)
        {
            image.sprite = photo.sprite;
            var fitter = image.GetComponent<AspectRatioFitter>();
            var r = photo.sprite.rect;
            fitter.aspectRatio = r.height > 0f ? r.width / r.height : 1f;
        }

        private static Image NewPhoto(RectTransform parent, string name)
        {
            var rt = Child(parent, name);
            var go = rt.gameObject;
            Stretch(rt);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return image;
        }
    }
}
