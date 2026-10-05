using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Near-first selection with a dwell-gated far select, for first-time VR visitors who reach
    /// out and grab rather than point. It applies to every selectable interactable: grabbables and
    /// world buttons alike. Anything with a collider within <see cref="GrabSettings.NearGrabRadius"/>
    /// of the grab centre (<see cref="GrabSettings.NearGrabForwardOffset"/> ahead of the palm) is a near target: it glows at once and can be selected. The far ray can only
    /// select after resting on the same interactable for <see cref="GrabSettings.FarDwellSeconds"/>;
    /// meanwhile the target's glow and a line from the hand to the target fade in together, so
    /// sweeping the hands around the room never pulls or presses anything. Book pages are near
    /// only. An item held by the other hand is a near target only (grabbing it hands it over,
    /// XRI Single select mode) and ranks after every free near target, so the free hand still
    /// turns a held book's pages. While the ray is on a UI canvas
    /// the same line is drawn at full alpha to the UI hit, since the XRI line visual is off.
    /// Implements <see cref="IXRSelectFilter"/> on this hand's Near-Far interactor to refuse
    /// early far selects. With <see cref="GrabSettings.FarGrabEnabled"/> off, only near selects
    /// pass and no far dwell starts; the UI line still draws. A drawer handle loses to any other
    /// near target and is refused while the palm is inside its drawer, so reaching in for a prop
    /// takes the prop, or above the top of its drawer front, so a hand on the desk top never
    /// takes it.
    /// Also this interactor's <see cref="IXRTargetFilter"/>: XRI's Near-Far interactor hands on
    /// only its first valid target, sorted by its own distance measure, so a sheet in a drawer
    /// lost to the drawer handle every time and was never found (2026-09-30). The filter sorts
    /// near targets by distance from the grab centre (the measure the glow uses) and drops
    /// blocked handles before XRI picks. One per controller, next to HandPresence.
    /// </summary>
    public class GrabTargeting : MonoBehaviour, IXRSelectFilter, IXRTargetFilter
    {
        [Tooltip("Shared grab settings asset (radius, dwell time, line look).")]
        [SerializeField] private GrabSettings grabSettings;

        [Tooltip("This hand's interactor. Auto-found in children if empty.")]
        [SerializeField] private NearFarInteractor interactor;

        [Tooltip("Palm centre of this hand's model (PalmAnchor). The near zone is centred here.")]
        [SerializeField] private Transform palmAnchor;

        [Tooltip("Material for the dwell and UI line. Must use vertex colour alpha (Default-Line works).")]
        [SerializeField] private Material lineMaterial;

        /// <summary>A far target may drop out of the ray this long (seconds) without losing its dwell.</summary>
        private const float TargetLossGrace = 0.1f;

        /// <summary>A palm this far (m) behind a drawer's front face is inside the drawer and cannot take its handle.</summary>
        private const float HandleBehindFaceMargin = 0.03f;

        /// <summary>
        /// A palm this far (m) above a drawer's front face is over the desk top and cannot take its
        /// handle, so slapping the keyboard never lights up the drawer under it.
        /// </summary>
        private const float HandleAboveFaceMargin = 0.02f;

        /// <summary>Sort penalty for a near target the other hand holds, so free targets always rank first.</summary>
        private const float HeldByOtherPenalty = 1000f;


        /// <summary>Floor (seconds) on the dwell time so a zero setting cannot divide by zero.</summary>
        private const float MinDwellSeconds = 0.01f;

        private static readonly List<GrabTargeting> s_Active = new List<GrabTargeting>();

        private readonly List<IXRInteractable> _validTargets = new List<IXRInteractable>();
        private readonly List<IXRInteractable> _nearSorted = new List<IXRInteractable>();
        private readonly Dictionary<IXRInteractable, float> _nearDistance = new Dictionary<IXRInteractable, float>();
        private System.Comparison<IXRInteractable> _byNearDistance;
        private IXRSelectInteractable _nearTarget;
        private IXRSelectInteractable _farTarget;
        private float _dwell;
        private float _lostAt = -1f;
        private LineRenderer _line;
        /// <summary>Centre of the near grab zone: a child of the palm, pushed toward the fingertips.</summary>
        private Transform _grabCentre;

        /// <summary>Every enabled GrabTargeting (one per hand).</summary>
        public static IReadOnlyList<GrabTargeting> Active => s_Active;

        /// <summary>Centre of this hand's near grab zone.</summary>
        public Transform GrabCentre => _grabCentre;

        /// <summary>This hand's Near-Far interactor (haptics, selection).</summary>
        public NearFarInteractor Interactor => interactor;

        /// <summary>True while this hand holds something.</summary>
        public bool IsHolding => interactor != null && interactor.hasSelection;

        /// <summary>0 to 1: how far the current far target's dwell has run.</summary>
        public float DwellProgress => _farTarget == null || grabSettings == null
            ? 0f
            : Mathf.Clamp01(_dwell / Mathf.Max(grabSettings.FarDwellSeconds, MinDwellSeconds));

        /// <inheritdoc />
        public bool canProcess => isActiveAndEnabled;

        /// <summary>
        /// Glow amount (0 to 1) for an interactable across both hands: 1 while it is a near
        /// target, the dwell progress while it is a far target, else 0.
        /// </summary>
        public static float GlowFor(IXRInteractable interactable)
        {
            float glow = 0f;
            foreach (var t in s_Active)
                glow = Mathf.Max(glow, t.GlowForTarget(interactable));
            return glow;
        }

        private float GlowForTarget(IXRInteractable interactable)
        {
            if (interactable == null) return 0f;
            if (ReferenceEquals(interactable, _nearTarget)) return 1f;
            if (ReferenceEquals(interactable, _farTarget)) return DwellProgress;
            return 0f;
        }

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInChildren<NearFarInteractor>(true);
            if (grabSettings == null || interactor == null || palmAnchor == null)
            {
                Debug.LogError($"[GrabTargeting] Needs GrabSettings, a NearFarInteractor and the PalmAnchor on {name}.", this);
                enabled = false;
                return;
            }

            // The near zone is a sphere just ahead of the palm (the palm's +Z runs along the
            // fingers), not around the controller origin, so it reaches further where the hand points.
            _grabCentre = new GameObject("[NearGrabCentre]").transform;
            _grabCentre.SetParent(palmAnchor, false);
            _grabCentre.localPosition = new Vector3(0f, 0f, grabSettings.NearGrabForwardOffset);

            var sphere = interactor.nearInteractionCaster as SphereInteractionCaster;
            if (sphere != null)
            {
                sphere.castOrigin = _grabCentre;
                sphere.castRadius = grabSettings.NearGrabRadius;
            }
            else
            {
                Debug.LogWarning($"[GrabTargeting] Near caster on {name} is not a SphereInteractionCaster: near radius and origin were not applied.", this);
            }

            if (lineMaterial == null)
                Debug.LogWarning($"[GrabTargeting] No line material on {name}: the dwell line will render magenta.", this);

            BuildLine();
        }

        private void BuildLine()
        {
            var go = new GameObject("[DwellLine]");
            go.transform.SetParent(interactor.transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.widthMultiplier = grabSettings.DwellLineWidth;
            _line.sharedMaterial = lineMaterial;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
        }

        private void OnEnable()
        {
            if (interactor == null) return;
            if (!s_Active.Contains(this)) s_Active.Add(this);
            interactor.selectFilters.Add(this);
            if (interactor.targetFilter == null)
                interactor.targetFilter = this;
            else if (!ReferenceEquals(interactor.targetFilter, this))
                Debug.LogWarning($"[GrabTargeting] {interactor.name} already has a target filter: drawer handles may hide props.", this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
            if (interactor == null) return;
            interactor.selectFilters.Remove(this);
            if (ReferenceEquals(interactor.targetFilter, this))
                interactor.targetFilter = null;
            Clear();
        }

        private void LateUpdate()
        {

            if (interactor.hasSelection)
            {
                Clear();
                return;
            }

            var candidate = CurrentCandidate();

            if (candidate != null && IsNear(candidate))
            {
                // Near always wins; a far dwell in progress is dropped.
                _nearTarget = candidate;
                ResetFar();
                UpdateLine();
                return;
            }

            _nearTarget = null;

            if (_farTarget != null && FarTargetStillValid())
            {
                // The 6 degree far cone can hold several items whose order jitters; keep
                // dwelling on the current one as long as it is anywhere in the valid list.
                _dwell += Time.deltaTime;
                _lostAt = -1f;
            }
            else if (_farTarget != null)
            {
                if (_lostAt < 0f)
                    _lostAt = Time.unscaledTime;
                else if (Time.unscaledTime - _lostAt > TargetLossGrace)
                    ResetFar();
            }

            if (_farTarget == null && candidate != null && !(candidate is BookPage) && grabSettings.FarGrabEnabled)
            {
                _farTarget = candidate;
                _dwell = 0f;
                _lostAt = -1f;
            }

            UpdateLine();
        }


        /// <summary>
        /// Refreshes the valid target list and returns its first selectable entry that no other
        /// interactor holds and is not a blocked drawer handle, or null.
        /// </summary>
        private IXRSelectInteractable CurrentCandidate()
        {
            _validTargets.Clear();
            interactor.GetValidTargets(_validTargets);
            foreach (var t in _validTargets)
            {
                var select = t as IXRSelectInteractable;
                if (select == null) continue;
                // Something the other hand holds can be taken over from close by, never by a far dwell.
                if (IsHeldByOther(select) && !IsNear(select)) continue;
                if (HandleBlocked(select, _validTargets)) continue;
                return select;
            }
            return null;
        }

        /// <summary>
        /// True for a drawer handle this hand must not take: the palm is inside that drawer or above
        /// the top of its front (over the desk top), or another near target (a prop in the drawer)
        /// is in range.
        /// </summary>
        private bool HandleBlocked(IXRSelectInteractable target, List<IXRInteractable> others)
        {
            var drawer = DrawerSlide.ForHandle(target);
            if (drawer == null) return false;
            if (drawer.IsBehindFace(palmAnchor.position, HandleBehindFaceMargin)) return true;
            if (drawer.IsAboveFace(palmAnchor.position, HandleAboveFaceMargin)) return true;

            foreach (var t in others)
            {
                var other = t as IXRSelectInteractable;
                if (other == null || ReferenceEquals(other, target) || DrawerSlide.ForHandle(other) != null) continue;
                if (!IsHeldByOther(other) && IsNear(other)) return true;
            }
            return false;
        }

        /// <summary>
        /// True while the current far target is anywhere in the valid target list (refreshed by
        /// <see cref="CurrentCandidate"/> this frame) and is still a legal far target.
        /// </summary>
        private bool FarTargetStillValid()
        {
            bool listed = false;
            foreach (var t in _validTargets)
            {
                if (ReferenceEquals(t, _farTarget)) { listed = true; break; }
            }
            return listed && !IsHeldByOther(_farTarget) && !IsNear(_farTarget);
        }

        /// <summary>True when the target is selected by an interactor other than this hand's.</summary>
        private bool IsHeldByOther(IXRSelectInteractable target)
        {
            return target.isSelected && !interactor.IsSelecting(target);
        }

        /// <summary>True when any enabled collider of the interactable is within the near radius of the grab centre.</summary>
        private bool IsNear(IXRSelectInteractable interactable)
        {
            float r = grabSettings.NearGrabRadius;
            return NearSqrDistance(interactable) <= r * r;
        }

        /// <summary>Squared distance from the grab centre to the interactable's nearest usable collider (MaxValue if none).</summary>
        private float NearSqrDistance(IXRInteractable interactable)
        {
            var baseInteractable = interactable as XRBaseInteractable;
            if (baseInteractable == null) return float.MaxValue;

            Vector3 centre = _grabCentre.position;
            float best = float.MaxValue;
            foreach (var c in baseInteractable.colliders)
            {
                if (!IsUsableCollider(c)) continue;
                best = Mathf.Min(best, (ClosestPointOn(c, centre) - centre).sqrMagnitude);
            }
            return best;
        }

        /// <inheritdoc />
        public void Link(IXRInteractor linkedInteractor) { }

        /// <inheritdoc />
        public void Unlink(IXRInteractor linkedInteractor) { }

        /// <summary>
        /// Target filter: near targets first, nearest to the grab centre first (anything the other
        /// hand holds after every free one), with blocked drawer handles removed; everything else
        /// (far ray hits) follows in XRI's order.
        /// </summary>
        public void Process(IXRInteractor filterInteractor, List<IXRInteractable> targets, List<IXRInteractable> results)
        {
            results.Clear();
            _nearSorted.Clear();
            _nearDistance.Clear();
            float r2 = grabSettings.NearGrabRadius * grabSettings.NearGrabRadius;
            foreach (var t in targets)
            {
                float d = NearSqrDistance(t);
                if (d > r2) continue;
                _nearSorted.Add(t);
                // Held by the other hand: ranked behind every free target (distances are squared metres, far below this).
                var held = t as IXRSelectInteractable;
                _nearDistance[t] = held != null && IsHeldByOther(held) ? d + HeldByOtherPenalty : d;
            }

            if (_byNearDistance == null)
                _byNearDistance = (a, b) => _nearDistance[a].CompareTo(_nearDistance[b]);
            _nearSorted.Sort(_byNearDistance);

            foreach (var t in _nearSorted)
            {
                var select = t as IXRSelectInteractable;
                if (select != null && HandleBlocked(select, _nearSorted)) continue;
                results.Add(t);
            }
            foreach (var t in targets)
            {
                if (!_nearDistance.ContainsKey(t))
                    results.Add(t);
            }
        }

        private static bool IsUsableCollider(Collider c)
        {
            return c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy;
        }

        /// <summary>Closest point on a collider; non-convex mesh colliders fall back to their bounds.</summary>
        private static Vector3 ClosestPointOn(Collider c, Vector3 point)
        {
            var mesh = c as MeshCollider;
            return mesh != null && !mesh.convex ? c.bounds.ClosestPoint(point) : c.ClosestPoint(point);
        }

        /// <inheritdoc />
        public bool Process(IXRSelectInteractor selectInteractor, IXRSelectInteractable target)
        {
            if (!ReferenceEquals(selectInteractor, interactor))
                return true;
            // XRI re-checks filters on held objects; never drop something already held.
            if (interactor.IsSelecting(target))
                return true;
            if (IsHeldByOther(target))
                return IsNear(target);
            if (HandleBlocked(target, _validTargets))
                return false;
            if (IsNear(target))
                return true;
            if (target is BookPage || !grabSettings.FarGrabEnabled)
                return false;
            return ReferenceEquals(target, _farTarget) && DwellProgress >= 1f;
        }


        /// <summary>
        /// Draws the hand line: at full alpha to the UI hit while the ray is on a canvas (the XRI
        /// line visual is off, so this is the only UI pointer), else fading in with the dwell
        /// from the palm to the far target.
        /// </summary>
        private void UpdateLine()
        {
            if (_line == null) return;

            RaycastResult ui;
            if (interactor.TryGetCurrentUIRaycastResult(out ui) && (ui.isValid || ui.gameObject != null))
            {
                DrawLine(ui.worldPosition, 1f);
                return;
            }

            float p = DwellProgress;
            if (_farTarget == null || p <= 0f)
            {
                _line.enabled = false;
                return;
            }

            DrawLine(FarTargetEndPoint(), p);
        }

        /// <summary>
        /// Where the dwell line ends on the far target: the point of its first usable collider
        /// nearest the ray, found by taking the point on the ray closest to the collider's centre.
        /// One rule whether the ray's centre hits the item or only its cone catches it, so the end
        /// slides smoothly with the hand instead of jumping between the hit point and the centre.
        /// Falls back to the target's transform position when it has no usable collider.
        /// </summary>
        private Vector3 FarTargetEndPoint()
        {
            Collider col = null;
            var baseInteractable = _farTarget as XRBaseInteractable;
            if (baseInteractable != null)
            {
                foreach (var c in baseInteractable.colliders)
                {
                    if (IsUsableCollider(c)) { col = c; break; }
                }
            }

            if (col == null)
            {
                var comp = _farTarget as Component;
                return comp != null ? comp.transform.position : interactor.transform.position;
            }

            Transform origin = interactor.curveOrigin != null ? interactor.curveOrigin : interactor.transform;
            Vector3 dir = origin.forward;
            Vector3 centre = col.bounds.center;
            float along = Mathf.Max(0f, Vector3.Dot(centre - origin.position, dir));
            return ClosestPointOn(col, origin.position + dir * along);
        }

        private void DrawLine(Vector3 end, float alpha)
        {
            Color col = grabSettings.DwellLineColor;
            col.a *= alpha;
            _line.startColor = col;
            _line.endColor = col;
            _line.SetPosition(0, palmAnchor.position);
            _line.SetPosition(1, end);
            _line.enabled = true;
        }

        private void ResetFar()
        {
            _farTarget = null;
            _dwell = 0f;
            _lostAt = -1f;
        }

        private void Clear()
        {
            _nearTarget = null;
            ResetFar();
            if (_line != null) _line.enabled = false;
        }
    }
}
