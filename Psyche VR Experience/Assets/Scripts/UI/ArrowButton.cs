using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ArrowButton : MonoBehaviour
{
    public InstructionTextManager manager;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnClicked);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnClicked);
        }
    }

    private void OnClicked(SelectEnterEventArgs args)
    {
        if (manager == null) return;

        if (CompareTag("next_button"))
        {
            manager.Next();
        }
        else if (CompareTag("previous_button"))
        {
            manager.Previous();
        }
    }
}
