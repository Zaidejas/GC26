using GC26.Tanki.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = GC26.Tanki.Rules.PlayerInput;
using UnityEngine.InputSystem.Controls;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// Thumb controls: a fixed stick at the bottom left and a fire button at the bottom right.
    /// A touch on the left half steers relative to the stick's centre, a touch on the right
    /// half fires. The mouse acts as one finger and the keyboard works too, for the Unity editor.
    /// </summary>
    public sealed class Controls
    {
        const float DeadZoneShare = 0.3f;
        const float StickRadiusShare = 0.165f;

        public bool FireHeld { get; private set; }
        public bool SuperHeld { get; private set; }
        public Dir? Direction { get; private set; }
        public Vector2 KnobOffset { get; private set; }
        public float StickRadius => Mathf.Min(Screen.width, Screen.height) * StickRadiusShare;

        // Screen coordinates, y up.
        public Vector2 StickCentre => new Vector2(Screen.safeArea.xMin + StickRadius * 1.25f, Screen.safeArea.yMin + StickRadius * 1.15f);
        public Vector2 FireCentre => new Vector2(Screen.safeArea.xMax - StickRadius * 1.25f, Screen.safeArea.yMin + StickRadius * 1.15f);

        // The reactor strike sits low between the stick and the fire button, below the map.
        public float SuperRadius => StickRadius * 0.5f;
        public Vector2 SuperCentre => new Vector2((StickCentre.x + FireCentre.x) / 2f, Screen.safeArea.yMin + StickRadius * 0.75f);

        int stickFinger = -1;

        public PlayerInput Read()
        {
            var input = new PlayerInput();
            FireHeld = false;
            SuperHeld = false;

            bool stickSeen = false;
            int touches = 0;
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (TouchControl touch in screen.touches)
                {
                    UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                    bool ended = phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                    if (ended && !touch.press.wasReleasedThisFrame) continue;
                    touches++;
                    HandlePointer(touch.touchId.ReadValue(), touch.position.ReadValue(),
                        phase == UnityEngine.InputSystem.TouchPhase.Began || touch.press.wasPressedThisFrame, ended, ref stickSeen);
                }
            }
            Mouse mouse = Mouse.current;
            if (touches == 0 && mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
            {
                HandlePointer(int.MaxValue, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, ref stickSeen);
            }
            if (!stickSeen)
            {
                stickFinger = -1;
                Direction = null;
                KnobOffset = Vector2.zero;
            }

            input.Move = Direction;
            Dir? keys = KeyboardDirection();
            if (keys != null) input.Move = keys;
            Keyboard keyboard = Keyboard.current;
            input.Fire = FireHeld || (keyboard != null && keyboard.spaceKey.isPressed);
            input.Super = SuperHeld || (keyboard != null && (keyboard.eKey.isPressed || keyboard.leftShiftKey.isPressed));
            return input;
        }

        void HandlePointer(int finger, Vector2 position, bool began, bool ended, ref bool stickSeen)
        {
            if (finger == stickFinger)
            {
                if (ended) return;
                MoveStick(position);
                stickSeen = true;
                return;
            }

            // The strike button straddles the middle, so it is checked before the halves.
            if (!ended && Vector2.Distance(position, SuperCentre) <= SuperRadius * 1.25f)
            {
                SuperHeld = true;
                return;
            }

            bool leftSide = position.x < Screen.width * 0.5f;
            if (began && leftSide && stickFinger < 0 && !ended)
            {
                stickFinger = finger;
                MoveStick(position);
                stickSeen = true;
            }
            else if (!leftSide && !ended)
            {
                FireHeld = true;
            }
        }

        void MoveStick(Vector2 position)
        {
            Vector2 offset = position - StickCentre;
            float reach = StickRadius * 0.6f;
            KnobOffset = Vector2.ClampMagnitude(offset, reach);

            if (offset.magnitude < StickRadius * DeadZoneShare) Direction = null;
            else if (Mathf.Abs(offset.x) > Mathf.Abs(offset.y)) Direction = offset.x > 0f ? Dir.Right : Dir.Left;
            else Direction = offset.y > 0f ? Dir.Up : Dir.Down;
        }

        static Dir? KeyboardDirection()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null) return null;
            if (keys.upArrowKey.isPressed || keys.wKey.isPressed) return Dir.Up;
            if (keys.downArrowKey.isPressed || keys.sKey.isPressed) return Dir.Down;
            if (keys.leftArrowKey.isPressed || keys.aKey.isPressed) return Dir.Left;
            if (keys.rightArrowKey.isPressed || keys.dKey.isPressed) return Dir.Right;
            return null;
        }
    }
}
