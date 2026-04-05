
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine;
using System.Globalization;

public class SnappableObject : MonoBehaviour
{
    public Transform snapPoint;
    public bool isSnapped;
    public string objTag => gameObject.tag;
    public string description;
    public InstructionTextManager textManager;

    //disables grab for everything but the Bus (Tutorial object)
    void Start()
    {
        XRGrabInteractable grab = GetComponent<XRGrabInteractable>();

        if (!CompareTag("PSYCHE_Bus"))
        {
            if (grab != null)
                grab.enabled = false;
        }
    }

    public void SnapTo(Transform snapAnchor)
    {
        Debug.Log("Object root: " + transform.name);
        Debug.Log("SnapPoint object: " + snapPoint.name);
        Debug.Log("SnapPoint parent: " + snapPoint.parent.name);
        Debug.Log("Root position: " + transform.position);
        Debug.Log("SnapPoint world position: " + snapPoint.position);
        Debug.Log("Anchor world position: " + snapAnchor.position);
        if (snapPoint == null)
        {
            Debug.LogError("snapPoint is null on " + gameObject.name);
            return;
        }

        if (snapAnchor == null)
        {
            Debug.LogError("snapAnchor is null");
            return;
        }

        Debug.Log("Before snap: object = " + transform.position +
                  " | snapPoint = " + snapPoint.position +
                  " | anchor = " + snapAnchor.position);

        Quaternion rotationDelta = snapAnchor.rotation * Quaternion.Inverse(snapPoint.rotation);
        transform.rotation = rotationDelta * transform.rotation;

        Vector3 positionDelta = snapAnchor.position - snapPoint.position;
        transform.position += positionDelta;

        Debug.Log("After snap: object = " + transform.position +
                  " | snapPoint = " + snapPoint.position);

        isSnapped = true;
        textManager.SetText(description);
        if (objTag.Equals("PSYCHE_Bus")) {
            EnableAllOtherSnappables();
        }
    }

    private void EnableAllOtherSnappables()
    {
        SnappableObject[] all = FindObjectsByType<SnappableObject>(FindObjectsSortMode.None);

        foreach (SnappableObject obj in all)
        {
            if (!obj.CompareTag("PSYCHE_Bus"))
            {
                XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
                if (grab != null)
                {
                    grab.enabled = true;
                }
            }
        }
    }
}
