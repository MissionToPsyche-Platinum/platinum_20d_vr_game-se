using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Cartoon feedback for a dome <see cref="SlapKey"/> (TG-294, the puzzle board's buttons): on each
    /// press the dome glows for a moment, and can puff wider and lower with a wobble (<see cref="squash"/>,
    /// off on the board: any squash on top of SlapKey's sink read as the button shrinking). The sink, the
    /// click sound and the haptic pulse stay SlapKey's. The colour and glow tint the dome and everything
    /// under it (its collar) through a MaterialPropertyBlock, so the shared material is never touched.
    /// </summary>
    [RequireComponent(typeof(SlapKey))]
    public class PuffyButton : MonoBehaviour
    {
        private const string BaseColor = "_BaseColor";
        private const float WobbleDecay = 5f;              // how fast the spring-back wobble dies out
        private const float WobbleHalfTurns = 3f;          // half-oscillations over the spring-back

        [Tooltip("The dome's renderer. Defaults to this object's.")]
        [SerializeField] private Renderer dome;

        [Tooltip("Resting colour of the dome; alpha 0 keeps the material's own colour.")]
        [SerializeField] private Color colour = new Color(0f, 0f, 0f, 0f);

        [Tooltip("Colour the dome flashes to on a press.")]
        [SerializeField] private Color glow = new Color(1f, 0.88f, 0.55f, 1f);

        [Tooltip("Share of its height the dome loses at full squash.")]
        [SerializeField, Range(0f, 0.9f)] private float squash = 0.15f;

        [Tooltip("Share of its width the dome gains at full squash.")]
        [SerializeField, Range(0f, 0.5f)] private float spread = 0.05f;

        [Tooltip("Seconds to squash flat.")]
        [SerializeField] private float squashSeconds = 0.05f;

        [Tooltip("Seconds to spring back to shape.")]
        [SerializeField] private float springSeconds = 0.3f;

        [Tooltip("Seconds the glow takes to fade.")]
        [SerializeField] private float glowSeconds = 0.4f;

        private SlapKey _key;
        private Vector3 _restScale;
        private Color _restColour;
        private MaterialPropertyBlock _block;
        private Renderer[] _tinted;
        private float _pressedAt = float.NegativeInfinity;

        private void Awake()
        {
            _key = GetComponent<SlapKey>();
            if (dome == null) dome = GetComponent<Renderer>();
            _tinted = GetComponentsInChildren<Renderer>(true);
            _restScale = transform.localScale;
            _block = new MaterialPropertyBlock();
            _restColour = colour.a > 0f || dome == null || dome.sharedMaterial == null
                ? colour
                : dome.sharedMaterial.GetColor(BaseColor);
            Tint(_restColour);
        }

        private void OnEnable() => _key.OnPressed.AddListener(Play);

        private void OnDisable()
        {
            _key.OnPressed.RemoveListener(Play);
            transform.localScale = _restScale;
            Tint(_restColour);
            _pressedAt = float.NegativeInfinity;
        }

        /// <summary>Plays the squash and the glow from the start.</summary>
        public void Play() => _pressedAt = Time.time;

        private void Update()
        {
            float t = Time.time - _pressedAt;
            if (t > Mathf.Max(squashSeconds + springSeconds, glowSeconds))
            {
                if (t < float.PositiveInfinity && transform.localScale != _restScale)
                {
                    transform.localScale = _restScale;
                    Tint(_restColour);
                }
                return;
            }

            float amount;
            if (t < squashSeconds)
                amount = t / squashSeconds;
            else
            {
                float k = Mathf.Clamp01((t - squashSeconds) / springSeconds);
                amount = Mathf.Exp(-WobbleDecay * k) * Mathf.Cos(k * WobbleHalfTurns * Mathf.PI) * (1f - k);
            }
            transform.localScale = Vector3.Scale(_restScale,
                new Vector3(1f + spread * amount, 1f - squash * amount, 1f + spread * amount));
            Tint(Color.Lerp(glow, _restColour, Mathf.Clamp01(t / glowSeconds)));
        }

        private void Tint(Color c)
        {
            foreach (var r in _tinted)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, c);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
