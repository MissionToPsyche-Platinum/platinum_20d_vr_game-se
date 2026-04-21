using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

public class play_video : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    [SerializeField] private UnityEvent onVideoStarted = new UnityEvent();
    [SerializeField] private UnityEvent onVideoCompleted = new UnityEvent();

    void Start()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= HandleVideoCompleted;
            _videoPlayer.loopPointReached += HandleVideoCompleted;
        }
    }

    private void OnDestroy()
    {
        if (_videoPlayer != null)
            _videoPlayer.loopPointReached -= HandleVideoCompleted;
    }

    public void StartVideo()
    {
        if (_videoPlayer == null)
            return;

        _videoPlayer.Play();
        onVideoStarted.Invoke();
    }

    private void HandleVideoCompleted(VideoPlayer source)
    {
        onVideoCompleted.Invoke();
    }
}
