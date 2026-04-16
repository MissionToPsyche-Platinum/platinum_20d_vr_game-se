using TMPro;
using UnityEngine;

public class InstructionTextManager : MonoBehaviour
{
    public TMP_Text instructionText;

    public void Start()
    {
        this.SetText("Please place the PSYCHE Bus (Large Black Box) on top of the Cylinder to begin");
    }
    public void SetText(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }
}
