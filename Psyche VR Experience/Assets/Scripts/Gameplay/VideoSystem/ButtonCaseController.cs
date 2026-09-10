using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ButtonCaseController : MonoBehaviour
{
    private HingeJoint _hinge;
    private XRGrabInteractable _grabInteractable;
    private bool _isOpen = false;
    private bool _assistActive = false;
    private bool _forceOpening = false;

    public HingeJoint Hinge { get => _hinge; set => _hinge = value; }
    [SerializeField] private bool startOpenForTesting = false;
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

    [Tooltip("How close (degrees) the hinge has to be to the open angle before the case is frozen in place.")]
    [SerializeField] private float lockAngleTolerance = 2f;

    [Tooltip("Seconds to wait for the case to finish its swing before freezing it wherever it is.")]
    [SerializeField] private float lockSettleTimeout = 2f;


    public bool GetIsOpen()
    {
        return _isOpen;
    }

    public void SetIsOpen(bool value) => _isOpen = value;

    // Wired to this object's own XRGrabInteractable Select Entered event, so
    // selecting the case (the "Open The Case" prompt) opens it immediately
    // instead of requiring the player to physically swing it open. Reuses
    // the same finalize-open logic the hinge swing itself used to reach, so
    // the end state is identical either way.
    public void OpenOnInteract()
    {
        if (GetIsOpen())
            return;

        if (Hinge == null)
            Hinge = GetComponent<HingeJoint>();

        if (Hinge == null)
        {
            Debug.LogError("[ButtonCaseController] OpenOnInteract called but no HingeJoint was found.", this);
            return;
        }

        // Do not jump straight to LockCaseOpen() here: it makes the
        // Rigidbody kinematic immediately, which would freeze the case
        // wherever it currently sits (likely still closed at 0 degrees)
        // since a kinematic body no longer responds to the hinge spring.
        // Instead, kick off the same spring-assisted swing used when a
        // player releases the case mid-swing, and let Update() finish the
        // job once the hinge has actually rotated past openAngleThreshold.
        // _forceOpening keeps that spring engaged every frame regardless of
        // angle, since the normal manual-swing logic below would otherwise
        // turn it back off immediately (it only expects the assist to run
        // once the player has already swung past assistAngleThreshold).
        _forceOpening = true;
        ApplyOpenAssist();
    }

    void Start()
    {
        Hinge = GetComponent<HingeJoint>();
        if (Hinge == null)
        {
            Debug.LogError("[ButtonCaseController] No HingeJoint was found.", this);
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

        if (Mathf.Abs(angle) >= Mathf.Abs(openAngleThreshold))
        {
            LockCaseOpen();
            return;
        }

        if (_forceOpening)
        {
            // Keep driving toward open every frame until the threshold
            // above is reached, regardless of grab state or angle.
            ApplyOpenAssist();
            return;
        }

        // Assist: once the player has swung the case far enough and let go,
        // finish the motion for them instead of requiring the final few
        // degrees to be landed precisely while holding it.
        bool isGrabbed = _grabInteractable != null && _grabInteractable.isSelected;
        bool pastAssistPoint = Mathf.Abs(angle) >= Mathf.Abs(assistAngleThreshold);

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
        spring.targetPosition = GetOpenTargetAngle();
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
        _forceOpening = false;

        // Release the grab before touching the Rigidbody. While the case is
        // still selected, the XRGrabInteractable keeps driving the body toward
        // the player's hand, so freezing it here can leave the case stuck in a
        // pose the hinge never actually reached.
        ReleaseGrab();

        // Narrow the hinge limits to a small window at the open angle and keep
        // the spring pushing into it. openAngleThreshold is only the point at
        // which the case counts as open; the case still has to finish its swing
        // before it is frozen, otherwise it stops short of the open position.
        float openAngle = GetOpenTargetAngle();
        JointLimits limits = Hinge.limits;
        limits.min = openAngle >= 0f ? openAngle - lockAngleTolerance : openAngle;
        limits.max = openAngle >= 0f ? openAngle : openAngle + lockAngleTolerance;
        Hinge.limits = limits;

        ApplyOpenAssist();
        StartCoroutine(FreezeWhenOpen(openAngle));

        //Unlock THE button
        if (launchButton != null)
        {
            launchButton.UnlockButton();
        }
    }

    // Cancels any in-progress selection, then takes the interactable out of
    // play so the case cannot be grabbed again once it is open.
    private void ReleaseGrab()
    {
        if (_grabInteractable == null)
            _grabInteractable = GetComponent<XRGrabInteractable>();

        if (_grabInteractable == null)
            return;

        if (_grabInteractable.isSelected && _grabInteractable.interactionManager != null)
            _grabInteractable.interactionManager.CancelInteractableSelection(
                (IXRSelectInteractable)_grabInteractable);

        _grabInteractable.enabled = false;
    }

    // Waits for the spring to carry the case the rest of the way open, then
    // freezes it there. The timeout is a backstop: if something blocks the
    // swing, the case still ends up locked rather than swinging forever.
    private IEnumerator FreezeWhenOpen(float openAngle)
    {
        float deadline = Time.time + lockSettleTimeout;

        while (Time.time < deadline &&
               Mathf.Abs(Hinge.angle - openAngle) > lockAngleTolerance)
        {
            yield return new WaitForFixedUpdate();
        }

        RemoveOpenAssist();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private float GetOpenTargetAngle()
    {
        JointLimits limits = Hinge.limits;
        return Mathf.Abs(limits.max) >= Mathf.Abs(limits.min) ? limits.max : limits.min;
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
