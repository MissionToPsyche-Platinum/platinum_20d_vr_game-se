using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Plays or pauses a VideoPlayer when the user interacts with this object.
    /// Works with XR interaction (hover/grab) and keyboard input for testing.
    /// Attach to a 3D object (e.g. a button or panel) with an XRSimpleInteractable.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class VideoPlayerButton : MonoBehaviour
    {
        [Header("Video Settings")]
        [Tooltip("The VideoPlayer component to control. If null, searches this GameObject.")]
        [SerializeField] private VideoPlayer videoPlayer;

        [Tooltip("Video clip to play. Assign in Inspector.")]
        [SerializeField] private VideoClip videoClip;

        [Header("Render Target")]
        [Tooltip("The renderer whose material will display the video. If null, searches this GameObject.")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Debug")]
        [Tooltip("Keyboard key to toggle play/pause for testing without VR headset.")]
        [SerializeField] private KeyCode debugKey = KeyCode.P;

        private XRSimpleInteractable _interactable;
        private bool _isPlaying;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();

            if (videoPlayer == null)
                videoPlayer = GetComponent<VideoPlayer>();

            if (videoPlayer == null)
                videoPlayer = gameObject.AddComponent<VideoPlayer>();

            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnButtonPressed);
            SetupVideoPlayer();
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnButtonPressed);
        }

        private void Update()
        {
            if (Input.GetKeyDown(debugKey))
                ToggleVideo();
        }

        private void SetupVideoPlayer()
        {
            if (videoClip != null)
                videoPlayer.clip = videoClip;

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;

            if (targetRenderer != null)
                videoPlayer.targetMaterialRenderer = targetRenderer;

            videoPlayer.Prepare();
        }

        private void OnButtonPressed(SelectEnterEventArgs args)
        {
            ToggleVideo();
        }

        private void ToggleVideo()
        {
            if (_isPlaying)
            {
                videoPlayer.Pause();
                _isPlaying = false;
            }
            else
            {
                videoPlayer.Play();
                _isPlaying = true;
            }
        }
    }
}
