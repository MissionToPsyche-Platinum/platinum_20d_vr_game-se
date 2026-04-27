using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ButtonController : MonoBehaviour
{
    public Transform buttonTop;
    public float pressDistance = 0.01f;
    public float pressSpeed = 5f;

    private Vector3 _originalPosition;
    private Vector3 _pressedPosition;
    private bool _isAnimating = false;
    private XRSimpleInteractable _interactable;

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
