using Lines.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lines.Game
{
    /// <summary>
    /// Reads taps (touch on phones, mouse clicks in the editor) with the new Input System and
    /// passes the tapped board cell to the <see cref="GameController"/>. Taps outside the board are ignored.
    /// </summary>
    public sealed class InputHandler : MonoBehaviour
    {
        GameBootstrap game;

        public void Init(GameBootstrap bootstrap) => game = bootstrap;

        void Update()
        {
            // Pointer covers both the touchscreen and the mouse.
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            HandlePress(pointer.position.ReadValue());
        }

        /// <summary>Handles a press at a screen position (pixels, origin bottom-left). Also used by tests.</summary>
        public bool HandlePress(Vector2 screenPosition)
        {
            if (game == null || game.Controller == null) return false;
            if (!game.Layout.TryGetCell(screenPosition, out var cell)) return false;
            return game.Controller.Tap(cell);
        }
    }
}
