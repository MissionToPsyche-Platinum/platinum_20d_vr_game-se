using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using PsycheVR.Audio;
using PsycheVR.Data;

public class ArrowButton : MonoBehaviour, ISessionInteractable
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
        // a slap reaches Press() through SlapKey, which plays its own press sound
        InteractionAudio.Play(InteractionSound.ButtonPress, transform.position);
        Press();
    }

    /// <summary>Pages the instructions; called by a select or, on the event board, by a slap (SlapKey).</summary>
    public void Press()
    {
        if (manager == null) return;
        SessionEvents.Interaction("instructions_paged", this, "direction=" + (CompareTag("next_button") ? "next" : "back"));

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
