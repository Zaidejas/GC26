using System.Collections.Generic;
using BlockDrop.Core;
using BlockDrop.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockDrop
{
    /// <summary>Looks of the main menu: tile title, falling pieces, soft buttons and result panels.</summary>
    public sealed partial class GameController
    {
        const int TitleRows = 5;
        const int TitleColumns = 45;
        const int BackgroundPieces = 16;

        // "BLOCK DROP" as tile letters, 4 columns by 5 rows each.
        static readonly string[][] TitleLetters =
        {
            new[] { "### ", "#  #", "### ", "#  #", "### " }, // B
            new[] { "#   ", "#   ", "#   ", "#   ", "####" }, // L
            new[] { " ## ", "#  #", "#  #", "#  #", " ## " }, // O
            new[] { " ###", "#   ", "#   ", "#   ", " ###" }, // C
            new[] { "#  #", "# # ", "##  ", "# # ", "#  #" }, // K
            new[] { "### ", "#  #", "#  #", "#  #", "### " }, // D
            new[] { "### ", "#  #", "### ", "# # ", "#  #" }, // R
            new[] { " ## ", "#  #", "#  #", "#  #", " ## " }, // O
            new[] { "### ", "#  #", "### ", "#   ", "#   " }, // P
        };

        static readonly int[] TitleColors = { 2, 6, 3, 0, 1, 4, 5, 2, 6 };

        sealed class FallingPiece
        {
            public PieceType Type;
            public int Rotation;
            public float X, Y, Speed, Cell, Alpha;
        }

        readonly List<FallingPiece> menuPieces = new List<FallingPiece>();
        readonly float[] menuGlow = new float[MenuItems.Length];
        readonly System.Random menuRandom = new System.Random(2026);
        float menuClock;

        void UpdateMenuLooks(float deltaTime)
        {
            menuClock += deltaTime;
            for (int i = 0; i < menuGlow.Length; i++)
                menuGlow[i] = Mathf.MoveTowards(menuGlow[i], i == menuIndex ? 1f : 0f, deltaTime * 7f);

            while (menuPieces.Count < BackgroundPieces)
            {
                var piece = new FallingPiece();
                RespawnPiece(piece);
                // Spread the first ones over the whole screen so the menu never opens empty.
                piece.Y = MenuRange(-12f, 13f);
                menuPieces.Add(piece);
            }

            foreach (FallingPiece piece in menuPieces)
            {
                piece.Y -= piece.Speed * deltaTime;
                if (piece.Y < -15f)
                    RespawnPiece(piece);
            }
        }

        void RespawnPiece(FallingPiece piece)
        {
            piece.Type = (PieceType)menuRandom.Next(PieceShapes.PieceCount);
            piece.Rotation = menuRandom.Next(PieceShapes.RotationCount);
            float halfWidth = mainCamera.orthographicSize * Screen.width / Mathf.Max(1, Screen.height) + 1f;
            piece.X = MenuRange(-halfWidth, halfWidth);
            piece.Y = MenuRange(13f, 18f);
            // Nearer pieces are larger, brighter and faster, which gives the background some depth.
            float depth = (float)menuRandom.NextDouble();
            piece.Cell = Mathf.Lerp(0.55f, 1.1f, depth);
            piece.Speed = Mathf.Lerp(0.5f, 1.5f, depth);
            piece.Alpha = Mathf.Lerp(0.07f, 0.2f, depth);
        }

        float MenuRange(float min, float max)
        {
            return min + (max - min) * (float)menuRandom.NextDouble();
        }

        void DrawMenu(float unit)
        {
            FillWorld(0f, 0f, 200f, 200f, new Color(0.07f, 0.055f, 0.1f, 1f));
            DrawMenuBackground();
            DrawMenuTitle(unit);

            for (int i = 0; i < MenuItems.Length; i++)
                DrawMenuButton(i, unit);

            DrawTablePanel("Марафон: лучшие очки", -4.65f, marathonTable, unit);
            DrawTablePanel("Бой: лучшие очки", 4.65f, battleTable, unit);
            Label("↑ ↓ выбрать    Enter подтвердить    или мышью", 0f, -9.95f, unit, 0.42f, TextAnchor.MiddleCenter, Muted);
            Label("Монеты: " + coinsTotal.ToString("N0"), 9.6f, -9.95f, unit, 0.42f, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.25f));
        }

        void DrawMenuBackground()
        {
            foreach (FallingPiece piece in menuPieces)
            {
                Color color = BoardView.PieceColor((int)piece.Type);
                color.a = piece.Alpha;
                foreach (Cell cell in PieceShapes.Get(piece.Type, piece.Rotation))
                    DrawTile(piece.X + cell.X * piece.Cell, piece.Y + cell.Y * piece.Cell, piece.Cell * 0.94f, color);
            }
        }

        void DrawMenuTitle(float unit)
        {
            float visibleWidth = 2f * mainCamera.orthographicSize * Screen.width / Mathf.Max(1, Screen.height);
            float cell = Mathf.Min(0.5f, visibleWidth * 0.92f / TitleColumns);
            float left = -TitleColumns * cell / 2f + cell / 2f;
            const float CenterY = 7.3f;

            int column = 0;
            for (int letter = 0; letter < TitleLetters.Length; letter++)
            {
                // Each letter bobs a little, one after another, like a slow wave.
                float bob = Mathf.Sin(menuClock * 1.6f + letter * 0.55f) * 0.09f;
                Color color = BoardView.PieceColor(TitleColors[letter]);
                string[] rows = TitleLetters[letter];
                for (int row = 0; row < TitleRows; row++)
                {
                    for (int x = 0; x < rows[row].Length; x++)
                    {
                        if (rows[row][x] != '#')
                            continue;
                        float worldX = left + (column + x) * cell;
                        float worldY = CenterY + bob + (TitleRows / 2 - row) * cell;
                        DrawTile(worldX + 0.07f, worldY - 0.09f, cell * 0.94f, new Color(0f, 0f, 0f, 0.45f));
                        DrawTile(worldX, worldY, cell * 0.94f, color);
                    }
                }
                // One column between letters, one more between the two words.
                column += 5 + (letter == 4 ? 1 : 0);
            }
        }

        void DrawMenuButton(int index, float unit)
        {
            MenuItem item = MenuItems[index];
            DrawButton(MenuText(item), MenuHint(item), MenuItemY(index), MenuItemWidth, MenuItemHeight, menuGlow[index], TitleColors[index], unit);
        }

        void DrawButton(string text, string hint, float y, float baseWidth, float height, float rawGlow, int tileColor, float unit)
        {
            float glow = Mathf.SmoothStep(0f, 1f, rawGlow);
            float width = baseWidth + glow * 0.5f;
            Color accent = view.AccentColor;

            RoundedWorld(0f, y - 0.1f, width, height, new Color(0f, 0f, 0f, 0.4f), 0.4f, 0f, unit);
            RoundedWorld(0f, y, width, height, Color.Lerp(new Color(0.13f, 0.11f, 0.2f, 1f), new Color(0.25f, 0.2f, 0.38f, 1f), glow), 0.4f, 0f, unit);
            if (glow > 0.01f)
                RoundedWorld(0f, y, width, height, new Color(accent.r, accent.g, accent.b, glow), 0.4f, 0.07f, unit);

            // A small tile on the left, the same colours the pieces use.
            Color tile = BoardView.PieceColor(tileColor);
            tile.a = Mathf.Lerp(0.35f, 1f, glow);
            float pulse = 1f + glow * 0.08f * Mathf.Sin(menuClock * 5f);
            DrawTile(-width / 2f + 0.95f, y, 0.72f * pulse, tile);

            Color textColor = Color.Lerp(Muted, Bright, glow);
            if (hint == null)
            {
                Label(text, 0f, y, unit, 0.78f + glow * 0.06f, TextAnchor.MiddleCenter, textColor);
            }
            else
            {
                Label(text, 0f, y + 0.2f, unit, 0.68f + glow * 0.05f, TextAnchor.MiddleCenter, textColor);
                Label(hint, 0f, y - 0.43f, unit, 0.34f, TextAnchor.MiddleCenter, Muted);
            }
        }

        // Moves the selection by keys and pointer. Returns true when the selected button was pressed.
        bool HandleButtons(ref int index, int count, float topY, float stepY, float width, float height)
        {
            if (Pressed(Key.UpArrow) || Pressed(Key.W))
            {
                index = (index + count - 1) % count;
                gameAudio.Play("move", 0.6f);
            }
            if (Pressed(Key.DownArrow) || Pressed(Key.S))
            {
                index = (index + 1) % count;
                gameAudio.Play("move", 0.6f);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                // The pointer only takes over when it moves, so it never fights the arrow keys.
                Vector2 position = mouse.position.ReadValue();
                int hovered = ButtonAt(position, count, topY, stepY, width, height);
                if (hovered >= 0 && position != lastMousePosition && hovered != index)
                {
                    index = hovered;
                    gameAudio.Play("move", 0.6f);
                }
                lastMousePosition = position;
                if (hovered >= 0 && mouse.leftButton.wasPressedThisFrame)
                {
                    index = hovered;
                    return true;
                }
            }

            return Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Space);
        }

        int ButtonAt(Vector2 screenPosition, int count, float topY, float stepY, float width, float height)
        {
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f));
            if (Mathf.Abs(world.x) > width / 2f)
                return -1;
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(world.y - (topY - i * stepY)) <= height / 2f)
                    return i;
            }
            return -1;
        }

        // --- pause ----------------------------------------------------------------------------

        enum PauseItem { Resume, Restart, Sound, Menu }

        static readonly PauseItem[] PauseItems = { PauseItem.Resume, PauseItem.Restart, PauseItem.Sound, PauseItem.Menu };

        const float PauseTopY = 2.6f;
        const float PauseStepY = 2.2f;
        const float PauseItemWidth = 11f;
        const float PauseItemHeight = 1.85f;

        readonly float[] pauseGlow = new float[PauseItems.Length];
        int pauseIndex;
        bool pauseInputUsed;

        void OpenPauseMenu()
        {
            Debug.Log("[BlockDrop] Pause opened");
            pauseIndex = 0;
            for (int i = 0; i < pauseGlow.Length; i++)
                pauseGlow[i] = i == 0 ? 1f : 0f;
            gameAudio.Play("hold", 0.5f);
        }

        void UpdatePauseMenu()
        {
            menuClock += Time.deltaTime;
            for (int i = 0; i < pauseGlow.Length; i++)
                pauseGlow[i] = Mathf.MoveTowards(pauseGlow[i], i == pauseIndex ? 1f : 0f, Time.deltaTime * 7f);

            if (HandleButtons(ref pauseIndex, PauseItems.Length, PauseTopY, PauseStepY, PauseItemWidth, PauseItemHeight))
                ActivatePauseItem();
        }

        void ActivatePauseItem()
        {
            pauseInputUsed = true;
            PauseItem item = PauseItems[pauseIndex];
            Debug.Log("[BlockDrop] Pause: " + item);
            switch (item)
            {
                case PauseItem.Resume:
                    game.TogglePause();
                    break;
                case PauseItem.Restart:
                    StartNewGame();
                    gameAudio.Play("hold");
                    break;
                case PauseItem.Sound:
                    gameAudio.ToggleMute();
                    gameAudio.Play("hold");
                    break;
                case PauseItem.Menu:
                    BackToMenu();
                    break;
            }
        }

        void DrawPauseMenu(float unit)
        {
            FillWorld(0f, 0f, 200f, 200f, new Color(0.03f, 0.02f, 0.06f, 0.8f));

            Color accent = view.AccentColor;
            RoundedWorld(0f, -0.35f, 14.6f, 16.9f, new Color(0f, 0f, 0f, 0.45f), 0.6f, 0f, unit);
            RoundedWorld(0f, -0.2f, 14.6f, 16.9f, new Color(0.09f, 0.075f, 0.14f, 1f), 0.6f, 0f, unit);
            RoundedWorld(0f, -0.2f, 14.6f, 16.9f, new Color(accent.r, accent.g, accent.b, 0.35f), 0.6f, 0.05f, unit);

            Label("ПАУЗА", 0f, 6.65f, unit, 1.25f, TextAnchor.MiddleCenter, accent);
            Label(PauseStats(), 0f, 4.75f, unit, 0.5f, TextAnchor.MiddleCenter, Muted);

            for (int i = 0; i < PauseItems.Length; i++)
                DrawButton(PauseText(PauseItems[i]), null, PauseTopY - i * PauseStepY, PauseItemWidth, PauseItemHeight, pauseGlow[i], TitleColors[i], unit);

            Label("P продолжить   R заново   Backspace в меню", 0f, -7.65f, unit, 0.4f, TextAnchor.MiddleCenter, Muted);
        }

        string PauseText(PauseItem item)
        {
            switch (item)
            {
                case PauseItem.Resume: return "Продолжить";
                case PauseItem.Restart: return "Начать заново";
                case PauseItem.Sound: return gameAudio.Muted ? "Звук: выкл." : "Звук: вкл.";
                default: return "В главное меню";
            }
        }

        string PauseStats()
        {
            if (game.Mode == GameMode.Battle)
                return "Бой     счёт " + game.Score.ToString("N0") + "     бот " + battle.Level + "     монет +" + battle.CoinsEarned;
            return "Марафон    счёт " + game.Score.ToString("N0") + "     линий " + game.Lines + "     уровень " + game.Level;
        }

        static string MenuHint(MenuItem item)
        {
            switch (item)
            {
                case MenuItem.Marathon: return "играй, пока хватит места";
                case MenuItem.Battle: return "линии бьют бота, комбо сильнее, падают монетки";
                default: return null;
            }
        }

        void DrawTablePanel(string title, float x, Leaderboard table, float unit)
        {
            RoundedWorld(x, -6.9f, 8.7f, 4.7f, new Color(0.11f, 0.09f, 0.16f, 0.92f), 0.35f, 0f, unit);
            Label(title, x, -5.08f, unit, 0.5f, TextAnchor.MiddleCenter, view.AccentColor);
            for (int i = 0; i < Leaderboard.Size; i++)
            {
                bool filled = i < table.Entries.Count;
                string value = filled ? ((long)table.Entries[i]).ToString("N0") : "—";
                Color color = !filled ? new Color(Muted.r, Muted.g, Muted.b, 0.4f) : i == 0 ? Bright : Muted;
                float rowY = -5.82f - i * 0.68f;
                Label((i + 1).ToString(), x - 2.6f, rowY, unit, 0.46f, TextAnchor.MiddleCenter, color);
                Label(value, x + 0.5f, rowY, unit, 0.5f, TextAnchor.MiddleCenter, color);
            }
        }

        void DrawTile(float centerX, float centerY, float size, Color color)
        {
            Vector2 min = WorldToGui(centerX - size / 2f, centerY - size / 2f);
            Vector2 max = WorldToGui(centerX + size / 2f, centerY + size / 2f);
            GUI.color = color;
            GUI.DrawTexture(new Rect(min.x, max.y, max.x - min.x, min.y - max.y), view.TileTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        // border 0 fills the shape; otherwise only an outline of that thickness is drawn.
        void RoundedWorld(float centerX, float centerY, float width, float height, Color color, float radius, float border, float unit)
        {
            Vector2 min = WorldToGui(centerX - width / 2f, centerY - height / 2f);
            Vector2 max = WorldToGui(centerX + width / 2f, centerY + height / 2f);
            var rect = new Rect(min.x, max.y, max.x - min.x, min.y - max.y);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, border * unit, radius * unit);
        }
    }
}
