using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Plays a book's page video (BookContent.pageVideo) inside a window on one page face, looped and
    /// silent, while the book is held (TG-180: the Taters laser video on page 1 of the cat book).
    ///
    /// The page keeps its own material: while playing, the face is drawn from a render texture into which
    /// each new video frame is blitted with the page image around it (Resources/BookPageVideoComposite),
    /// set through a MaterialPropertyBlock, so the shared material asset is never touched. Released, the face goes back
    /// to its page image, whose window holds a still of the clip. Nothing here depends on the page mesh:
    /// the window is a rectangle of the page image.
    ///
    /// BookContentApplier adds this at Start in play mode for any content with a page video.
    /// </summary>
    public class BookPageVideo : MonoBehaviour
    {
        private const string MainTexture = "_MainTexture";
        private const string CompositeShader = "BookPageVideoComposite";   // in Resources
        // the composite is half the page images' 2048 px width: sharp enough at reading distance and a
        // quarter of the memory and mip work per video frame on the Quest
        private const int CompositeWidth = 1024;

        private BookContent _content;
        private PsycheGrabbable _grabbable;
        private Renderer _face;
        private int _materialIndex = -1;
        private Texture _pageImage;
        private VideoPlayer _player;
        private RenderTexture _composite, _videoFrame;
        private Material _blend;
        private MaterialPropertyBlock _block;
        private long _lastFrame = -1;
        private bool _playing;

        /// <summary>Finds the video face and the book's grab; disables itself when either is missing.</summary>
        public void Init(BookContent content)
        {
            _content = content;
            if (!FindFace() || !FindGrabbable())
            {
                enabled = false;
                return;
            }
            var shader = Resources.Load<Shader>(CompositeShader);
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning($"[BookPageVideo] {name}: composite shader missing or unsupported; the page shows its still.", this);
                enabled = false;
                return;
            }
            _blend = new Material(shader) { name = "BookPageVideo.Composite" };
            Rect w = content.videoWindow;
            _blend.SetVector("_Window", new Vector4(w.x, w.y, w.width, w.height));
            _grabbable.selectEntered.AddListener(OnGrabbed);
            _grabbable.selectExited.AddListener(OnReleased);
            if (_grabbable.isSelected) StartVideo();
        }

        private bool FindFace()
        {
            var material = _content.VideoFaceMaterial;
            var page = BookContentApplier.FindChildRecursive(transform, $"Page_{_content.videoPage:000}");
            _face = page != null ? page.GetComponent<Renderer>() : null;
            if (material == null || _face == null)
            {
                Debug.LogWarning($"[BookPageVideo] {name}: no page face for the video of '{_content.name}'.", this);
                return false;
            }
            var mats = _face.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == material) _materialIndex = i;
            _pageImage = material.GetTexture(MainTexture);
            if (_materialIndex < 0 || _pageImage == null)
            {
                Debug.LogWarning($"[BookPageVideo] {name}: page {_content.videoPage} does not carry '{material.name}' or it has no image.", this);
                return false;
            }
            return true;
        }

        private bool FindGrabbable()
        {
            // the book's grab sits on its SpinePivot child, under the root that carries the content
            _grabbable = GetComponentInChildren<PsycheGrabbable>(true);
            if (_grabbable == null) _grabbable = GetComponentInParent<PsycheGrabbable>();
            if (_grabbable == null)
                Debug.LogWarning($"[BookPageVideo] {name}: no PsycheGrabbable, so the video never starts.", this);
            return _grabbable != null;
        }

        private void OnGrabbed(SelectEnterEventArgs args) => StartVideo();

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!_grabbable.isSelected) StopVideo();   // still held by the other hand: keep playing
        }

        private void StartVideo()
        {
            if (_playing) return;
            EnsureTextures();
            Graphics.Blit(_pageImage, _composite);   // the page as printed, still in the window
            _composite.GenerateMips();
            _block ??= new MaterialPropertyBlock();
            _block.SetTexture(MainTexture, _composite);
            _face.SetPropertyBlock(_block, _materialIndex);
            _lastFrame = -1;
            _player.Play();
            _playing = true;
        }

        private void StopVideo()
        {
            if (!_playing) return;
            _player.Stop();
            _block.Clear();
            _face.SetPropertyBlock(_block, _materialIndex);   // back to the page image
            _playing = false;
        }

        private void EnsureTextures()
        {
            if (_composite != null) return;
            int height = Mathf.RoundToInt(CompositeWidth * (float)_pageImage.height / _pageImage.width);
            var page = new RenderTextureDescriptor(CompositeWidth, height, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = true, useMipMap = true, autoGenerateMips = false,
            };
            _composite = new RenderTexture(page) { name = "BookPageVideo.Page", filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            _composite.Create();

            var frame = page;
            frame.width = (int)_content.pageVideo.width;
            frame.height = (int)_content.pageVideo.height;
            frame.useMipMap = false;
            _videoFrame = new RenderTexture(frame) { name = "BookPageVideo.Frame" };
            _videoFrame.Create();
            _blend.SetTexture("_VideoTex", _videoFrame);

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = _content.pageVideo;
            _player.isLooping = true;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _videoFrame;
            _player.aspectRatio = VideoAspectRatio.Stretch;
            _player.skipOnDrop = true;
        }

        private void Update()
        {
            if (!_playing || !_player.isPlaying || _player.frame == _lastFrame) return;
            _lastFrame = _player.frame;
            Graphics.Blit(_pageImage, _composite, _blend);
            _composite.GenerateMips();
        }

        private void OnDisable() => StopVideo();

        private void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.selectEntered.RemoveListener(OnGrabbed);
                _grabbable.selectExited.RemoveListener(OnReleased);
            }
            if (_composite != null) _composite.Release();
            if (_videoFrame != null) _videoFrame.Release();
            Destroy(_composite);
            Destroy(_videoFrame);
            Destroy(_blend);
        }
    }
}
