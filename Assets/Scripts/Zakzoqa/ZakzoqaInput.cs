using UnityEngine;
using UnityEngine.InputSystem;

namespace Zakzoqa
{
    // What the player asked for this frame, in the game's own terms. Every transformation reads
    // this and never a device: a new control scheme (a gamepad, tilt one day) is a new IZakzoqaInput,
    // and no form changes.
    public struct InputFrame
    {
        // A tap or press began this frame.
        public bool Press;
        // The press came from the finger (or mouse), so PointerWorld says where. False for a key.
        public bool PointerPress;
        // Where, in world space. Meaningful only when PointerPress or Held.
        public Vector2 PointerWorld;
        public bool Held;
        // -1..1 from keys: left and right. Zero on a touch screen.
        public float Horizontal;
    }

    public interface IZakzoqaInput
    {
        InputFrame Read();
    }

    // Touch on a phone, mouse or keyboard in the editor (Decisions §1, row 10: the Input System).
    //
    // ONE FINGER (Decisions §3): a tap anywhere is a press at that point; the form decides what a
    // press means. The keyboard stands in for the finger: Space is a tap straight above Zakzoqa,
    // and the arrows lean it.
    public sealed class DeviceInput : IZakzoqaInput
    {
        readonly Camera camera;

        public DeviceInput(Camera camera) { this.camera = camera; }

        public InputFrame Read()
        {
            InputFrame frame = default;

            Pointer pointer = Pointer.current;
            if (pointer != null && camera != null)
            {
                frame.Press = frame.PointerPress = pointer.press.wasPressedThisFrame;
                frame.Held = pointer.press.isPressed;
                Vector2 screen = pointer.position.ReadValue();
                frame.PointerWorld = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float h = 0f;
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) h -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) h += 1f;
                frame.Horizontal = h;
                if (keyboard.spaceKey.wasPressedThisFrame) frame.Press = true;
                if (keyboard.spaceKey.isPressed) frame.Held = true;
            }

            return frame;
        }
    }
}
