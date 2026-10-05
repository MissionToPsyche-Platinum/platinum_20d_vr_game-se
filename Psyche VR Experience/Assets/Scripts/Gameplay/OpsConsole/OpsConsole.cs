using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The mission ops console's monitor: power state and which tab is showing.
    /// The keyboard's slap keys call <see cref="NextTab"/> / <see cref="PreviousTab"/> and the
    /// tower's power button calls <see cref="TogglePower"/>. While the power is off the screen is
    /// black and tab keys do nothing. Every visit starts on tab 1 with the power on, since the
    /// Event reset reloads the scene.
    ///
    /// Until the tab content lands (TG-195) the screen shows a placeholder: a tint per tab and a
    /// "TAB n / N" label, so slaps can be checked on the headset.
    /// </summary>
    public class OpsConsole : MonoBehaviour
    {
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        [Tooltip("The monitor's screen face.")]
        [SerializeField] private Renderer screen;

        [Tooltip("Placeholder label on the screen.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Number of tabs.")]
        [SerializeField] private int tabCount = 4;

        [Tooltip("Placeholder screen tint per tab, cycled if there are fewer colours than tabs.")]
        [SerializeField] private Color[] tabTints =
        {
            new Color(0.10f, 0.22f, 0.40f), new Color(0.12f, 0.32f, 0.22f),
            new Color(0.36f, 0.20f, 0.10f), new Color(0.26f, 0.14f, 0.36f),
        };

        [Tooltip("Screen colour while the power is off.")]
        [SerializeField] private Color offColor = new Color(0.02f, 0.02f, 0.025f);

        [SerializeField] private bool startOn = true;

        [Tooltip("Fires with the new tab index (0-based) whenever the tab changes or the screen turns on.")]
        [SerializeField] private UnityEvent<int> onTabShown = new UnityEvent<int>();

        [Tooltip("Fires with the new power state.")]
        [SerializeField] private UnityEvent<bool> onPowerChanged = new UnityEvent<bool>();

        public int CurrentTab { get; private set; }
        public bool IsOn { get; private set; }
        public int TabCount => tabCount;
        public UnityEvent<int> OnTabShown => onTabShown;
        public UnityEvent<bool> OnPowerChanged => onPowerChanged;

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetPower(startOn);
        }

        /// <summary>Shows the next tab, wrapping to the first. Ignored while off.</summary>
        public void NextTab()
        {
            if (!IsOn) return;
            ShowTab((CurrentTab + 1) % tabCount);
        }

        /// <summary>Shows the previous tab, wrapping to the last. Ignored while off.</summary>
        public void PreviousTab()
        {
            if (!IsOn) return;
            ShowTab((CurrentTab - 1 + tabCount) % tabCount);
        }

        /// <summary>Turns the screen off if on, on if off.</summary>
        public void TogglePower()
        {
            SetPower(!IsOn);
        }

        /// <summary>Turns the screen on (showing the current tab) or off (black).</summary>
        public void SetPower(bool on)
        {
            IsOn = on;
            if (label != null) label.gameObject.SetActive(on);
            onPowerChanged.Invoke(on);
            if (on) ShowTab(CurrentTab);
            else Paint(offColor);
        }

        private void ShowTab(int index)
        {
            CurrentTab = index;
            Paint(tabTints.Length > 0 ? tabTints[index % tabTints.Length] : Color.black);
            if (label != null) label.text = $"TAB {index + 1} / {tabCount}";
            onTabShown.Invoke(index);
        }

        private void Paint(Color c)
        {
            if (screen == null) return;
            screen.GetPropertyBlock(_block);
            _block.SetColor(BaseColorID, c);
            screen.SetPropertyBlock(_block);
        }
    }
}
