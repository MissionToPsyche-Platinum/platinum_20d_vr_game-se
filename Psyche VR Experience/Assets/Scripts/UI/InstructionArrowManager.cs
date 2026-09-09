using System.ComponentModel.Design;
using UnityEngine;

public class InstructionArrowManager : MonoBehaviour
{
    public GameObject busArrow;
    public GameObject antennaArrow;
    public GameObject leftSolarArrow;
    public GameObject rightSolarArrow;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        busArrow.SetActive(true);
        antennaArrow.SetActive(false);
        leftSolarArrow.SetActive(false);
        rightSolarArrow.SetActive(false);

    }

    void activateAll()
    {
        busArrow.SetActive(false);
        antennaArrow.SetActive(true);
        leftSolarArrow.SetActive(true);
        rightSolarArrow.SetActive(true);
    }

    void deactivateArrow(GameObject arrow)
    {
        if (arrow != null)
        {
            arrow.SetActive(false);
        }
    }

}
