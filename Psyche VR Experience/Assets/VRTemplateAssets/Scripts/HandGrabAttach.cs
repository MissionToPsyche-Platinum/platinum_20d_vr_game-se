using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandGrabAttach : XRGrabInteractable
{
    public Transform leftHandAttach;
    public Transform rightHandAttach;

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        string hand = interactor.transform.parent.name;

        if (hand == "Left Controller")
        {
            return leftHandAttach;
        }

        if (hand == "Right Controller")
        {
            return rightHandAttach;
        }

        return base.GetAttachTransform(interactor);
    }
}
