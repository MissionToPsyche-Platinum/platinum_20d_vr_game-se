
using UnityEngine;
using System.Collections.Generic;

public class SnappableObject : MonoBehaviour
{
    public Transform snapPoint;
    public bool isSnapped;
    public string objectDescription;




    public void SnapTo(Transform snapAnchor)
    {
        if (snapPoint == null || snapAnchor == null) return;

        Quaternion rotationDelta = snapAnchor.rotation * Quaternion.Inverse(snapPoint.rotation);
        transform.rotation = rotationDelta * transform.rotation;

        Vector3 positionDelta = snapAnchor.position - snapPoint.position;
        transform.position += positionDelta;

        isSnapped = true;
    }


}
