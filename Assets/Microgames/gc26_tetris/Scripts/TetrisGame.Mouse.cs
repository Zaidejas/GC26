using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// Mouse play: the piece follows the pointer across the well, left click drops it, right click or the wheel turns it,
    /// a middle click or a click on the hold panel holds it. A pause button and the game-over buttons take clicks too.
    /// </summary>
    public sealed partial class TetrisGame
    {
        // After a game ends, clicks and Space wait a moment, so a player still hammering drop doesn't restart at once.
        const float GameOverInputDelay = 0.8f;
        const float OverTopY = -3.05f;
        const float OverStepY = 1.55f;
        const float OverItemWidth = 9f;
        const float OverItemHeight = 1.3f;

        enum OverItem { Again, Menu }

        static readonly OverItem[] OverItems = { OverItem.Again, OverItem.Menu };

        readonly float[] overGlow = new float[OverItems.Length];
        int overIndex;
        float gameOverAge;
        Vector2 lastPlayMouse;
        bool usingMouse;

        /// <summary>Forgets the pointer position, so a game never starts with the piece jumping to it.</summary>
        void SyncPointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                lastPlayMouse = mouse.position.ReadValue();
        }

        void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                usingMouse = false;
            if (mouse == null || game.State != GameState.Playing)
                return;

            Vector2 screen = mouse.position.ReadValue();
            bool moved = (screen - lastPlayMouse).sqrMagnitude > 1f;
            lastPlayMouse = screen;
            if (screen.x < 0f || screen.y < 0f || screen.x >= Screen.width || screen.y >= Screen.height)
                return;
            Vector2 world = PointerWorld(screen);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                usingMouse = true;
                if (Inside(world, HudLayout.PauseButtonCenter, HudLayout.PauseButtonSize))
                {
                    Debug.Log("[BlockDrop] Mouse: pause");
                    game.TogglePause();
                    OpenPauseMenu();
                    return;
                }
                if (Inside(world, HudLayout.HoldPanelCenter, HudLayout.HoldPanelSize))
                {
                    MouseHold();
                    return;
                }
            }

            if (moved)
            {
                usingMouse = true;
                FollowPointer(world.x);
            }
            if (mouse.middleButton.wasPressedThisFrame)
            {
                usingMouse = true;
                MouseHold();
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                usingMouse = true;
                MouseRotate(1);
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel != 0f)
            {
                usingMouse = true;
                MouseRotate(wheel > 0f ? 1 : -1);
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Debug.Log("[BlockDrop] Mouse: drop");
                game.HardDrop();
            }
        }

        Vector2 PointerWorld(Vector2 screen)
        {
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            return new Vector2(world.x, world.y);
        }

        static bool Inside(Vector2 point, Vector2 center, Vector2 size)
        {
            return Mathf.Abs(point.x - center.x) <= size.x / 2f && Mathf.Abs(point.y - center.y) <= size.y / 2f;
        }

        /// <summary>Slides the piece until its middle is under the pointer, or a wall or block stops it.</summary>
        void FollowPointer(float worldX)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            foreach (Cell cell in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
            {
                minX = Mathf.Min(minX, cell.X);
                maxX = Mathf.Max(maxX, cell.X);
            }
            float middle = BoardView.BoardLeft + game.CurrentX + (minX + maxX + 1) / 2f;
            int steps = Mathf.RoundToInt(worldX - middle);
            int direction = steps > 0 ? 1 : -1;
            bool movedAny = false;
            for (int i = 0; i < Mathf.Abs(steps); i++)
            {
                if (!game.Move(direction))
                    break;
                movedAny = true;
            }
            if (movedAny)
                gameAudio.Play("move", 0.3f);
        }

        void MouseRotate(int direction)
        {
            if (game.Rotate(direction))
                gameAudio.Play("rotate", 0.55f, direction > 0 ? 1f : 0.9f);
        }

        void MouseHold()
        {
            Debug.Log("[BlockDrop] Mouse: hold");
            if (game.Hold())
                gameAudio.Play("hold", 0.7f);
        }

        void DrawPauseButton(float unit)
        {
            if (game.State != GameState.Playing)
                return;
            Vector2 center = HudLayout.PauseButtonCenter, size = HudLayout.PauseButtonSize;
            Mouse mouse = Mouse.current;
            bool hover = mouse != null && Inside(PointerWorld(mouse.position.ReadValue()), center, size);
            Color accent = view.AccentColor;
            RoundedWorld(center.x, center.y, size.x, size.y, hover ? new Color(0.25f, 0.2f, 0.38f, 1f) : new Color(0.13f, 0.11f, 0.2f, 0.9f), 0.3f, 0f, unit);
            RoundedWorld(center.x, center.y, size.x, size.y, new Color(accent.r, accent.g, accent.b, hover ? 0.9f : 0.35f), 0.3f, 0.05f, unit);

            // Two bars: the pause sign. The wide layout adds the word next to it.
            bool wide = size.x > size.y * 1.5f;
            float iconX = wide ? center.x - size.x / 2f + 0.8f : center.x;
            Color bars = hover ? Bright : Muted;
            RoundedWorld(iconX - 0.17f, center.y, 0.2f, 0.62f, bars, 0.06f, 0f, unit);
            RoundedWorld(iconX + 0.17f, center.y, 0.2f, 0.62f, bars, 0.06f, 0f, unit);
            if (wide)
                Label(T("Пауза", "Pause"), center.x + 0.35f, center.y, unit, 0.5f, TextAnchor.MiddleCenter, bars);
        }

        string ControlsHint()
        {
            return usingMouse
                ? T("мышь — ход   ЛКМ сброс   ПКМ или колесо поворот   клик по «Запасу» — запас",
                    "mouse moves   left click drops   right click or wheel turns   click HOLD to hold")
                : T("← → ход   ↑ поворот   ↓ быстрее   Пробел сброс", "← → move   ↑ turn   ↓ faster   Space drop");
        }

        // --- game over --------------------------------------------------------------------------

        void ResetGameOverButtons()
        {
            overIndex = 0;
            gameOverAge = 0f;
            for (int i = 0; i < overGlow.Length; i++)
                overGlow[i] = i == 0 ? 1f : 0f;
        }

        void UpdateGameOverButtons()
        {
            gameOverAge += Time.deltaTime;
            for (int i = 0; i < overGlow.Length; i++)
                overGlow[i] = Mathf.MoveTowards(overGlow[i], i == overIndex ? 1f : 0f, Time.deltaTime * 7f);
            if (gameOverAge < GameOverInputDelay)
                return;
            if (!HandleButtons(ref overIndex, OverItems.Length, OverTopY, OverStepY, OverItemWidth, OverItemHeight))
                return;

            Debug.Log("[BlockDrop] Game over: " + OverItems[overIndex]);
            if (OverItems[overIndex] == OverItem.Again)
            {
                StartNewGame();
                gameAudio.Play("hold");
            }
            else
            {
                BackToMenu();
            }
        }

        void DrawGameOverButtons(float unit)
        {
            bool battleMode = game.Mode == GameMode.Battle;
            for (int i = 0; i < OverItems.Length; i++)
            {
                string text = OverItems[i] == OverItem.Again ? (battleMode ? T("Новый бой", "New battle") : T("Новая игра", "New game")) : T("В меню", "Menu");
                DrawButton(text, null, OverTopY - i * OverStepY, OverItemWidth, OverItemHeight, overGlow[i], TitleColors[i], unit);
            }
        }
    }
}
