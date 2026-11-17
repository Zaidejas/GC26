using GC26.Lines.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace GC26.Lines.Game
{
    /// <summary>
    /// Reads taps (touch on phones, mouse clicks in the editor) with the new Input System and
    /// passes the tapped board cell to the <see cref="LinesGameController"/>. Taps outside the board are ignored.
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
            ETouch.onFingerMove += OnFingerMove;
        }

        void OnDisable()
        {
            ETouch.onFingerDown -= OnFingerDown;
            ETouch.onFingerMove -= OnFingerMove;
            EnhancedTouchSupport.Disable();
        }

        // L16: while aiming a puffball, a finger sliding on the board moves the burst along (the press itself
        // already moved it there; lifting the finger doesn't burst: a tap on the lit spot does).
        bool pressedAiming;

        void OnFingerDown(Finger finger)
        {
            pressedAiming = game != null && game.Controller != null && game.Controller.Aiming;
            HandlePress(finger.screenPosition);
        }

        void OnFingerMove(Finger finger)
        {
            if (pressedAiming) HandleSlide(finger.screenPosition);
        }

        /// <summary>A finger (or the mouse with its button held) sliding at a screen position while aiming (also tests).</summary>
        public void HandleSlide(Vector2 screenPosition)
        {
            if (game == null || game.Controller == null || game.InputBlocked || !game.Controller.Aiming) return;
            if (game.Layout.TryGetCell(screenPosition, out var cell)) game.Controller.AimAt(cell);
        }

        void Update()
        {
            // The mouse, for playing in the Unity editor.
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pressedAiming = game != null && game.Controller != null && game.Controller.Aiming;
                HandlePress(mouse.position.ReadValue());
            }
            else if (mouse != null && mouse.leftButton.isPressed && pressedAiming && mouse.delta.ReadValue() != Vector2.zero)
                HandleSlide(mouse.position.ReadValue());
        }

        /// <summary>Handles a press at a screen position (pixels, origin bottom-left). Also used by tests.</summary>
        public bool HandlePress(Vector2 screenPosition)
        {
            if (game == null || game.Controller == null || game.InputBlocked) return false;
            if (!game.Layout.TryGetCell(screenPosition, out var cell)) return false;
            return game.Controller.Tap(cell);
        }
    }
}
