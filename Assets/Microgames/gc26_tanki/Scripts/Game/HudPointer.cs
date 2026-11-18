using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// Menu buttons that take a press from either IMGUI's own mouse events or the Input System.
    /// The Unity editor's Device Simulator sends taps only to the Input System, so plain GUI.Button never
    /// fired there; some players and injected clicks reach only IMGUI. A press counts when it starts and
    /// ends inside the same button, and one press never fires twice even when both paths see it.
    /// </summary>
    static class HudPointer
    {
        static int frame = -1;
        static int firedFrame = -1;
        static bool held;
        static bool released;
        static Vector2 position = new Vector2(-1f, -1f);
        static Vector2 pressedAt = new Vector2(-1f, -1f);
        static Vector2 guiPressedAt = new Vector2(-1f, -1f);
        static bool guiHeld;
        static float lastFire = -1f;
        static readonly Vector2 Nowhere = new Vector2(-1f, -1f);

        /// <summary>Reads the pointer once per frame, in GUI coordinates (y down).</summary>
        static void Refresh()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;

            bool seen = false, down = false, up = false, isHeld = false;
            Vector2 at = Vector2.zero;
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
            {
                TouchControl touch = screen.primaryTouch;
                if (touch.press.isPressed || touch.press.wasReleasedThisFrame)
                {
                    seen = true;
                    at = touch.position.ReadValue();
                    down = touch.press.wasPressedThisFrame;
                    up = touch.press.wasReleasedThisFrame;
                    isHeld = touch.press.isPressed;
                }
            }
            Mouse mouse = Mouse.current;
            if (!seen && mouse != null)
            {
                seen = true;
                at = mouse.position.ReadValue();
                down = mouse.leftButton.wasPressedThisFrame;
                up = mouse.leftButton.wasReleasedThisFrame;
                isHeld = mouse.leftButton.isPressed;
            }
            if (!seen) { held = released = false; return; }

            position = new Vector2(at.x, Screen.height - at.y);
            if (down) pressedAt = position;
            held = isHeld;
            released = up;
        }

        /// <summary>Draws the button and returns true once, when a press on it ends.</summary>
        public static bool Button(Rect rect, GUIContent content, GUIStyle style, string name = null)
        {
            Refresh();
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                guiPressedAt = e.mousePosition;
                guiHeld = true;
                e.Use();
                return false;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                guiHeld = false;
                if (!rect.Contains(e.mousePosition) || !rect.Contains(guiPressedAt)) return false;
                guiPressedAt = Nowhere;
                e.Use();
                return Fire(name ?? content.text);
            }
            if (e.type != EventType.Repaint) return false;

            bool inside = rect.Contains(position);
            bool pressed = (held && inside && rect.Contains(pressedAt)) || (guiHeld && rect.Contains(guiPressedAt));
            style.Draw(rect, content, inside, pressed && GUI.enabled, false, false);
            if (!released || !inside || !rect.Contains(pressedAt) || firedFrame == frame) return false;
            firedFrame = frame;
            pressedAt = Nowhere;
            return Fire(name ?? content.text);
        }

        /// <summary>One press fires once: whichever path sees it second is ignored.</summary>
        static bool Fire(string name)
        {
            if (!GUI.enabled || Time.unscaledTime - lastFire < 0.3f) return false;
            lastFire = Time.unscaledTime;
            TankiLog.Info("Кнопка: " + (string.IsNullOrEmpty(name) ? "без подписи" : name.Replace('\n', ' ')));
            return true;
        }
    }
}
