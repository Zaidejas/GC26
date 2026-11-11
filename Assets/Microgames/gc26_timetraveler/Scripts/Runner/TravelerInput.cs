using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Timetraveler
{
    /// <summary>
    /// One-thumb controls, read once per frame.
    /// Touch / mouse: tap = jump (hold = higher), swipe down = slide, swipe up = time slow,
    /// swipe left or right = switch world.
    /// Keyboard (PC testing): Space / W / Up = jump, S / Down = slide (hold to keep sliding),
    /// E or Shift = time slow, Q / Left / Right = switch world.
    /// </summary>
    public class TravelerInput
    {
        /// <summary>Swipe distance as a share of screen height.</summary>
        const float SwipeThreshold = 0.04f;
        /// <summary>A press is treated as a jump unless it turns into a swipe within this time
        /// (releasing earlier jumps at once).</summary>
        const float TapDecisionTime = 0.1f;

        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SlidePressed { get; private set; }
        /// <summary>Slide key still down, or the finger that swiped down still on the screen.</summary>
        public bool SlideHeld { get; private set; }
        public bool SlowPressed { get; private set; }
        public bool SwitchPressed { get; private set; }
        public bool AnyPressed { get; private set; }

        /// <summary>Screen area (Input System coordinates, origin bottom-left) where presses are ignored,
        /// e.g. the pause button, so pressing it does not also make the traveler jump.</summary>
        public Rect IgnoreRect { get; set; }

        public bool PausePressed { get; private set; }

        bool _pointerDown;
        bool _pointerDecided;
        bool _pointerIsJump;
        bool _pointerIsSlide;
        Vector2 _pointerStart;
        float _pointerStartTime;

        public void Update()
        {
            JumpPressed = false;
            SlidePressed = false;
            SlowPressed = false;
            SwitchPressed = false;
            PausePressed = false;
            AnyPressed = false;

            ReadKeyboard(out var keyJumpHeld, out var keySlideHeld);
            ReadPointer();

            JumpHeld = keyJumpHeld || (_pointerDown && _pointerIsJump);
            SlideHeld = keySlideHeld || (_pointerDown && _pointerIsSlide);
        }

        void ReadKeyboard(out bool jumpHeld, out bool slideHeld)
        {
            jumpHeld = false;
            slideHeld = false;
            var kb = Keyboard.current;
            if (kb == null) return;
            slideHeld = kb.downArrowKey.isPressed || kb.sKey.isPressed;
            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) PausePressed = true;

            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                JumpPressed = AnyPressed = true;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                SlidePressed = AnyPressed = true;
            if (kb.eKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                SlowPressed = AnyPressed = true;
            if (kb.qKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                SwitchPressed = AnyPressed = true;

            jumpHeld = kb.spaceKey.isPressed || kb.upArrowKey.isPressed || kb.wKey.isPressed;
        }

        void ReadPointer()
        {
            // Pointer.current covers touchscreen, mouse and pen (and the Device Simulator).
            var pointer = Pointer.current;
            if (pointer == null) return;

            var pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame && IgnoreRect.Contains(pos)) return;

            if (pointer.press.wasPressedThisFrame)
            {
                _pointerDown = true;
                _pointerDecided = false;
                _pointerIsJump = false;
                _pointerIsSlide = false;
                _pointerStart = pos;
                _pointerStartTime = Time.unscaledTime;
                AnyPressed = true;
            }

            if (_pointerDown && !_pointerDecided)
            {
                var delta = (pos - _pointerStart) / Mathf.Max(1f, Screen.height);
                if (delta.magnitude > SwipeThreshold)
                {
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) SwitchPressed = true;
                    else if (delta.y < 0f) SlidePressed = _pointerIsSlide = true;
                    else SlowPressed = true;
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
                _pointerIsSlide = false;
            }
        }
    }
}
