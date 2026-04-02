

using UnityEngine;

public class SnappableObject : MonoBehaviour
{
    public Transform snapPoint;
    public bool isSnapped;

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
    }
}
