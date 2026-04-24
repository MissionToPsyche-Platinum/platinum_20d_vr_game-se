using UnityEngine;
using UnityEngine.InputSystem;
using PsycheVR.VR;

namespace PsycheVR.Test
{
    public class HarnessTeleportInput : MonoBehaviour
    {
        [SerializeField] private BlinkTeleporter teleporter;
        [SerializeField] private BlinkTeleportAnchor anchorA;
        [SerializeField] private BlinkTeleportAnchor anchorB;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                teleporter.TeleportTo(anchorA);
            if (keyboard.digit2Key.wasPressedThisFrame)
                teleporter.TeleportTo(anchorB);
        }
    }
}
