using PsycheVR.OpsConsole.Core;
using UnityEngine;
using UnityEngine.Events;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The mission ops console's monitor: power, which tab shows, and the idle attract cycle.
    /// The keyboard's slap keys call <see cref="NextTab"/> / <see cref="PreviousTab"/> and the
    /// tower's power button calls <see cref="TogglePower"/>. While the power is off the screen is
    /// black and tab keys still sink but change nothing. Every visit starts on tab 1 with the power
    /// on, since the Event reset reloads the scene.
    ///
    /// The screen itself (<see cref="MonitorScreen"/>) is built in code on <see cref="screen"/> at
    /// Awake, from <see cref="content"/>. After <see cref="IdleSeconds"/> without a slap the tabs
    /// turn on their own every <see cref="AttractStepSeconds"/> under the content's attract prompt.
    /// Tab animations and the idle clock follow scaled time and freeze while the game is paused;
    /// page slides and photo fades use unscaled time.
    /// </summary>
    public class OpsConsole : MonoBehaviour
    {
        private const string CanvasName = "MonitorCanvas";
        private const float IdleSeconds = 45f;
        private const float AttractStepSeconds = 10f;
        private const int MarsFlybyIndex = 0;
        private const int DsnIndex = 1;            // the ping (TG-227) jumps here
        private const int SolarPowerIndex = 2;
        private const int TabTotal = 4;

        [Tooltip("The monitor's screen face; the screen canvas is built on it.")]
        [SerializeField] private Renderer screen;

        [Tooltip("Everything the screen shows.")]
        [SerializeField] private MonitorContent content;

        [Tooltip("Plays the page click.")]
        [SerializeField] private AudioSource clickSource;

        [SerializeField] private bool startOn = true;

        [Tooltip("Fires with the new tab index (0-based) whenever the tab changes or the screen turns on.")]
        [SerializeField] private UnityEvent<int> onTabShown = new UnityEvent<int>();

        [Tooltip("Fires with the new power state.")]
        [SerializeField] private UnityEvent<bool> onPowerChanged = new UnityEvent<bool>();

        /// <summary>Zero-based index of the tab on screen.</summary>
        public int CurrentTab => _state != null ? _state.CurrentTab : 0;
        /// <summary>True while the screen is powered on.</summary>
        public bool IsOn => _state != null && _state.IsOn;
        /// <summary>Number of tabs.</summary>
        public int TabCount => TabTotal;
        /// <summary>Fires with the new tab index whenever the tab changes or the screen turns on.</summary>
        public UnityEvent<int> OnTabShown => onTabShown;
        /// <summary>Fires with the new power state.</summary>
        public UnityEvent<bool> OnPowerChanged => onPowerChanged;
        /// <summary>Everything the screen shows (read by the mouse's push haptic for its lines).</summary>
        public MonitorContent Content => content;

        private ConsoleState _state;
        private MonitorScreen _screen;
        private MonitorTab[] _tabs;
        private int _shownTab = -1;
        private bool _attractShown;
        private bool _failed;
        private bool _addedScreen;   // MonitorScreen was added by EnsureBuilt (not authored on the prefab)
        private bool _footerOverridden;
        private string _tabFooter;   // the tab's own footer, restored when the override ends

        private void Awake()
        {
            if (!EnsureBuilt()) return;
            SetPower(startOn);
        }

        /// <summary>Shows the next tab, wrapping to the first. Ignored while off.</summary>
        public void NextTab()
        {
            if (_state != null && _state.Next()) Turn(+1);
        }

        /// <summary>Shows the previous tab, wrapping to the last. Ignored while off.</summary>
        public void PreviousTab()
        {
            if (_state != null && _state.Previous()) Turn(-1);
        }

        /// <summary>Turns the screen off if on, on if off.</summary>
        public void TogglePower()
        {
            SetPower(!IsOn);
        }

        /// <summary>
        /// Shows <paramref name="text"/> in the footer instead of the current tab's footer; null puts
        /// the tab's footer back. A tab change or a power change ends the override on its own.
        /// </summary>
        public void SetFooterOverride(string text)
        {
            if (_screen == null || _screen.Footer == null) return;
            if (text == null)
            {
                if (!_footerOverridden) return;
                _footerOverridden = false;
                _screen.Footer.text = _tabFooter;
                return;
            }
            if (!_footerOverridden) _tabFooter = _screen.Footer.text;
            _footerOverridden = true;
            _screen.Footer.text = text;
        }

        /// <summary>Turns the screen on (showing the current tab) or off (black).</summary>
        public void SetPower(bool on)
        {
            if (_state == null) return;
            SetFooterOverride(null);
            _state.SetPower(on);
            _screen.SetBlack(!on);
            _screen.HideAttract();
            _attractShown = false;
            onPowerChanged.Invoke(on);
            if (on) ShowCurrent();
            else HideShown();
        }

        /// <summary>
        /// Edit-mode preview for renders: builds the screen if needed, powers it on and shows
        /// <paramref name="tab"/> at once (no slide, no click, no coroutines).
        /// </summary>
        public void BuildForPreview(int tab)
        {
            if (!EnsureBuilt()) return;
            if (!Application.isPlaying) MarkDontSave();
            _state.SetPower(true);
            _screen.SetBlack(false);
            _screen.HideAttract();
            int target = ((tab % TabTotal) + TabTotal) % TabTotal;
            while (_state.CurrentTab != target) _state.Next();
            ShowCurrent();
        }

        private void Update()
        {
            if (_state == null) return;
            if (_state.Tick(Time.deltaTime)) _screen.Slide(+1, ShowCurrent);
            if (_state.IsAttracting != _attractShown)
            {
                _attractShown = _state.IsAttracting;
                if (_attractShown) _screen.ShowAttract(content.attractPrompt);
                else _screen.HideAttract();
            }
            if (_shownTab >= 0) _tabs[_shownTab].Tick(Time.deltaTime);
        }

        /// <summary>Builds state, screen and tabs once. Logs one error and disables itself if it cannot.</summary>
        private bool EnsureBuilt()
        {
            if (_state != null) return true;
            if (_failed) return false;
            if (content == null || screen == null)
                return Fail($"[OpsConsole] {name}: {(content == null ? "content" : "screen")} is not assigned; the monitor stays blank.");

            _screen = GetComponent<MonitorScreen>();
            _addedScreen = _screen == null;
            if (_addedScreen) _screen = gameObject.AddComponent<MonitorScreen>();
            // A canvas left by an earlier edit-mode preview (its references are lost on script reload):
            // remove it so Build never makes a second one.
            if (_screen.Title == null)
                foreach (Transform child in transform)
                    if (child.name == CanvasName) { DestroyImmediate(child.gameObject); break; }
            _screen.Build(screen, content);
            if (_screen.Title == null) return Fail(null);   // MonitorScreen logged why

            var data = content.Tabs;
            _tabs = new MonitorTab[TabTotal];
            for (int i = 0; i < TabTotal; i++)
            {
                var tab = i < data.Length && data[i] != null ? data[i] : new MonitorContent.Tab();
                _tabs[i] = NewTab(i);
                _tabs[i].Build(_screen, tab, $"Tab {i + 1}");
            }
            _state = new ConsoleState(TabTotal, IdleSeconds, AttractStepSeconds);
            return true;
        }

        /// <summary>
        /// Edit-mode previews build into the open scene: flag what they created DontSave so saving the
        /// scene (or prefab) never stores the preview canvas.
        /// </summary>
        private void MarkDontSave()
        {
            if (_addedScreen) _screen.hideFlags |= HideFlags.DontSave;
            Transform canvas = transform.Find(CanvasName);
            if (canvas == null) return;
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags |= HideFlags.DontSave;
                foreach (var c in t.GetComponents<Component>()) c.hideFlags |= HideFlags.DontSave;
            }
        }

        private bool Fail(string message)
        {
            if (message != null) Debug.LogError(message, this);
            _failed = true;
            enabled = false;
            return false;
        }

        private void Turn(int direction)
        {
            if (clickSource != null && content.pageClick != null) clickSource.PlayOneShot(content.pageClick);
            _screen.Slide(direction, ShowCurrent);
        }

        /// <summary>Shows the state's current tab (hiding the previous one) and lights its dot.</summary>
        private void ShowCurrent()
        {
            // a slide that finishes after power off must not bring the tab back
            if (!_state.IsOn) return;
            int current = _state.CurrentTab;
            if (_shownTab != current) HideShown();
            _footerOverridden = false;   // Show writes the new tab's own footer
            _tabs[current].Show();
            _shownTab = current;
            _screen.SetDots(current, TabTotal);
            onTabShown.Invoke(current);
        }

        private void HideShown()
        {
            if (_shownTab >= 0) _tabs[_shownTab].Hide();
            _shownTab = -1;
        }

        /// <summary>The tab class for screen position <paramref name="index"/>, in <see cref="MonitorContent.Tabs"/> order.</summary>
        private static MonitorTab NewTab(int index)
        {
            switch (index)
            {
                case MarsFlybyIndex: return new MarsFlybyTab();
                case DsnIndex: return new DsnTab();
                case SolarPowerIndex: return new SolarPowerTab();
                default: return new ThrusterTab();
            }
        }
    }
}
