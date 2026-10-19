using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PsycheVR.Gameplay;

/// <summary>
/// The puzzle info board's pages: one per kind of piece placed (the two solar arrays share one), paged with
/// Next and Previous. With a <see cref="screen"/> (the Event room board, TG-295) the pages show on the
/// designed display with a picture of each piece; without one the two plain text fields are used.
/// </summary>
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
    public GameObject nextButton;
    public GameObject previousButton;
    [Tooltip("Designed display for the pages; leave empty to use the plain text fields.")]
    public PuzzleBoardScreen screen;

    private readonly Sprite[] picture = new Sprite[4];
    private readonly int[] entry = { -1, -1, -1, -1 };

    public void Start()
    {
        if (screen != null)
        {
            // the screen replaces the plain fields
            if (instructionText != null) instructionText.gameObject.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(false);
            screen.ShowStart();
        }
        else
        {
            SetText("Place the Psyche bus (the large black box) on top of the cylinder to begin.");
            SetTitle("Instruction");
        }
        UpdateButtonState();
    }

    /// <summary>Shows the page at <see cref="currPosition"/> on whichever display is in use.</summary>
    private void ShowCurrent()
    {
        SetTitle(title[currPosition]);
        SetText(description[currPosition]);
        if (screen == null) return;
        var found = new int[currSize];
        for (int i = 0; i < currSize; i++) found[i] = entry[i];
        screen.ShowPage(title[currPosition], description[currPosition], picture[currPosition],
                        currPosition, currSize, entry[currPosition], found);
    }

    private void UpdateButtonState()
    {
        bool shouldShow = currSize > 1;

        if (nextButton != null)
            nextButton.SetActive(shouldShow);

        if (previousButton != null)
            previousButton.SetActive(shouldShow);
    }

    public void SetText(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }

    public void SetTitle(string newTitle)
    {
        if (titleText != null)
        {
            titleText.text = newTitle;
        }
    }

    public void AddItem(SnappableObject snapObject)
    {
        if (snapObject == null) return;

        if (snapObject.CompareTag("PSYCHE_Right_Solar") || snapObject.CompareTag("PSYCHE_Left_Solar"))
        {
            if (solarAdded)
            {
                currPosition = firstSolarPosition;
                ShowCurrent();
                UpdateButtonState();
                return;
            }
            else
            {
                firstSolarPosition = currSize;
                solarAdded = true;
            }
        }

        if (currSize >= description.Length) return;

        string toAddDesc = snapObject.objectDescription;
        string toAddTitle = snapObject.objectTitle;

        title[currSize] = toAddTitle;
        description[currSize] = toAddDesc;
        if (screen != null)
        {
            picture[currSize] = screen.PictureFor(snapObject.tag);
            entry[currSize] = screen.IndexOf(snapObject.tag);
        }

        currSize++;
        currPosition = currSize - 1;

        ShowCurrent();
        UpdateButtonState();
    }

    public void Next()
    {
        if (currSize == 0) return;

        currPosition++;

        if (currPosition >= currSize)
        {
            currPosition = 0;
        }

        ShowCurrent();
    }

    public void Previous()
    {
        if (currSize == 0) return;

        currPosition--;

        if (currPosition < 0)
        {
            currPosition = currSize - 1;
        }

        ShowCurrent();
    }
}
