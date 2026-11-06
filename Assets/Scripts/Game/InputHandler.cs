using Lines.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

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

        // Touches are taken as events, not checked once per frame: a quick tap can go down and up
        // inside one frame (the iOS Simulator's taps always do), and a per-frame check misses it.
        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            ETouch.onFingerDown += OnFingerDown;
        }

        void OnDisable()
        {
            ETouch.onFingerDown -= OnFingerDown;
            EnhancedTouchSupport.Disable();
        }

        void OnFingerDown(Finger finger) => HandlePress(finger.screenPosition);

        void Update()
        {
            // The mouse, for playing in the Unity editor.
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                HandlePress(mouse.position.ReadValue());
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
