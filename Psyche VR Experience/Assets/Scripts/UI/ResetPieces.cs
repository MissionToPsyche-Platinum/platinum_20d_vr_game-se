
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using PsycheVR.Data;

public class ResetPieces : MonoBehaviour, ISessionInteractable
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

    private void OnClicked(SelectEnterEventArgs args) => Press();

    /// <summary>Sends every unsnapped piece home; called by a select or, on the event board, by a slap (SlapKey).</summary>
    public void Press()
    {
        if (pieces == null) return;
        SessionEvents.Interaction("puzzle_reset", this);
        foreach (SnappableObject piece in pieces)
        {
            if (piece != null && !piece.isSnapped)
            {
                piece.ResetPiece();
            }
        }
    }
}
