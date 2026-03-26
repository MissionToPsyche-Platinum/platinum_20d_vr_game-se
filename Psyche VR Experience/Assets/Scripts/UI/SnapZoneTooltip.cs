using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays an educational tooltip when a piece is snapped into a SnapZone.
/// Attach this to the same GameObject as a SnapZone component.
///
/// Setup:
///   1. Add this component alongside SnapZone on a snap zone object
///   2. Assign the tooltipPanel (a UI Canvas/Panel with TextMeshPro)
///   3. Set componentName and componentInfo for this zone's piece
///   4. When a piece snaps in, the tooltip fades in with the info
/// </summary>
[RequireComponent(typeof(SnapZone))]
public class SnapZoneTooltip : MonoBehaviour
{
    [Header("Tooltip Content")]
    [Tooltip("Name of the spacecraft component (e.g. 'Solar Panel')")]
    [SerializeField] private string componentName = "Component";

    [Tooltip("Educational info shown when piece is placed")]
    [TextArea(2, 5)]
    [SerializeField] private string componentInfo = "Info about this component.";

    [Header("UI References")]
    [Tooltip("The tooltip panel GameObject (Canvas with text). Will be shown/hidden.")]
    [SerializeField] private GameObject tooltipPanel;

    [Tooltip("TextMeshPro element for the component name.")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("TextMeshPro element for the component info.")]
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Settings")]
    [Tooltip("Delay before tooltip appears after snap (seconds).")]
    [SerializeField] private float showDelay = 0.5f;

    [Tooltip("How long the tooltip stays visible (0 = forever).")]
    [SerializeField] private float displayDuration = 0f;

    [Tooltip("Fade in duration (seconds).")]
    [SerializeField] private float fadeInDuration = 0.3f;

    private SnapZone _snapZone;
    private CanvasGroup _canvasGroup;
    private bool _hasShown;

    private void Awake()
    {
        _snapZone = GetComponent<SnapZone>();

        if (tooltipPanel != null)
        {
            _canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            tooltipPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (_hasShown) return;

        if (_snapZone.hasSnapped)
        {
            _hasShown = true;
            StartCoroutine(ShowTooltip());
        }
    }

    private IEnumerator ShowTooltip()
    {
        yield return new WaitForSeconds(showDelay);

        if (nameText != null)
            nameText.text = componentName;

        if (infoText != null)
            infoText.text = componentInfo;

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);

            // Fade in
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // Auto-hide after duration (if set)
            if (displayDuration > 0f)
            {
                yield return new WaitForSeconds(displayDuration);
                yield return StartCoroutine(HideTooltip());
            }
        }
    }

    private IEnumerator HideTooltip()
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        tooltipPanel.SetActive(false);
    }

    /// <summary>
    /// Call this to manually hide the tooltip (e.g. from a close button).
    /// </summary>
    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(HideTooltip());
    }
}
