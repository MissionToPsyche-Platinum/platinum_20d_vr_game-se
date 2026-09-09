using UnityEngine;

public class InteractionIndicator : MonoBehaviour
{
    public bool isActive = true;
    public float rotationSpeed = 90f;
    private SnappableObject snappableObject;
    public MeshRenderer mesh;

    void Start()
    {
        snappableObject = GetComponentInParent<SnappableObject>();
    }

    void Update()
    {
        if (snappableObject.isSnapped) {
            isActive = false;
            mesh.enabled = false;
        }

        if (isActive)
        {
            transform.Rotate(0f, rotationSpeed * Time.deltaTime,0f);
        }
    }
}
