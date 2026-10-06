using System.Collections.Generic;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// One tab of the ops monitor. Built once into the screen's three panels (inactive until shown);
    /// <see cref="Show"/> and <see cref="Hide"/> switch its objects and start or stop its animations,
    /// so nothing animates off screen.
    /// </summary>
    public abstract class MonitorTab
    {
        /// <summary>The screen this tab was built into.</summary>
        protected MonitorScreen Screen;
        /// <summary>This tab's content.</summary>
        protected MonitorContent.Tab Data;
        private readonly List<GameObject> _roots = new List<GameObject>();

        /// <summary>Creates this tab's content in the screen's panels, inactive.</summary>
        public void Build(MonitorScreen screen, MonitorContent.Tab data)
        {
            Screen = screen; Data = data;
            _roots.Add(NewRoot(screen.Main)); _roots.Add(NewRoot(screen.Top)); _roots.Add(NewRoot(screen.Bottom));
            BuildPanels(_roots[0].transform, _roots[1].transform, _roots[2].transform);
            foreach (var r in _roots) r.SetActive(false);
        }

        /// <summary>Activates the tab and writes the header and footer.</summary>
        public virtual void Show()
        {
            foreach (var r in _roots) r.SetActive(true);
            Screen.Title.text = (Data.title ?? "").ToUpperInvariant();
            Screen.Banner.text = Data.banner ?? "";
            Screen.Footer.text = Data.footer ?? "";
            Screen.Credits.text = Credits();
        }

        /// <summary>Deactivates the tab's objects, which stops anything they animate.</summary>
        public virtual void Hide() { foreach (var r in _roots) r.SetActive(false); }

        /// <summary>Called every frame while shown.</summary>
        public virtual void Tick(float dt) { }

        /// <summary>Fills the main, top and bottom panel roots.</summary>
        protected abstract void BuildPanels(Transform main, Transform top, Transform bottom);

        /// <summary>Footer credit line for what is on screen, plus the as-of date.</summary>
        protected abstract string Credits();

        private static GameObject NewRoot(RectTransform panel)
        {
            var go = new GameObject("Tab", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.layer = panel.gameObject.layer;
            return go;
        }
    }
}
