using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A photo filling one monitor panel (cropped to cover it) with a caption strip along the bottom.
    /// With more than one photo it cross-fades to the next every few seconds, on unscaled time and only
    /// while the panel is active and playing, so a hidden tab never advances. Photos without a sprite
    /// are skipped; with none left the panel hides itself.
    /// </summary>
    public class PhotoPanel : MonoBehaviour
    {
        private const float CaptionShare = 0.18f;
        private const float CaptionAlpha = 0.95f;   // blended in linear space: 0.95 still reads as a dark strip over white
        private const float CaptionSize = 7f;          // mm
        private const float CaptionPad = 3f;           // mm
        private const float FadeSeconds = 0.8f;
        private const float DefaultSecondsEach = 6f;

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
        /// body font; <paramref name="content"/> may be null (default font).
        /// </summary>
        public static PhotoPanel Create(RectTransform parent, MonitorContent content)
        {
            var go = new GameObject("PhotoPanel", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            go.layer = parent.gameObject.layer;
            var panel = go.AddComponent<PhotoPanel>();
            panel.back = NewPhoto(rt, "PhotoBack");
            panel.front = NewPhoto(rt, "PhotoFront");

            var strip = new GameObject("Caption", typeof(RectTransform));
            strip.layer = go.layer;
            var srt = (RectTransform)strip.transform;
            srt.SetParent(rt, false);
            srt.anchorMin = Vector2.zero; srt.anchorMax = new Vector2(1f, CaptionShare);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var bg = strip.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, CaptionAlpha); bg.raycastTarget = false;
            panel.captionStrip = strip;
            panel.caption = MonitorScreen.Text(srt, "CaptionText", content != null ? content.bodyFont : null,
                CaptionSize, TextAlignmentOptions.MidlineLeft, MonitorPalette.White);
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
            caption.text = has ? photo.caption : "";
        }

        private static void Assign(Image image, MonitorContent.Photo photo)
        {
            image.sprite = photo.sprite;
            var fitter = image.GetComponent<AspectRatioFitter>();
            var r = photo.sprite.rect;
            fitter.aspectRatio = r.height > 0f ? r.width / r.height : 1f;
        }

        private static void SetAlpha(Image image, float a)
        {
            var c = image.color; c.a = a; image.color = c;
        }

        private static Image NewPhoto(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return image;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
