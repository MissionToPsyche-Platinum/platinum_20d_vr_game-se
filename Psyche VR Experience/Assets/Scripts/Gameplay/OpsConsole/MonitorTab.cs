using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PsycheVR.OpsConsole.Core;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// One tab of the ops monitor. Built once into the screen's three panels (inactive until shown);
    /// <see cref="Show"/> and <see cref="Hide"/> switch its objects and start or stop its animations,
    /// so nothing animates off screen. Photo panels made with <see cref="Photos"/> are recorded: the
    /// base class starts and stops their rotation and builds the credit line from their photos.
    /// </summary>
    public abstract class MonitorTab
    {
        /// <summary>The screen this tab was built into.</summary>
        protected MonitorScreen Screen;
        /// <summary>This tab's content.</summary>
        protected MonitorContent.Tab Data;
        private const string CreditSeparator = " · ";
        private const string AsOfSeparator = "\n";   // the as-of date on its own line under the credits (both right-aligned)
        private const string AsOfPrefix = "as of ";
        private const string AsOfFormat = "MMM d, yyyy";

        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<PhotoPanel> _photoPanels = new List<PhotoPanel>();
        private readonly List<MonitorContent.Photo[]> _photoSets = new List<MonitorContent.Photo[]>();
        private readonly List<string> _viewCredits = new List<string>();
        private string _title;

        // parsed once per snapshot asset and shared by every tab
        private static TextAsset _snapshotSource;
        private static MonitorSnapshot _snapshot;

        /// <summary>
        /// Creates this tab's content in the screen's panels, inactive. <paramref name="fallbackTitle"/>
        /// shows when the content has no title (the content itself is never written to).
        /// </summary>
        public void Build(MonitorScreen screen, MonitorContent.Tab data, string fallbackTitle = "")
        {
            Screen = screen; Data = data;
            _title = string.IsNullOrEmpty(data.title) ? fallbackTitle ?? "" : data.title;
            _roots.Add(NewRoot(screen.Main)); _roots.Add(NewRoot(screen.Top)); _roots.Add(NewRoot(screen.Bottom));
            BuildPanels(_roots[0].transform, _roots[1].transform, _roots[2].transform);
            foreach (var r in _roots) r.SetActive(false);
        }

        /// <summary>Activates the tab, writes the header and footer and starts the photo rotations.</summary>
        public virtual void Show()
        {
            foreach (var r in _roots) r.SetActive(true);
            Screen.Title.text = _title;   // the title style is FontStyles.UpperCase
            Screen.Banner.text = Data.banner ?? "";
            Screen.Footer.text = Data.footer ?? "";
            Screen.Credits.text = Credits();
            foreach (var p in _photoPanels) p.Play(true);
        }

        /// <summary>Stops the photo rotations and deactivates the tab's objects.</summary>
        public virtual void Hide()
        {
            foreach (var p in _photoPanels) p.Play(false);
            foreach (var r in _roots) r.SetActive(false);
        }

        /// <summary>Called every frame while shown.</summary>
        public virtual void Tick(float dt) { }

        /// <summary>Fills the main, top and bottom panel roots.</summary>
        protected abstract void BuildPanels(Transform main, Transform top, Transform bottom);

        /// <summary>
        /// Footer credit line: credits added with <see cref="AddCredit"/>, then the recorded panels' photo
        /// credits, distinct and non-empty, in order, joined with " · ", then the snapshot's as-of date on
        /// a second line (omitted without a snapshot; one line when there are no credits).
        /// </summary>
        protected virtual string Credits()
        {
            string line = string.Join(CreditSeparator, _viewCredits.Concat(_photoSets.SelectMany(s => s)
                .Where(p => p != null && p.sprite != null && !string.IsNullOrEmpty(p.credit))
                .Select(p => p.credit)).Distinct());
            var snapshot = Snapshot;
            if (snapshot == null) return line;
            string asOf = AsOfPrefix + snapshot.BuildDate.ToString(AsOfFormat, CultureInfo.InvariantCulture);
            return line.Length == 0 ? asOf : line + AsOfSeparator + asOf;
        }

        /// <summary>The content's build-date snapshot, parsed once; null if missing or unreadable.</summary>
        protected MonitorSnapshot Snapshot
        {
            get
            {
                var source = Screen != null && Screen.Content != null ? Screen.Content.snapshot : null;
                if (source == _snapshotSource) return _snapshot;
                _snapshotSource = source;
                _snapshot = null;
                if (source == null) return null;
                try
                {
                    var parsed = MonitorSnapshot.Parse(source.text);
                    _ = parsed.BuildDate;   // reject a snapshot whose date does not parse
                    _snapshot = parsed;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MonitorTab] snapshot {source.name} is unreadable ({e.Message}); numbers from it stay hidden.");
                }
                return _snapshot;
            }
        }

        /// <summary>
        /// A photo panel over <paramref name="parent"/> showing <paramref name="photos"/>, recorded for the
        /// rotation and the credit line.
        /// </summary>
        protected PhotoPanel Photos(Transform parent, MonitorContent.Photo[] photos, float secondsEach = 0f)
        {
            // the right-hand stack's photos are small, so a caption over their bottom edge hid too much of
            // them and left a sliver of photo under the strip: theirs sits in its own strip below
            bool small = parent.parent != Screen.Main;
            var panel = PhotoPanel.Create((RectTransform)parent, Screen.Content, captionBelow: small);
            panel.Set(photos, secondsEach);
            _photoPanels.Add(panel);
            _photoSets.Add(photos ?? new MonitorContent.Photo[0]);
            return panel;
        }

        /// <summary>Adds the credit of a non-photo view (e.g. its data source) to the credit line, ahead of the photo credits.</summary>
        protected void AddCredit(string credit)
        {
            if (!string.IsNullOrEmpty(credit)) _viewCredits.Add(credit);
        }

        /// <summary>A stat panel over <paramref name="parent"/> showing <paramref name="stats"/>.</summary>
        protected StatPanel Stats(Transform parent, MonitorContent.Stat[] stats)
        {
            var panel = StatPanel.Create((RectTransform)parent, Screen.Content);
            panel.Set(stats);
            return panel;
        }

        private static GameObject NewRoot(RectTransform panel)
        {
            var rt = MonitorUi.Child(panel, "Tab");
            MonitorUi.Stretch(rt);
            return rt.gameObject;
        }
    }
}
