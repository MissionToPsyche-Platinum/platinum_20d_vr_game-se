using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Teal tint over a grabbable's whole surface while a hand targets it (Job Simulator style),
    /// fading out once it is grabbed.
    /// The strength follows <see cref="GrabTargeting.GlowFor"/> every frame instead of hover
    /// events: full glow for a near target, the dwell progress (0..1) for a far one.
    /// PsycheGrabbable adds this to every grabbable, the book and basketball included, so they
    /// all highlight the same way. Colour, strength and fade come from <see cref="GrabSettings"/>.
    ///
    /// Every mesh gets a hidden child renderer drawing <see cref="GrabSettings.GlowOverlayMaterial"/>
    /// over the whole mesh (one overlay material per submesh), switched on only while the glow
    /// is visible, so an idle prop costs nothing and its own materials are never touched.
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabGlow : MonoBehaviour
    {
        private static readonly int RimColorID = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerID = Shader.PropertyToID("_RimPower");

        private XRBaseInteractable _interactable;
        private GrabSettings _settings;
        private MaterialPropertyBlock _block;
        private readonly List<Renderer> _overlaySources = new List<Renderer>();
        private readonly List<MeshRenderer> _overlayRenderers = new List<MeshRenderer>();
        private bool _overlayOn;
        private float _current;
        private float _target;

        /// <summary>Wires the glow to its grabbable. Called by PsycheGrabbable in Awake.</summary>
        public void Init(XRBaseInteractable interactable, GrabSettings settings)
        {
            _interactable = interactable;
            _settings = settings;
            _block = new MaterialPropertyBlock();
            CollectRenderers();
        }

        private void CollectRenderers()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                // Text, particles and lines are not the prop's body.
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer)
                    continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null)
                    continue;

                var overlay = BuildOverlay(r);
                if (overlay != null)
                {
                    _overlaySources.Add(r);
                    _overlayRenderers.Add(overlay);
                }
            }
        }

        /// <summary>A disabled child that draws the glow material over the whole of r's mesh.</summary>
        private MeshRenderer BuildOverlay(Renderer r)
        {
            var filter = r.GetComponent<MeshFilter>();
            var material = _settings != null ? _settings.GlowOverlayMaterial : null;
            if (!(r is MeshRenderer) || filter == null || filter.sharedMesh == null || material == null)
                return null;

            // One overlay material per submesh covers the whole mesh without reading it (the
            // spacecraft meshes are not Read/Write enabled in builds).
            var mesh = filter.sharedMesh;
            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            var go = new GameObject("[Glow]");
            go.layer = r.gameObject.layer;
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var glow = go.AddComponent<MeshRenderer>();
            glow.sharedMaterials = materials;
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glow.enabled = false;
            return glow;
        }

        private void OnDisable()
        {
            _current = _target = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_interactable != null)
                // Held objects glow too: the free hand can take them over. The holding hand's
                // GrabTargeting has no near target while it holds, so it never lights its own.
                _target = GrabTargeting.GlowFor(_interactable);

            // A source can switch off while glowing (the paper sheet turning into a ball).
            if (_overlayOn)
            {
                for (int i = 0; i < _overlayRenderers.Count; i++)
                    _overlayRenderers[i].enabled = _overlaySources[i] != null && _overlaySources[i].enabled && _overlaySources[i].gameObject.activeInHierarchy;
            }

            if (_settings == null || Mathf.Approximately(_current, _target))
                return;

            float fade = _settings.GlowFadeDuration;
            _current = fade > 0f ? Mathf.MoveTowards(_current, _target, Time.deltaTime / fade) : _target;
            Apply(_current);
        }

        private void Apply(float t)
        {
            if (_settings == null)
                return;

            Color c = _settings.GlowColor;
            c.a *= t;

            SetOverlay(t > 0f);
            if (_overlayOn)
            {
                foreach (var r in _overlayRenderers)
                    SetBlock(r, c);
            }
        }

        private void SetBlock(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetColor(RimColorID, c);
            _block.SetFloat(RimPowerID, _settings.GlowRimPower);
            r.SetPropertyBlock(_block);
        }

        private void SetOverlay(bool on)
        {
            if (on == _overlayOn)
                return;

            _overlayOn = on;
            for (int i = 0; i < _overlayRenderers.Count; i++)
            {
                var src = _overlaySources[i];
                _overlayRenderers[i].enabled = on && src != null && src.enabled && src.gameObject.activeInHierarchy;
            }
        }
    }
}
