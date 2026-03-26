using UnityEngine;
using TMPro;

/// <summary>
/// Simple test script for tooltip POC. Click a 3D object to simulate
/// snapping and show a tooltip with component info.
///
/// Setup:
///   1. Attach to a 3D object with a Collider (e.g. colored cube)
///   2. Assign the tooltip UI elements in Inspector
///   3. Set the component name and info
///   4. Click the object in Play Mode to show the tooltip
/// </summary>
public class TooltipTestButton : MonoBehaviour
{
    [Header("Component Info")]
    [Tooltip("Name of the spacecraft component")]
    [SerializeField] private string componentName = "Component";

    [Tooltip("Educational info about this component")]
    [TextArea(2, 5)]
    [SerializeField] private string componentInfo = "Info about this component.";

    [Header("UI References")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Visual Feedback")]
    [SerializeField] private Color defaultColor = Color.gray;
    [SerializeField] private Color snappedColor = Color.green;

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private bool _isSnapped;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);

        UpdateColor(defaultColor);
    }

    private void OnMouseDown()
    {
        if (_isSnapped) return;

        _isSnapped = true;
        UpdateColor(snappedColor);
        ShowTooltip();
    }

    private void ShowTooltip()
    {
        if (nameText != null)
            nameText.text = componentName;

        if (infoText != null)
            infoText.text = componentInfo;

        if (tooltipPanel != null)
            tooltipPanel.SetActive(true);
    }

    private void UpdateColor(Color color)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", color);
        _renderer.SetPropertyBlock(_propBlock);
    }
}
