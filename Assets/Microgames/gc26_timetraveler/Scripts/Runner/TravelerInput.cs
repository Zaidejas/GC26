using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Timetraveler
{
    /// <summary>
    /// One-thumb controls, read once per frame.
    /// Touch / mouse: tap = jump (hold = higher), swipe down = slide (fast fall in the air).
    /// Keyboard (PC testing): Space / Up / W = jump, Down / S = slide.
    /// </summary>
    public class TravelerInput
    {
        /// <summary>Swipe distance as a share of screen height.</summary>
        const float SwipeThreshold = 0.06f;
        /// <summary>A press is treated as a jump unless it turns into a swipe within this time.</summary>
        const float TapDecisionTime = 0.08f;

        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SlidePressed { get; private set; }
        public bool AnyPressed { get; private set; }

        bool _pointerDown;
        bool _pointerDecided;
        bool _pointerIsJump;
        Vector2 _pointerStart;
        float _pointerStartTime;

        public void Update()
        {
            JumpPressed = false;
            SlidePressed = false;
            AnyPressed = false;

            ReadKeyboard(out var keyJumpHeld);
            ReadPointer();

            JumpHeld = keyJumpHeld || (_pointerDown && _pointerIsJump);
        }

        void ReadKeyboard(out bool jumpHeld)
        {
            jumpHeld = false;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            {
                JumpPressed = true;
                AnyPressed = true;
            }

            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
            {
                SlidePressed = true;
                AnyPressed = true;
            }

            jumpHeld = kb.spaceKey.isPressed || kb.upArrowKey.isPressed || kb.wKey.isPressed;
        }

        void ReadPointer()
        {
            // Pointer.current covers touchscreen, mouse and pen (and the Device Simulator).
            var pointer = Pointer.current;
            if (pointer == null) return;

            var pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pointerDown = true;
                _pointerDecided = false;
                _pointerIsJump = false;
                _pointerStart = pos;
                _pointerStartTime = Time.unscaledTime;
                AnyPressed = true;
            }

            if (_pointerDown && !_pointerDecided)
            {
                var dy = (pos.y - _pointerStart.y) / Mathf.Max(1f, Screen.height);
                if (dy < -SwipeThreshold)
                {
                    SlidePressed = true;
                    _pointerDecided = true;
                }
                else if (Time.unscaledTime - _pointerStartTime >= TapDecisionTime || pointer.press.wasReleasedThisFrame)
                {
                    JumpPressed = true;
                    _pointerIsJump = true;
                    _pointerDecided = true;
                }
            }

            if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed)
            {
                _pointerDown = false;
                _pointerIsJump = false;
            }
        }
    }
}
