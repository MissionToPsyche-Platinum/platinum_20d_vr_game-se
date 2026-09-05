using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ButtonController : MonoBehaviour
{
    public Transform buttonTop;
    public float pressDistance = 0.01f;
    public float pressSpeed = 5f;

    [Header("Hand Slap")]
    [Tooltip("A hand within this radius (meters) of the button is considered touching it.")]
    [SerializeField] private float slapRadius = 0.08f;

    [Tooltip("Minimum hand speed (m/s) required for a touch to count as a slap, so just resting a hand near the button doesn't press it.")]
    [SerializeField] private float minSlapSpeed = 0.3f;

    [Tooltip("Seconds to wait after a slap before another one can be registered.")]
    [SerializeField] private float slapCooldown = 0.5f;

    private Vector3 _originalPosition;
    private Vector3 _pressedPosition;
    private bool _isAnimating = false;
    private XRSimpleInteractable _interactable;

    private readonly List<Transform> _hands = new List<Transform>();
    private readonly Dictionary<Transform, Vector3> _lastHandPositions = new Dictionary<Transform, Vector3>();
    private float _lastSlapTime = -Mathf.Infinity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Selected");
        _interactable = GetComponent<XRSimpleInteractable>();

        // Disable the interactable at startup
        if (_interactable != null)
        {
            _interactable.enabled = true;
        }


        if (buttonTop != null)
        {
            _originalPosition = buttonTop.localPosition;
            _pressedPosition = _originalPosition - new Vector3(0, pressDistance, 0);
        }

        FindHands();
    }

    // Locates the player's hand controllers so we can detect a physical
    // slap without needing the player to hold grip/trigger to "select"
    // the button. Any NearFarInteractor in the scene is treated as a hand.
    private void FindHands()
    {
        _hands.Clear();
        _lastHandPositions.Clear();

        NearFarInteractor[] interactors = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        foreach (NearFarInteractor interactor in interactors)
        {
            _hands.Add(interactor.transform);
            _lastHandPositions[interactor.transform] = interactor.transform.position;
        }
    }

    void Update()
    {
        CheckForSlap();
    }

    // Presses the button when a hand passes through it quickly, instead of
    // requiring the player to point at it and hold the select input.
    private void CheckForSlap()
    {
        if (_interactable == null || !_interactable.enabled)
            return;

        if (_isAnimating)
            return;

        if (Time.time - _lastSlapTime < slapCooldown)
            return;

        if (Time.deltaTime <= 0f)
            return;

        Vector3 buttonPosition = buttonTop != null ? buttonTop.position : transform.position;

        foreach (Transform hand in _hands)
        {
            if (hand == null)
                continue;

            Vector3 lastPosition = _lastHandPositions[hand];
            Vector3 currentPosition = hand.position;
            float speed = (currentPosition - lastPosition).magnitude / Time.deltaTime;
            _lastHandPositions[hand] = currentPosition;

            float distance = Vector3.Distance(currentPosition, buttonPosition);

            if (distance <= slapRadius && speed >= minSlapSpeed)
            {
                _lastSlapTime = Time.time;
                PressButton();
                break;
            }
        }
    }

    public void UnlockButton()
    {
        if (_interactable != null)
        {
            _interactable.enabled = true;
            Debug.Log("Button is now interactable!");
        }
    }

    public void PressButton()
    {
        Debug.Log("Pressed Button");

        if (_interactable != null)
        {
            _interactable.enabled = false; //Prevent double pressing
        }

        if (!_isAnimating)
        {
            // 1. Play the up/down animation
            StartCoroutine(AnimateButton());
            
        }
    }

    private IEnumerator AnimateButton()
    {
        _isAnimating = true;

        // Move Down
        float time = 0;
        while (time < 1f)
        {
            time += Time.deltaTime * pressSpeed;
            buttonTop.localPosition = Vector3.Lerp(_originalPosition, _pressedPosition, time);
            yield return null;
        }

        // Move Up
        time = 0;
        while (time < 1f)
        {
            time += Time.deltaTime * pressSpeed;
            buttonTop.localPosition = Vector3.Lerp(_pressedPosition, _originalPosition, time);
            yield return null;
        }

        // Ensure it snaps perfectly back to the start
        buttonTop.localPosition = _originalPosition;
        _isAnimating = false;
    }
}
