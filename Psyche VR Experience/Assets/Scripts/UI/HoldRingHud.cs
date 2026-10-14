using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PsycheVR.UI
{
    /// <summary>
    /// The staff hold ring: a radial ring in the middle of the view, locked to the headset, that fills while
    /// a staff combo (<see cref="ControllerHoldCombo"/>) is held past its indicator delay. One per camera,
    /// shared by every combo: each owner reports its own state through <see cref="Report"/> and the ring
    /// shows the furthest one, so two combos never fight over it. It is the ring the pause menu's admin
    /// reveal used in the card's corner, a little larger and centred. Drawn after everything else with no
    /// depth test, so a desk or wall in front of the player never hides it.
    /// </summary>
    public sealed class HoldRingHud : MonoBehaviour
    {
        private const string ObjectName = "Hold Ring HUD";
        private const float Distance = 0.6f;          // m in front of the eyes: close, so nothing in the room sits between
        private const float Diameter = 0.03f;         // m: about 2.9 degrees across, up from the menu ring's 1.6
        private const float CanvasUnits = 100f;       // canvas units across the ring
        private const float TrackAlpha = 0.2f;        // the unfilled part of the ring, so the remaining hold reads
        private const int OverlayQueue = 4000;        // Overlay: after the scene's transparent pass
        private const int RingTextureSize = 64;
        private const float RingInnerRadiusFraction = 0.36f;
        private static readonly Color RingColour = new Color(0.85f, 0.9f, 0.95f, 0.9f);

        private static Sprite _ringSprite;
        private static Material _overlayMaterial;

        private readonly Dictionary<object, float> _requests = new Dictionary<object, float>();
        private GameObject _canvas;
        private Image _fill;

        /// <summary>The ring on <paramref name="cameraTransform"/>, created on first use. Null without a camera.</summary>
        public static HoldRingHud For(Transform cameraTransform)
        {
            if (cameraTransform == null)
                return null;

            var existing = cameraTransform.Find(ObjectName);
            if (existing != null && existing.TryGetComponent(out HoldRingHud found))
                return found;

            var go = new GameObject(ObjectName);
            go.transform.SetParent(cameraTransform, false);
            var hud = go.AddComponent<HoldRingHud>();
            hud.Build(cameraTransform.GetComponent<Camera>());
            return hud;
        }

        /// <summary>
        /// Reports <paramref name="owner"/>'s combo: shown at <paramref name="progress"/> (0..1) while
        /// <paramref name="visible"/>, withdrawn otherwise. Owners withdraw when they are disabled.
        /// </summary>
        public void Report(object owner, bool visible, float progress)
        {
            if (owner == null)
                return;

            if (visible)
                _requests[owner] = Mathf.Clamp01(progress);
            else if (!_requests.Remove(owner))
                return;

            bool show = _requests.Count > 0;
            if (_canvas.activeSelf != show)
                _canvas.SetActive(show);
            if (show)
                _fill.fillAmount = _requests.Values.Max();
        }

        /// <summary>A white ring on a transparent square, generated once, so no sprite asset is needed.</summary>
        public static Sprite RingSprite()
        {
            if (_ringSprite != null)
                return _ringSprite;

            var texture = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Hold Ring",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float center = (RingTextureSize - 1) * 0.5f;
            float outer = RingTextureSize * 0.5f - 1f;
            float inner = RingTextureSize * RingInnerRadiusFraction;
            var pixels = new Color32[RingTextureSize * RingTextureSize];

            for (int y = 0; y < RingTextureSize; y++)
            {
                for (int x = 0; x < RingTextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    // One-pixel soft edge on both sides of the band.
                    float coverage = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
                    pixels[y * RingTextureSize + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, RingTextureSize, RingTextureSize), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.name = "Hold Ring";
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
        }

        private void Build(Camera eye)
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.layer = gameObject.layer;
            var rect = (RectTransform)_canvas.transform;
            rect.SetParent(transform, false);
            rect.localPosition = new Vector3(0f, 0f, Distance);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (Diameter / CanvasUnits);
            rect.sizeDelta = Vector2.one * CanvasUnits;

            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eye;

            Ring("Track", new Color(RingColour.r, RingColour.g, RingColour.b, TrackAlpha), false);
            _fill = Ring("Fill", RingColour, true);
            _canvas.SetActive(false);
        }

        private Image Ring(string name, Color colour, bool filled)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = _canvas.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.sprite = RingSprite();
            image.material = OverlayMaterial();
            image.color = colour;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
                image.fillAmount = 0f;
            }
            return image;
        }

        /// <summary>The default UI shader with its depth test off and drawn in the overlay queue.</summary>
        private static Material OverlayMaterial()
        {
            if (_overlayMaterial != null)
                return _overlayMaterial;

            _overlayMaterial = new Material(Shader.Find("UI/Default"))
            {
                name = "Hold Ring Overlay",
                renderQueue = OverlayQueue,
                hideFlags = HideFlags.HideAndDontSave
            };
            _overlayMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return _overlayMaterial;
        }
    }
}
