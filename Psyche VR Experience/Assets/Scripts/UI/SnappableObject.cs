using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit;

public class SnappableObject : MonoBehaviour
{
    public Transform snapPoint;
    public bool isSnapped;
    public string objectDescription;
    public string objectTitle;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Rigidbody rb;

    public void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        rb = GetComponent<Rigidbody>();
    }

    public void SnapTo(Transform snapAnchor)
    {
        if (snapPoint == null || snapAnchor == null) return;

        Quaternion rotationDelta = snapAnchor.rotation * Quaternion.Inverse(snapPoint.rotation);
        transform.rotation = rotationDelta * transform.rotation;

        Vector3 positionDelta = snapAnchor.position - snapPoint.position;
        transform.position += positionDelta;

        isSnapped = true;
    }

    public void ResetPiece()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.position = startPosition;
            rb.rotation = startRotation;
        }
        else
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }

        isSnapped = false;
    }
}
