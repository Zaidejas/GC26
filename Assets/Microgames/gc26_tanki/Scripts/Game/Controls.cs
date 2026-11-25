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
        const float DeadZoneShare = 0.25f;
        const float StickRadiusShare = 0.22f;

        public bool SuperHeld { get; private set; }
        public Dir? Direction { get; private set; }
        public Vector2 KnobOffset { get; private set; }
        public float StickRadius => Mathf.Min(Screen.width, Screen.height) * StickRadiusShare;

        // Screen coordinates, y up.
        // The stick and the fire button sit a little above the bottom edge, so a thumb pulling down stays on the screen.
        public Vector2 HomeCentre => new Vector2(Screen.safeArea.xMin + StickRadius * 1.25f, Screen.safeArea.yMin + StickRadius * 1.25f);
        /// <summary>The stick floats: it sits where the thumb landed in the lower left, and goes home when let go.</summary>
        public Vector2 StickCentre => floating ?? HomeCentre;
        Vector2? floating;

        // There is no fire button: the gun fires by itself (GameSim.TargetAhead). The tank's ability sits on the right.
        public float SuperRadius => StickRadius * 0.55f;
        public Vector2 SuperCentre => new Vector2(Screen.safeArea.xMax - StickRadius * 0.95f, Screen.safeArea.yMin + StickRadius * 1.25f);

        int stickFinger = -1;
#if UNITY_EDITOR_WIN
        int mouseUpFrames;
        bool stickGhost;
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
#endif
        readonly System.Collections.Generic.HashSet<int> ignored = new System.Collections.Generic.HashSet<int>();
        bool ignoreMouse;

        /// <summary>
        /// Lets go of everything: fingers and the mouse button that are down now are ignored until they lift.
        /// Called when the window loses focus and when a battle starts or ends, so a press that was lost
        /// on the way can't keep the tank driving.
        /// </summary>
        public void ReleaseAll()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
                foreach (TouchControl touch in screen.touches)
                    if (touch.press.isPressed) ignored.Add(touch.touchId.ReadValue());
            Mouse mouse = Mouse.current;
            ignoreMouse = mouse != null && mouse.leftButton.isPressed;
            if (stickFinger >= 0) TankiLog.Info("Джойстик сброшен");
            stickFinger = -1;
            floating = null;
            Direction = null;
            KnobOffset = Vector2.zero;
        }

        public PlayerInput Read()
        {
            var input = new PlayerInput();
            SuperHeld = false;

            bool stickSeen = false;
            int touches = 0;
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
            {
#if UNITY_EDITOR
                // The editor's Device Simulator has one finger, the mouse. If it lost a release (the mouse let go
                // outside the phone's screen), that touch stays down for ever and the tank keeps driving.
                // So a new touch there means every older one is over.
                foreach (TouchControl touch in screen.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;
                    int fresh = touch.touchId.ReadValue();
                    foreach (TouchControl other in screen.touches)
                    {
                        int id = other.touchId.ReadValue();
                        if (id != fresh && other.press.isPressed && ignored.Add(id))
                            TankiLog.Info("Касание " + id + " не было отпущено, считаю его отпущенным");
                    }
                }
#if UNITY_EDITOR_WIN
                // The simulator can also lose a release with the mouse still over the phone: the knob then freezes
                // and the tank drives on by itself. So check the real mouse button: a touch held while the button
                // has been up for a few frames is over.
                bool held = false;
                foreach (TouchControl touch in screen.touches)
                    if (touch.press.isPressed && !ignored.Contains(touch.touchId.ReadValue())) held = true;
                mouseUpFrames = held && (GetAsyncKeyState(0x01) & 0x8000) == 0 ? mouseUpFrames + 1 : 0;
                if (mouseUpFrames >= 3)
                {
                    foreach (TouchControl touch in screen.touches)
                    {
                        int id = touch.touchId.ReadValue();
                        if (touch.press.isPressed && ignored.Add(id))
                            TankiLog.Info("Касание " + id + " залипло (кнопка мыши уже отпущена), сбрасываю");
                    }
                    mouseUpFrames = 0;
                }
#endif
                // A touch we stopped listening to can't keep holding the stick.
                if (stickFinger >= 0 && stickFinger != int.MaxValue && ignored.Contains(stickFinger))
                {
                    stickFinger = -1;
                    floating = null;
                }
#endif
                foreach (TouchControl touch in screen.touches)
                {
                    UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                    int id = touch.touchId.ReadValue();
                    bool ended = phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                    if (ignored.Contains(id))
                    {
                        // A fresh press with the same id (the simulator reuses its one finger) is listened to again.
                        bool again = phase == UnityEngine.InputSystem.TouchPhase.Began || touch.press.wasPressedThisFrame;
                        if (ended || phase == UnityEngine.InputSystem.TouchPhase.None || !touch.press.isPressed) ignored.Remove(id);
                        if (!again) continue;
                        ignored.Remove(id);
                    }
                    if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                    if (ended && !touch.press.wasReleasedThisFrame) continue;
                    touches++;
                    HandlePointer(id, touch.position.ReadValue(),
                        phase == UnityEngine.InputSystem.TouchPhase.Began || touch.press.wasPressedThisFrame, ended, ref stickSeen);
                }
            }
            Mouse mouse = Mouse.current;
            if (ignoreMouse && mouse != null && !mouse.leftButton.isPressed) ignoreMouse = false;
            if (touches == 0 && !ignoreMouse && mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
            {
                HandlePointer(int.MaxValue, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, ref stickSeen);
            }
#if UNITY_EDITOR_WIN
            // The simulator ends the touch when the mouse leaves the phone's screen, even with the button still down.
            // While the real button is held, the stick keeps its last direction.
            if (!stickSeen && stickFinger >= 0 && stickFinger != int.MaxValue && (GetAsyncKeyState(0x01) & 0x8000) != 0)
            {
                if (!stickGhost) TankiLog.Info("Мышь ушла за экран телефона, джойстик держит направление");
                stickGhost = true;
                stickSeen = true;
            }
            else if (stickSeen) stickGhost = false;
#endif
            if (!stickSeen && stickFinger >= 0) TankiLog.Info("Джойстик отпущен");
            if (!stickSeen)
            {
#if UNITY_EDITOR_WIN
                stickGhost = false;
#endif
                stickFinger = -1;
                floating = null;
                Direction = null;
                KnobOffset = Vector2.zero;
            }

            input.Move = Direction;
            Dir? keys = KeyboardDirection();
            if (keys != null) input.Move = keys;
            Keyboard keyboard = Keyboard.current;
            input.Fire = keyboard != null && keyboard.spaceKey.isPressed;
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

            if (!ended && Vector2.Distance(position, SuperCentre) <= SuperRadius * 1.3f)
            {
                SuperHeld = true;
                return;
            }

            // Anywhere on the left 60% of the screen grabs the stick.
            bool leftSide = position.x < Screen.width * 0.6f;
            if (began && leftSide && stickFinger < 0 && !ended)
            {
                stickFinger = finger;
                TankiLog.Info(finger == int.MaxValue ? "Джойстик: мышь" : "Джойстик: касание " + finger);
                // A thumb landing low on the left becomes the stick's centre, kept fully on screen.
                Rect safe = Screen.safeArea;
                if (position.y < safe.yMin + safe.height * 0.42f)
                    floating = new Vector2(
                        Mathf.Clamp(position.x, safe.xMin + StickRadius * 1.1f, Screen.width * 0.6f - StickRadius * 0.6f),
                        Mathf.Max(position.y, safe.yMin + StickRadius * 1.2f));
                MoveStick(position);
                stickSeen = true;
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
