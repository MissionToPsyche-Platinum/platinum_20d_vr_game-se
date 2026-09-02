
using UnityEngine;

public class reposition : MonoBehaviour
{
    //GameState gameState; When we're ready to add full game state will add here and will be what determines whether the canvas should be in
    //Tooltip mode (bottom) or info mode (middle) 
    public bool isTooltip;
    public bool isInfo;
    private RectTransform rectTransform;
    public float bottomY;
    public float middleY;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        if(isInfo == true && isTooltip == false)
        {
            MoveToMiddle();
        } else
        {
            if(isTooltip == true && isInfo == false)
            {
                MoveToBottom();
            }
        }
    }

    public void MoveToBottom()
    {
        Vector3 position = rectTransform.localPosition;
        position.y = bottomY;
        rectTransform.localPosition = position;
    }

    public void MoveToMiddle()
    {
        Vector3 position = rectTransform.localPosition;
        position.y = middleY;
        rectTransform.localPosition = position;
        
    }
}
