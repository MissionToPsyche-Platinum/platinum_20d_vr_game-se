using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Plays or pauses a VideoPlayer when the 3D button is clicked.
    /// Works with mouse click (OnMouseDown) and VR interaction.
    ///
    /// Setup:
    ///   1. Attach to a 3D object with a Collider (e.g. a cube)
    ///   2. Assign a VideoClip in the Inspector
    ///   3. Assign the Target Renderer (the screen that shows the video)
    ///   4. Click the red button to play, click again to pause
    /// </summary>
    public class VideoPlayerButton : MonoBehaviour
    {
        [Header("Video Settings")]
        [Tooltip("Video clip to play. Assign in Inspector.")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("Should the video loop?")]
        [SerializeField] private bool loop = true;

        [Header("Render Target")]
        [Tooltip("The renderer whose material will display the video.")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Button Feedback")]
        [Tooltip("Color of the button when video is stopped.")]
        [SerializeField] private Color stoppedColor = Color.red;

        [Tooltip("Color of the button when video is playing.")]
        [SerializeField] private Color playingColor = Color.green;

        [Header("Events")]
        [SerializeField] private UnityEvent onVideoStarted = new UnityEvent();
        [SerializeField] private UnityEvent onVideoPaused = new UnityEvent();
        [SerializeField] private UnityEvent onVideoCompleted = new UnityEvent();

        private VideoPlayer _videoPlayer;
        private Renderer _buttonRenderer;
        private MaterialPropertyBlock _propBlock;
        private Vector3 _originalScale;
        private bool _isPlaying;

        private void Awake()
        {
            _buttonRenderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            _originalScale = transform.localScale;

            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        private void Start()
        {
            SetupVideoPlayer();
            UpdateButtonColor();
        }

        private void OnDestroy()
        {
            if (_videoPlayer != null)
                _videoPlayer.loopPointReached -= HandleVideoCompleted;
        }

        private void SetupVideoPlayer()
        {
            if (videoClip != null)
                _videoPlayer.clip = videoClip;

            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = loop;
            _videoPlayer.renderMode = VideoRenderMode.MaterialOverride;

            if (targetRenderer != null)
                _videoPlayer.targetMaterialRenderer = targetRenderer;

            _videoPlayer.loopPointReached -= HandleVideoCompleted;
            _videoPlayer.loopPointReached += HandleVideoCompleted;
            _videoPlayer.Prepare();
        }

        /// <summary>
        /// Mouse click on the 3D button triggers video toggle.
        /// </summary>
        private void OnMouseDown()
        {
            ToggleVideo();
            StartCoroutine(PressAnimation());
        }

        /// <summary>
        /// Public method - can be called from UI Button OnClick or XR events.
        /// </summary>
        public void ToggleVideo()
        {
            if (_isPlaying)
            {
                _videoPlayer.Pause();
                _isPlaying = false;
                onVideoPaused.Invoke();
            }
            else
            {
                _videoPlayer.Play();
                _isPlaying = true;
                onVideoStarted.Invoke();
            }

            UpdateButtonColor();
        }

        private void HandleVideoCompleted(VideoPlayer source)
        {
            _isPlaying = false;
            UpdateButtonColor();
            onVideoCompleted.Invoke();
        }

        private void UpdateButtonColor()
        {
            if (_buttonRenderer == null) return;

            _buttonRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", _isPlaying ? playingColor : stoppedColor);
            _buttonRenderer.SetPropertyBlock(_propBlock);
        }

        private System.Collections.IEnumerator PressAnimation()
        {
            transform.localScale = _originalScale * 0.85f;
            yield return new WaitForSeconds(0.15f);
            transform.localScale = _originalScale;
        }
    }
}
