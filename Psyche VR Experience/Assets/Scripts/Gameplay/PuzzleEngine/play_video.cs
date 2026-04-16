using UnityEngine;
using UnityEngine.Video;

public class play_video : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _videoPlayer = GetComponent<VideoPlayer>();

        
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void StartVideo()
    {
        _videoPlayer.Play();
    }
}
