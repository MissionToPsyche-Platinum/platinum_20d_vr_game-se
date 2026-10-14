using UnityEngine;
using UnityEngine.UI;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A UI line through the first <see cref="Count"/> of <see cref="Points"/> (local rect coordinates),
    /// drawn as one quad per segment in the graphic's colour. Dashed lines skip the gaps; the dash pattern
    /// runs on across corners. Not a raycast target unless switched on. A line redrawn every frame belongs
    /// under its own nested Canvas, so its redraws do not rebuild the rest of the screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIPolyline : MaskableGraphic
    {
        private const float DefaultThickness = 1f;      // mm
        private const float DefaultDashLength = 4f;     // mm
        private const float MinStep = 1e-4f;

        [SerializeField] private Vector2[] points = new Vector2[0];
        [SerializeField] private float thickness = DefaultThickness;
        [SerializeField] private bool dashed;
        [SerializeField] private float dashLength = DefaultDashLength;
        [SerializeField] private int count;
        // set once the line's own defaults (no raycasts) have been applied, so a saved choice survives reloads
        [SerializeField, HideInInspector] private bool defaultsApplied;

        /// <summary>The line's points in this rect's local space. Set through <see cref="SetPoints(Vector2[], int)"/>.</summary>
        public Vector2[] Points => points;

        /// <summary>How many of <see cref="Points"/>, from the first, the line runs through.</summary>
        public int Count => count;

        /// <summary>Line width, in canvas units.</summary>
        public float Thickness { get => thickness; set { thickness = value; SetVerticesDirty(); } }

        /// <summary>Draws dashes of <see cref="DashLength"/> with equal gaps instead of a solid line.</summary>
        public bool Dashed { get => dashed; set { dashed = value; SetVerticesDirty(); } }

        /// <summary>Length of one dash (and one gap), in canvas units.</summary>
        public float DashLength { get => dashLength; set { dashLength = value; SetVerticesDirty(); } }

        /// <summary>Uses all of <paramref name="newPoints"/> as the line; see <see cref="SetPoints(Vector2[], int)"/>.</summary>
        public void SetPoints(Vector2[] newPoints) => SetPoints(newPoints, newPoints?.Length ?? 0);

        /// <summary>
        /// Uses the first <paramref name="newCount"/> of <paramref name="newPoints"/> as the line (the array
        /// is kept, not copied: preallocate it, change it in place and call again with a new count) and
        /// redraws. The count is clamped to the array.
        /// </summary>
        public void SetPoints(Vector2[] newPoints, int newCount)
        {
            points = newPoints ?? new Vector2[0];
            count = Mathf.Clamp(newCount, 0, points.Length);
            SetVerticesDirty();
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();
            if (defaultsApplied) return;
            defaultsApplied = true;
            raycastTarget = false;   // decoration: never blocks pokes or rays
        }

        /// <inheritdoc/>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = points == null ? 0 : Mathf.Min(count, points.Length);
            if (n < 2 || thickness <= 0f) return;
            float along = 0f;   // distance from the first point, for the dash pattern
            for (int i = 1; i < n; i++)
            {
                Vector2 a = points[i - 1], b = points[i];
                float length = Vector2.Distance(a, b);
                if (length <= 0f) continue;
                if (!dashed || dashLength <= 0f) { Quad(vh, a, b); along += length; continue; }
                float period = dashLength * 2f;
                float s = 0f;
                while (s < length)
                {
                    float phase = Mathf.Repeat(along + s, period);
                    // floor on the step: Repeat can return the period itself, which would stall the loop
                    float step = Mathf.Min(Mathf.Max((phase < dashLength ? dashLength : period) - phase, MinStep), length - s);
                    if (phase < dashLength) Quad(vh, Vector2.Lerp(a, b, s / length), Vector2.Lerp(a, b, (s + step) / length));
                    s += step;
                }
                along += length;
            }
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b)
        {
            Vector2 n = new Vector2(a.y - b.y, b.x - a.x).normalized * (thickness * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.up);
            vh.AddVert(b + n, color, Vector2.one);
            vh.AddVert(b - n, color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
