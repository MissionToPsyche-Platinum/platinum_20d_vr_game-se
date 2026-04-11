using System.Collections.Generic;
using TMPro;
using UnityEngine;
import

public class InstructionTextManager : MonoBehaviour
{
    public TMP_Text instructionText;
    public Dictionary<string, string> textItem = new Dictionary<string, string>();
    public string[] description = new string[4];
    public string[] title = new string[4];
    public int currSize = 0;
    public int currPosition = 0;

    public void Start()
    {
        this.SetText("Please place the PSYCHE Bus (Large Black Box) on top of the Cylinder to begin");
        textItem.Add("PSYCHE_Antenna", "Antenna");
        textItem.Add("PSYCHE_Right_Solar", "Solar Panel");
        textItem.Add("PSYCHE_Left_Solar", "Solar Panel");
        textItem.Add("PSYCHE_Bus", "Spacecraft Bus");
    }

    public void SetText(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }

    public void addItem(SnappableObject snapObject)
    {
        if (snapObject = null)) {
            return;
        }

        const toAddTitle = textItem.TryGetValue(snapObject.objectTitle);
        const toAddDesc = textItem.TryGetValue(snapObject.objectDescription);
        description[currSize] = toAddDesc;
        title[currSize] = toAddTitle;
        currSize++;


    }

    public void next()
    {

    }
}
