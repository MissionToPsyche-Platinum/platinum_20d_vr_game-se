using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Hides the book's spine mesh while the book is held and shows it again on release.
    /// The hover highlight that used to live here is now GrabGlow's teal tint, which
    /// PsycheGrabbable adds to the book like every other grabbable.
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    public class BookHighlight : MonoBehaviour
    {
        private PsycheGrabbable _grabbable;
        private Renderer _spineRenderer;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();

            var spineTransform = transform.Find("SpineMesh");
            if (spineTransform != null)
                _spineRenderer = spineTransform.GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            _grabbable.selectEntered.AddListener(OnSelectEntered);
            _grabbable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            _grabbable.selectEntered.RemoveListener(OnSelectEntered);
            _grabbable.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (_spineRenderer != null)
                _spineRenderer.enabled = false;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (_spineRenderer != null && !_grabbable.isSelected)
                _spineRenderer.enabled = true;
        }
    }
}
