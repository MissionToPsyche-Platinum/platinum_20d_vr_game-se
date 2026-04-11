using UnityEngine;

public class ButtonCaseController : MonoBehaviour
{
    private HingeJoint _hinge;
    private bool _isOpen = false;

    public HingeJoint Hinge { get => _hinge; set => _hinge = value; }


    public bool GetIsOpen()
    {
        return _isOpen;
    }

    public void SetIsOpen(bool value) => _isOpen = value;

    void Start()
    {
        Hinge = GetComponent<HingeJoint>();
        if (Hinge != null)
        {
            Debug.Log("Succefully stored hinge!");
        } else
        {
            Debug.Log("Failed to load hinge!");
        }

      
        
    }

    void Update()
    {
       
        if (GetIsOpen() || Hinge == null || Mathf.Approximately(Time.timeScale, 0f))
            return;

        if (Hinge.angle >= 85f)
        {
            LockCaseOpen();
            Debug.Log(Hinge.angle);
        }
        Debug.Log(Hinge.angle);

    }

    private void LockCaseOpen()
    {
        SetIsOpen(true);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Lock the hinge permanently at 90 degree
        JointLimits limits = Hinge.limits;
        limits.min = 90f;
        limits.max = 90f;
        Hinge.limits = limits;

        // disable the grab interactable
        var grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        } else
        {
            var oldGrab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (oldGrab != null)
            {
                oldGrab.enabled = false;
            }
        }

     


    }
}
