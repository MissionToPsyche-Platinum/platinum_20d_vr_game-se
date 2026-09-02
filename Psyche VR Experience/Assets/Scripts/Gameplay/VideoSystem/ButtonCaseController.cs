using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ButtonCaseController : MonoBehaviour
{
    private HingeJoint _hinge;
    private XRGrabInteractable _grabInteractable;
    private bool _isOpen = false;
    private bool _assistActive = false;

    public HingeJoint Hinge { get => _hinge; set => _hinge = value; }
    [SerializeField] private bool startOpenForTesting = true;
    [SerializeField] ButtonController launchButton;

    [Header("Open Behavior")]
    [Tooltip("Hinge angle (degrees) at which the case is considered fully open. Lowered from the hinge's max so players don't have to swing it to the exact physical limit.")]
    [SerializeField] private float openAngleThreshold = 60f;

    [Tooltip("Hinge angle (degrees) past which, once the player lets go, the case will finish opening on its own instead of requiring a precise final swing.")]
    [SerializeField] private float assistAngleThreshold = 35f;

    [Tooltip("Spring force used to gently carry the case the rest of the way open once the assist kicks in.")]
    [SerializeField] private float assistSpringForce = 40f;

    [Tooltip("Spring damper used alongside assistSpringForce.")]
    [SerializeField] private float assistSpringDamper = 4f;


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

        _grabInteractable = GetComponent<XRGrabInteractable>();

        if (startOpenForTesting)
            UnlockCaseForTesting();
    }

    void Update()
    {
        if (GetIsOpen() || Hinge == null || Mathf.Approximately(Time.timeScale, 0f))
            return;

        float angle = Hinge.angle;

        if (angle >= openAngleThreshold)
        {
            LockCaseOpen();
            return;
        }

        // Assist: once the player has swung the case far enough and let go,
        // finish the motion for them instead of requiring the final few
        // degrees to be landed precisely while holding it.
        bool isGrabbed = _grabInteractable != null && _grabInteractable.isSelected;
        bool pastAssistPoint = angle >= assistAngleThreshold;

        if (pastAssistPoint && !isGrabbed)
        {
            ApplyOpenAssist();
        }
        else if (_assistActive)
        {
            RemoveOpenAssist();
        }
    }

    private void ApplyOpenAssist()
    {
        if (Hinge == null)
            return;

        _assistActive = true;

        Hinge.useSpring = true;
        JointSpring spring = Hinge.spring;
        spring.spring = assistSpringForce;
        spring.damper = assistSpringDamper;
        spring.targetPosition = Hinge.limits.max;
        Hinge.spring = spring;
    }

    private void RemoveOpenAssist()
    {
        _assistActive = false;

        if (Hinge != null)
            Hinge.useSpring = false;
    }

    private void LockCaseOpen()
    {
        SetIsOpen(true);

        RemoveOpenAssist();

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

        //Unlock THE button
        if(launchButton != null)
        {
            launchButton.UnlockButton();
        }
     
    }

    private void UnlockCaseForTesting()
    {
        SetIsOpen(true);

        Collider caseCollider = GetComponent<Collider>();
        if (caseCollider != null)
            caseCollider.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        var grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
            grabInteractable.enabled = false;

        if (launchButton != null)
            launchButton.UnlockButton();
    }
}
