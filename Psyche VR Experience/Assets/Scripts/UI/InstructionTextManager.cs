using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InstructionTextManager : MonoBehaviour
{
    public TMP_Text instructionText;
    public TMP_Text titleText;
    public string[] description = new string[4];
    public string[] title = new string[4];
    public int currSize = 0;
    public int currPosition = 0;
    public int firstSolarPosition = -1;
    public bool solarAdded = false;

    public void Start()
    {
        this.SetText("Please place the PSYCHE Bus (Large Black Box) on top of the Cylinder to begin");
        this.SetTitle("Instruction");
    }

    public void SetText(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }

    public void SetTitle(string title)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }
    }

    public void AddItem(SnappableObject snapObject)
    {
        if (snapObject == null) return;
        if (currSize >= description.Length) return;

        string objectTag = snapObject.tag;

        if (snapObject.CompareTag("PSYCHE_Right_Solar") || snapObject.CompareTag("PSYCHE_Left_Solar"))
        {
            if(solarAdded)
            {
                currPosition = firstSolarPosition;
                SetTitle(title[firstSolarPosition]);
                SetText(description[firstSolarPosition]);
                return;
            } else
            {
                firstSolarPosition = currSize;
                solarAdded = true;
            }
        }


        string toAddDesc = snapObject.objectDescription;
        string toAddTitle = snapObject.objectTitle;

        title[currSize] = toAddTitle;
        description[currSize] = toAddDesc;

        currSize++;

        currPosition = currSize - 1;

        SetTitle(title[currPosition]);
        SetText(description[currPosition]);
    }

    public void Next()
    {
        if (currSize == 0) return;

        currPosition++;

        if (currPosition >= currSize)
        {
            currPosition = 0;
        }

        SetTitle(title[currPosition]);
        SetText(description[currPosition]);
    }

    public void Previous()
    {
        if (currSize == 0) return;

        currPosition--;

        if (currPosition < 0)
        {
            currPosition = currSize - 1;
        }

        SetTitle(title[currPosition]);
        SetText(description[currPosition]);
    }
}
