using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// Phone play. Drag sideways to move the piece cell by cell, tap to turn it, drag down to drop it faster,
    /// swipe down to drop it at once, swipe up (or tap the hold panel) to hold it. Taps work the menus through
    /// HandleButtons, which reads any pointer, touch included. The phone's back key is Escape.
    /// </summary>
    public sealed partial class TetrisGame
    {
        const float TapSlop = 0.6f;          // world units the finger may wander and still count as a tap
        const float TapTime = 0.35f;
        const float SwipeTime = 0.3f;
        const float SwipeDistance = 2.5f;
        const float SoftDropDistance = 1.5f;

        bool touching;
        bool touchMoved;
        bool touchSoftDrop;
        bool usingTouch;
        Vector2 touchStart;
        float touchAnchorX;
        float touchTime;

        static bool IsPhone
        {
            get { return Application.isMobilePlatform; }
        }

        void SetUpPhone()
        {
            if (!IsPhone)
                return;
            // Phones default to 30 frames a second; falling blocks look much smoother at 60.
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void HandleTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null || game.State != GameState.Playing)
            {
                touching = false;
                touchSoftDrop = false;
                return;
            }

            var touch = screen.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                usingTouch = true;
                Vector2 start = PointerWorld(touch.position.ReadValue());
                if (Inside(start, HudLayout.PauseButtonCenter, HudLayout.PauseButtonSize))
                {
                    Debug.Log("[BlockDrop] Touch: pause");
                    game.TogglePause();
                    OpenPauseMenu();
                    return;
                }
                if (Inside(start, HudLayout.HoldPanelCenter, HudLayout.HoldPanelSize))
                {
                    MouseHold();
                    return;
                }
                touching = true;
                touchMoved = false;
                touchSoftDrop = false;
                touchStart = start;
                touchAnchorX = start.x;
                touchTime = 0f;
                return;
            }

            // A finger that came down on a menu button (say "New game") is not a move in the game.
            if (!touching)
                return;

            Vector2 now = PointerWorld(touch.position.ReadValue());
            Vector2 delta = now - touchStart;
            touchTime += Time.unscaledDeltaTime;

            if (touch.press.isPressed)
            {
                DragPiece(now.x);
                if (delta.magnitude > TapSlop)
                    touchMoved = true;
                touchSoftDrop = -delta.y > SoftDropDistance && -delta.y > Mathf.Abs(delta.x) * 1.5f;
                return;
            }

            touching = false;
            touchSoftDrop = false;
            if (!touchMoved && touchTime < TapTime)
            {
                MouseRotate(1);
                return;
            }
            bool quick = touchTime < SwipeTime;
            if (quick && -delta.y > SwipeDistance && -delta.y > Mathf.Abs(delta.x) * 1.5f)
            {
                Debug.Log("[BlockDrop] Touch: drop");
                game.HardDrop();
            }
            else if (quick && delta.y > SwipeDistance && delta.y > Mathf.Abs(delta.x) * 1.5f)
            {
                MouseHold();
            }
        }

        /// <summary>Moves the piece one cell for every cell the finger travels sideways.</summary>
        void DragPiece(float fingerX)
        {
            bool movedAny = false;
            while (fingerX - touchAnchorX >= 1f || touchAnchorX - fingerX >= 1f)
            {
                int direction = fingerX > touchAnchorX ? 1 : -1;
                if (!game.Move(direction))
                {
                    // Against a wall: start counting again from here, so moving back answers at once.
                    touchAnchorX = fingerX;
                    break;
                }
                touchAnchorX += direction;
                movedAny = true;
            }
            if (movedAny)
                gameAudio.Play("move", 0.3f);
        }

        string TouchHint()
        {
            return T("веди пальцем — ход   тап — поворот   свайп вниз — сброс   вверх — запас",
                     "drag to move   tap to turn   swipe down to drop   up to hold");
        }

        // A phone can put the game away at any moment; pause it and keep what was earned.
        void OnApplicationPause(bool paused)
        {
            if (!paused || game == null)
                return;
            if (!inMenu && game.State == GameState.Playing)
            {
                game.TogglePause();
                OpenPauseMenu();
            }
            SaveAchievements();
            SaveStats();
        }
    }
}
