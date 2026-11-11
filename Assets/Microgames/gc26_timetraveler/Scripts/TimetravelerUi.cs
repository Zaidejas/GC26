using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Buttons for the greybox IMGUI screens that react through the Input System (Pointer.current),
    /// not through IMGUI's own mouse events. IMGUI clicks do not arrive reliably in the Device Simulator
    /// and on touch screens with "Input System only", which made the buttons dead.
    /// A button fires when a press starts and ends inside it. Call Update() from the screen's Update().
    /// </summary>
    public class TimetravelerUi
    {
        Vector2? _downAt;     // GUI coordinates (origin top-left)
        Vector2? _upAt;

        /// <summary>Read the pointer once per frame (before OnGUI).</summary>
        public void Update()
        {
            _upAt = null;
            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = ToGui(pointer.position.ReadValue());
            if (pointer.press.wasPressedThisFrame) _downAt = pos;
            if (pointer.press.wasReleasedThisFrame) _upAt = pos;
        }

        public static Vector2 ToGui(Vector2 inputSystemPosition) =>
            new Vector2(inputSystemPosition.x, Screen.height - inputSystemPosition.y);

        /// <summary>True if a pointer press that started this frame or earlier is inside the rect (GUI coordinates).</summary>
        public bool PressStartedIn(Rect rect) => _downAt.HasValue && rect.Contains(_downAt.Value);

        /// <summary>Draws a button and returns true once when it is tapped/clicked.</summary>
        public bool Button(Rect rect, string text, GUIStyle style, bool enabled = true)
        {
            var wasEnabled = GUI.enabled;
            enabled = enabled && wasEnabled;
            GUI.enabled = enabled;
            GUI.Box(rect, text, style);
            GUI.enabled = wasEnabled;

            // Decide once per frame, on the repaint pass.
            if (!enabled || Event.current.type != EventType.Repaint || !_upAt.HasValue || !_downAt.HasValue) return false;
            if (!rect.Contains(_upAt.Value) || !rect.Contains(_downAt.Value)) return false;
            _upAt = null;   // one button per tap
            _downAt = null;
            return true;
        }
    }
}
