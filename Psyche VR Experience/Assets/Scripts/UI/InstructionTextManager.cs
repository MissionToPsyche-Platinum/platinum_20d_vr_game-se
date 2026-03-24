using TMPro;
using UnityEngine;

public class InstructionTextManager : MonoBehaviour
{
    public TMP_Text instructionText;

    public void Start()
    {
        this.SetText("Please Place the Gold Cube to the left of the transparent Gray Box");
    }
    public void SetText(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }
}
