using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to each spacecraft piece. When the player grabs/holds this piece,
/// it updates the Chalkboard with the component's name and educational info.
/// When released, the chalkboard resets to default.
///
/// Also works with mouse click for desktop testing (no VR needed).
///
/// Setup:
///   1. Attach to a grabbable piece (must have a Collider)
///   2. Set Component Name and Component Info in Inspector
///   3. Assign the Chalkboard reference (or tag a Chalkboard object as "Chalkboard")
/// </summary>
public class ComponentInfo : MonoBehaviour
{
    [Header("Component Info")]
    [Tooltip("Name of this spacecraft component.")]
    [SerializeField] private string componentName = "Component";

    [Tooltip("Educational info about this component.")]
    [TextArea(2, 5)]
    [SerializeField] private string componentInfo = "Info about this component.";

    [Header("Chalkboard Reference")]
    [Tooltip("The Chalkboard to update. If null, searches for a GameObject tagged 'Chalkboard'.")]
    [SerializeField] private Chalkboard chalkboard;

    private XRGrabInteractable _grab;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();

        if (chalkboard == null)
        {
            var boardObj = GameObject.FindGameObjectWithTag("Chalkboard");
            if (boardObj != null)
                chalkboard = boardObj.GetComponent<Chalkboard>();
        }
    }

    private void OnEnable()
    {
        if (_grab != null)
        {
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (_grab != null)
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }
    }

    /// <summary>
    /// Mouse click for desktop testing.
    /// </summary>
    private void OnMouseDown()
    {
        if (chalkboard != null)
            chalkboard.ShowComponentInfo(componentName, componentInfo);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (chalkboard != null)
            chalkboard.ShowComponentInfo(componentName, componentInfo);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (chalkboard != null)
            chalkboard.ShowDefault();
    }
}
