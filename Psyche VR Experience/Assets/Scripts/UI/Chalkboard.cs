using UnityEngine;
using TMPro;

/// <summary>
/// A central display board (chalkboard) that shows info about spacecraft
/// components. Any ComponentInfo piece can update this board when grabbed.
///
/// Setup:
///   1. Create a Quad or Plane in the scene (the chalkboard surface)
///   2. Add a WorldSpace Canvas as a child with TextMeshPro elements
///   3. Attach this script and assign the text references
///   4. ComponentInfo pieces will find this board automatically via tag
///
/// Tag the chalkboard GameObject as "Chalkboard" so pieces can find it.
/// </summary>
public class Chalkboard : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("TextMeshPro element for the component name (large header).")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("TextMeshPro element for the educational info (body text).")]
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Default Text")]
    [Tooltip("Text shown when no piece is being held.")]
    [SerializeField] private string defaultName = "Psyche Spacecraft";

    [Tooltip("Default info when no piece is held.")]
    [TextArea(2, 5)]
    [SerializeField] private string defaultInfo = "Pick up a component to learn about it.";

    private void Start()
    {
        ShowDefault();
    }

    /// <summary>
    /// Update the chalkboard with component info. Called by ComponentInfo when grabbed.
    /// </summary>
    public void ShowComponentInfo(string componentName, string componentInfo)
    {
        if (nameText != null)
            nameText.text = componentName;

        if (infoText != null)
            infoText.text = componentInfo;
    }

    /// <summary>
    /// Reset the chalkboard to default text. Called by ComponentInfo when released.
    /// </summary>
    public void ShowDefault()
    {
        if (nameText != null)
            nameText.text = defaultName;

        if (infoText != null)
            infoText.text = defaultInfo;
    }
}
