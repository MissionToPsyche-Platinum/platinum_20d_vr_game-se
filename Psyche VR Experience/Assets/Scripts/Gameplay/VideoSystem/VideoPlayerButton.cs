using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Plays or pauses a VideoPlayer when the user interacts with this object.
    /// Works with XR interaction (grab/poke) and keyboard input for testing.
    ///
    /// Setup:
    ///   1. Attach to a 3D object (e.g. a cube acting as a button)
    ///   2. Assign a VideoClip in the Inspector
    ///   3. Assign the Target Renderer (the screen quad/plane that shows the video)
    ///   4. Press P or interact in VR to toggle play/pause
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class VideoPlayerButton : MonoBehaviour
    {
        [Header("Video Settings")]
        [Tooltip("Video clip to play. Assign in Inspector.")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("Should the video loop?")]
        [SerializeField] private bool loop = true;

        [Header("Render Target")]
        [Tooltip("The renderer whose material will display the video (e.g. a Quad).")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Button Feedback")]
        [Tooltip("Color of the button when video is stopped.")]
        [SerializeField] private Color stoppedColor = Color.red;

        [Tooltip("Color of the button when video is playing.")]
        [SerializeField] private Color playingColor = Color.green;

        [Tooltip("How much the button scales down when pressed.")]
        [SerializeField] private float pressScale = 0.85f;

        [Header("Debug")]
        [Tooltip("Keyboard key to toggle play/pause for testing without VR (default: P).")]
        [SerializeField] private Key debugKey = Key.P;

        private XRSimpleInteractable _interactable;
        private VideoPlayer _videoPlayer;
        private Renderer _buttonRenderer;
        private MaterialPropertyBlock _propBlock;
        private Vector3 _originalScale;
        private bool _isPlaying;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _buttonRenderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            _originalScale = transform.localScale;

            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnButtonPressed);
            _interactable.hoverEntered.AddListener(OnHoverEnter);
            _interactable.hoverExited.AddListener(OnHoverExit);

            SetupVideoPlayer();
            UpdateButtonColor();
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnButtonPressed);
            _interactable.hoverEntered.RemoveListener(OnHoverEnter);
            _interactable.hoverExited.RemoveListener(OnHoverExit);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[debugKey].wasPressedThisFrame)
            {
                ToggleVideo();
                AnimatePress();
            }
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

            _videoPlayer.Prepare();
        }

        private void OnButtonPressed(SelectEnterEventArgs args)
        {
            ToggleVideo();
            AnimatePress();
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            transform.localScale = _originalScale * 1.1f;
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            transform.localScale = _originalScale;
        }

        private void ToggleVideo()
        {
            if (_isPlaying)
            {
                _videoPlayer.Pause();
                _isPlaying = false;
            }
            else
            {
                _videoPlayer.Play();
                _isPlaying = true;
            }

            UpdateButtonColor();
        }

        private void UpdateButtonColor()
        {
            if (_buttonRenderer == null) return;

            _buttonRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", _isPlaying ? playingColor : stoppedColor);
            _buttonRenderer.SetPropertyBlock(_propBlock);
        }

        private void AnimatePress()
        {
            StopAllCoroutines();
            StartCoroutine(PressAnimation());
        }

        private System.Collections.IEnumerator PressAnimation()
        {
            transform.localScale = _originalScale * pressScale;
            yield return new WaitForSeconds(0.15f);
            transform.localScale = _originalScale;
        }
    }
}
