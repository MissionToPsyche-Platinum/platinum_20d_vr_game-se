
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ResetPieces : MonoBehaviour
{
    private SnappableObject[] pieces;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
    }

    private void Start()
    {
        pieces = FindObjectsByType<SnappableObject>(FindObjectsSortMode.None);
        Debug.Log("Found " + pieces.Length + " snappable objects.");
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
        foreach (SnappableObject piece in pieces)
        {
            if (piece != null && !piece.isSnapped)
            {
                piece.ResetPiece();
            }
        }
    }
}
