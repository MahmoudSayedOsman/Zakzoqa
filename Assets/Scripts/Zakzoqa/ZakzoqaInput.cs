using UnityEngine;
using UnityEngine.InputSystem;

namespace Zakzoqa
{
    // What the player asked for, in the game's own terms (Decisions §3: a classic platformer). Every
    // transformation reads this and never a device: the on-screen buttons, a keyboard and a gamepad
    // are all IZakzoqaInputs, and no form changes when one is added.
    public struct InputFrame
    {
        // -1 left .. 1 right.
        public float Move;
        // Began this frame. Latched by ZakzoqaBody until the next physics step uses it.
        public bool JumpPressed;
        // Held down: a higher jump, and floating on helium on the way down.
        public bool JumpHeld;
        // The transformation's ability. The balloon's is the puff.
        public bool AbilityPressed;
        // A door, a lever, someone to talk to.
        public bool InteractPressed;
    }

    public interface IZakzoqaInput
    {
        InputFrame Read();
    }

    // Keyboard and gamepad: the editor, a PC, a controller on a phone. The on-screen buttons
    // (Decisions §3: two arrows on the left, jump and ability on the right) arrive with the UI and
    // feed the same frame.
    //
    // Keyboard: A/D or arrows to move, Space/W/Up to jump, J or X to puff, E to interact.
    // Gamepad: left stick or d-pad, South to jump, West to puff, North to interact.
    public sealed class DeviceInput : IZakzoqaInput
    {
        public InputFrame Read()
        {
            InputFrame frame = default;

            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) frame.Move -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) frame.Move += 1f;
                frame.JumpPressed |= k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame;
                frame.JumpHeld |= k.spaceKey.isPressed || k.wKey.isPressed || k.upArrowKey.isPressed;
                frame.AbilityPressed |= k.jKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame;
                frame.InteractPressed |= k.eKey.wasPressedThisFrame;
            }

            Gamepad g = Gamepad.current;
            if (g != null)
            {
                float x = g.leftStick.x.ReadValue() + g.dpad.x.ReadValue();
                if (Mathf.Abs(x) > 0.25f) frame.Move += x;
                frame.JumpPressed |= g.buttonSouth.wasPressedThisFrame;
                frame.JumpHeld |= g.buttonSouth.isPressed;
                frame.AbilityPressed |= g.buttonWest.wasPressedThisFrame;
                frame.InteractPressed |= g.buttonNorth.wasPressedThisFrame;
            }

            frame.Move = Mathf.Clamp(frame.Move, -1f, 1f);
            return frame;
        }
    }
}
