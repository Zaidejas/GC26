using System.Collections.Generic;
using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.View
{
    /// <summary>Draws the well and the side previews with pooled sprites built at runtime.</summary>
    public sealed class BoardView
    {
        public const float BoardLeft = -Board.Width / 2f;
        public const float BoardBottom = -Board.VisibleHeight / 2f;
        public const float PanelCenterOffset = 3.6f;

        const float CellScale = 0.94f;
        const float PreviewScale = 0.72f;
        const float FlashDuration = 0.28f;

        static readonly Color[] PieceColors =
        {
            Hex(0xF2C14E), // I: saffron
            Hex(0xF78154), // O: coral
            Hex(0x4D9DE0), // T: sky
            Hex(0x5FAD56), // S: leaf
            Hex(0xE15554), // Z: brick
            Hex(0xB079D6), // J: orchid
            Hex(0x3BC4B2), // L: lagoon
        };

        static readonly Color DisabledColor = Hex(0x5A5568);
        static readonly Color DangerColor = Hex(0x7A1F2E);

        readonly struct Theme
        {
            public readonly Color Background, Well, EmptyCell, Panel, Accent;

            public Theme(int background, int well, int emptyCell, int panel, int accent)
            {
                Background = Hex(background);
                Well = Hex(well);
                EmptyCell = Hex(emptyCell);
                Panel = Hex(panel);
                Accent = Hex(accent);
            }
        }

        // One look per level, repeating. Piece colours stay the same so pieces remain recognisable.
        static readonly Theme[] Themes =
        {
            new Theme(0x120F1A, 0x1A1626, 0x252036, 0x201B2E, 0xF7C24F), // night violet
            new Theme(0x0A1620, 0x0F202C, 0x18323F, 0x132732, 0x5FD0E8), // deep water
            new Theme(0x0D1711, 0x13211A, 0x1F3427, 0x18291F, 0x8FD66A), // moss
            new Theme(0x1C0F0D, 0x271613, 0x3B2420, 0x2C1A16, 0xFF9A4A), // embers
            new Theme(0x1A0D1A, 0x261325, 0x3B2038, 0x2B172B, 0xF27FB2), // dusk rose
            new Theme(0x0F131B, 0x171D28, 0x262E3E, 0x1C2331, 0xA9C1FF), // cold steel
        };

        const float ThemeFadeSeconds = 0.7f;
        const float LockGlowDuration = 0.18f;
        const int DangerRows = 6;

        Color backgroundColor, wellColor, emptyCellColor, panelColor, accentColor;
        Color fadeBackground, fadeWell, fadeEmptyCell, fadePanel, fadeAccent;
        Theme targetTheme;
        float themeFade = 1f;
        SpriteRenderer wellRenderer;
        readonly List<SpriteRenderer> panels = new List<SpriteRenderer>();
        readonly float[,] lockGlow = new float[Board.Width, Board.VisibleHeight];
        readonly int[,] rowSnapshot = new int[Board.Width, Board.VisibleHeight];
        float gameOverClock;
        SpriteRenderer activeIcon, ghostIcon, holdIcon, zone;
        readonly SpriteRenderer[] previewIcons = new SpriteRenderer[Game.PreviewCount];

        public BoardEffects Effects { get; }
        public Color BackgroundColor { get { return backgroundColor; } }
        public Color AccentColor { get { return accentColor; } }
        public Texture TileTexture { get { return tileSprite.texture; } }
        public Sprite PlainSprite { get { return plainSprite; } }
        public Color PanelColor { get { return panelColor; } }

        readonly List<SpriteRenderer> legendIcons = new List<SpriteRenderer>();

        /// <summary>The battle mode uses this corner for the bot.</summary>
        public void SetLegendVisible(bool visible)
        {
            foreach (SpriteRenderer icon in legendIcons)
                icon.enabled = visible;
        }

        public static Color PieceColor(int index)
        {
            return PieceColors[index];
        }

        readonly Transform root;
        readonly Material material;
        readonly Sprite tileSprite;
        readonly Sprite plainSprite;
        readonly SpriteRenderer[,] grid = new SpriteRenderer[Board.Width, Board.VisibleHeight];
        readonly SpriteRenderer[][] previews = new SpriteRenderer[Game.PreviewCount][];
        readonly SpriteRenderer[] holdCells;
        readonly SpriteRenderer[] flashStrips = new SpriteRenderer[Board.VisibleHeight];
        readonly float[] flashTimers = new float[Board.VisibleHeight];

        public static float RightPanelX { get { return Board.Width / 2f + PanelCenterOffset; } }
        public static float LeftPanelX { get { return -RightPanelX; } }

        public BoardView(Transform parent, Material spriteMaterial)
        {
            root = new GameObject("BoardView").transform;
            root.SetParent(parent, false);
            material = spriteMaterial;
            tileSprite = CreateTileSprite();
            plainSprite = CreatePlainSprite();
            Effects = new BoardEffects(root, material, tileSprite, plainSprite);

            targetTheme = Themes[0];
            backgroundColor = targetTheme.Background;
            wellColor = targetTheme.Well;
            emptyCellColor = targetTheme.EmptyCell;
            panelColor = targetTheme.Panel;
            accentColor = targetTheme.Accent;

            CreateStaticBackdrop();

            for (int x = 0; x < Board.Width; x++)
            {
                for (int y = 0; y < Board.VisibleHeight; y++)
                {
                    grid[x, y] = CreateRenderer("Cell", tileSprite, GridPosition(x, y), CellScale, 2);
                    grid[x, y].color = emptyCellColor;
                }
            }

            for (int row = 0; row < flashStrips.Length; row++)
            {
                Vector2 center = new Vector2(0f, BoardBottom + row + 0.5f);
                flashStrips[row] = CreateRenderer("Flash", plainSprite, center, 1f, 3);
                flashStrips[row].transform.localScale = new Vector3(Board.Width, 1f, 1f);
                flashStrips[row].enabled = false;
            }

            for (int slot = 0; slot < previews.Length; slot++)
                previews[slot] = CreateCellGroup();
            holdCells = CreateCellGroup();

            activeIcon = CreateIconRenderer();
            ghostIcon = CreateIconRenderer();
            holdIcon = CreateIconRenderer();
            for (int slot = 0; slot < previewIcons.Length; slot++)
                previewIcons[slot] = CreateIconRenderer();
            zone = CreateRenderer("SpecialZone", plainSprite, Vector2.zero, 1f, 3);
            zone.enabled = false;

            CreateLegendIcon(SpecialKind.Bomb, LegendY(0));
            CreateLegendIcon(SpecialKind.Lightning, LegendY(1));
        }

        /// <summary>World height of a line in the special-block legend under the right panel.</summary>
        public static float LegendY(int line)
        {
            return -5.0f - line * 1.05f;
        }

        public static float LegendIconX { get { return RightPanelX - 2.05f; } }

        void CreateLegendIcon(SpecialKind kind, float y)
        {
            SpriteRenderer icon = CreateRenderer("LegendIcon", SpecialIcons.Get(kind), new Vector2(LegendIconX, y), 0.8f, 2);
            icon.color = Color.white;
            legendIcons.Add(icon);
        }

        SpriteRenderer CreateIconRenderer()
        {
            SpriteRenderer icon = CreateRenderer("SpecialIcon", null, Vector2.zero, 1f, 4);
            icon.enabled = false;
            return icon;
        }

        void PlaceIcon(SpriteRenderer icon, SpecialKind kind, Vector2 position, float scale, float alpha)
        {
            icon.enabled = kind != SpecialKind.None;
            if (!icon.enabled)
                return;
            icon.sprite = SpecialIcons.Get(kind);
            icon.color = new Color(1f, 1f, 1f, alpha);
            icon.transform.localPosition = position;
            icon.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void UpdateSpecialVisuals(Game game)
        {
            bool playing = !game.IsFinished;
            SpecialKind kind = playing ? game.CurrentSpecial : SpecialKind.None;
            Cell cell = PieceShapes.Get(game.CurrentType, game.CurrentRotation)[Game.SpecialCellIndex];
            int x = game.CurrentX + cell.X;
            int activeY = game.CurrentY + cell.Y;
            int ghostY = game.GhostY + cell.Y;

            float pulse = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f);
            PlaceIcon(activeIcon, activeY < Board.VisibleHeight ? kind : SpecialKind.None, GridPosition(x, activeY), 0.94f * pulse, 1f);
            PlaceIcon(ghostIcon, ghostY < Board.VisibleHeight && ghostY != activeY ? kind : SpecialKind.None, GridPosition(x, ghostY), 0.7f, 0.45f);

            for (int slot = 0; slot < previewIcons.Length; slot++)
            {
                PlaceIcon(previewIcons[slot], game.GetUpcomingSpecial(slot),
                    previews[slot][Game.SpecialCellIndex].transform.localPosition, PreviewScale * pulse, 1f);
            }
            PlaceIcon(holdIcon, game.HeldType.HasValue ? game.HeldSpecial : SpecialKind.None,
                holdCells[Game.SpecialCellIndex].transform.localPosition, PreviewScale * pulse, 1f);

            UpdateZone(kind, x, ghostY);
        }

        // Shows where a bomb or lightning will strike if the piece lands where its ghost is.
        void UpdateZone(SpecialKind kind, int x, int y)
        {
            zone.enabled = kind == SpecialKind.Bomb || kind == SpecialKind.Lightning;
            if (!zone.enabled)
                return;

            float alpha = 0.16f + 0.05f * Mathf.Sin(Time.unscaledTime * 8f);
            int minX = x, maxX = x, minY = 0, maxY = Board.VisibleHeight - 1;
            if (kind == SpecialKind.Bomb)
            {
                minX = Mathf.Max(0, x - 2);
                maxX = Mathf.Min(Board.Width - 1, x + 2);
                minY = Mathf.Max(0, y - 2);
                maxY = Mathf.Min(Board.VisibleHeight - 1, y + 2);
            }
            Color tint = SpecialIcons.ColorOf(kind);
            zone.color = new Color(tint.r, tint.g, tint.b, alpha);
            zone.transform.localPosition = new Vector2(BoardLeft + (minX + maxX + 1) / 2f, BoardBottom + (minY + maxY + 1) / 2f);
            zone.transform.localScale = new Vector3(maxX - minX + 1, maxY - minY + 1, 1f);
        }

        /// <summary>Shards from every cell a special removed, in the colours they had.</summary>
        public void BurstRemoved(SpecialResult result)
        {
            int perCell = result.Removed.Count > 14 ? 2 : 4;
            foreach (RemovedCell cell in result.Removed)
            {
                if (cell.Y < Board.VisibleHeight)
                    Effects.Burst(GridPosition(cell.X, cell.Y), PieceColors[cell.Value], perCell, 11f);
            }
        }

        public void TriggerFlash(IReadOnlyList<int> rows)
        {
            foreach (int row in rows)
            {
                if (row < flashTimers.Length)
                    flashTimers[row] = FlashDuration;
            }
        }

        /// <summary>Starts the fade to the look of this level.</summary>
        public void SetLevel(int level, bool instant)
        {
            fadeBackground = backgroundColor;
            fadeWell = wellColor;
            fadeEmptyCell = emptyCellColor;
            fadePanel = panelColor;
            fadeAccent = accentColor;
            targetTheme = Themes[(level - 1) % Themes.Length];
            themeFade = instant ? 0.999f : 0f;
        }

        public void ResetForNewGame()
        {
            gameOverClock = 0f;
            System.Array.Clear(lockGlow, 0, lockGlow.Length);
            SetLevel(1, true);
        }

        /// <summary>Call on lock, before the rows clear: glows the piece and remembers cell colours.</summary>
        public void OnPieceLocked(Game game)
        {
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.VisibleHeight; y++)
                    rowSnapshot[x, y] = game.Board.Get(x, y);

            foreach (Cell c in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
            {
                int x = game.CurrentX + c.X;
                int y = game.CurrentY + c.Y;
                if (y >= 0 && y < Board.VisibleHeight)
                    lockGlow[x, y] = LockGlowDuration;
            }
        }

        /// <summary>Shards from every cell of the cleared rows, in the colours they had.</summary>
        public void BurstRows(IReadOnlyList<int> rows)
        {
            int perCell = rows.Count >= 4 ? 5 : 3;
            foreach (int row in rows)
            {
                if (row >= Board.VisibleHeight)
                    continue;
                for (int x = 0; x < Board.Width; x++)
                {
                    int value = rowSnapshot[x, row];
                    Color color = value == Board.Empty ? Color.white : PieceColors[value];
                    Effects.Burst(GridPosition(x, row), color, perCell, rows.Count >= 4 ? 13f : 9f);
                }
            }
        }

        public void HardDropTrail(Game game, int fromY, int toY)
        {
            Color color = PieceColors[(int)game.CurrentType];
            var columnTop = new Dictionary<int, int>();
            foreach (Cell c in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
            {
                if (!columnTop.TryGetValue(c.X, out int top) || c.Y > top)
                    columnTop[c.X] = c.Y;
            }
            foreach (KeyValuePair<int, int> column in columnTop)
            {
                float x = BoardLeft + game.CurrentX + column.Key + 0.5f;
                float top = Mathf.Min(BoardBottom + fromY + column.Value + 1f, -BoardBottom);
                float bottom = BoardBottom + toY + column.Value + 1f;
                Effects.Streak(x, top, bottom, color);
            }
        }

        public void LandingDust(Game game, int landedY)
        {
            Color color = PieceColors[(int)game.CurrentType];
            foreach (Cell c in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
            {
                int y = landedY + c.Y;
                if (y < Board.VisibleHeight)
                    Effects.Burst(GridPosition(game.CurrentX + c.X, y), color, 2, 4f);
            }
        }

        /// <summary>Rows of stack height as a share of the danger zone at the top, from 0 to 1.</summary>
        public static float Tension(Game game)
        {
            for (int y = Board.VisibleHeight - 1; y >= 0; y--)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    if (game.Board.Get(x, y) != Board.Empty)
                        return Mathf.Clamp01((y + 1 - (Board.VisibleHeight - DangerRows)) / (float)DangerRows);
                }
            }
            return 0f;
        }

        public void Render(Game game, float deltaTime)
        {
            UpdateTheme(game, deltaTime);
            if (game.State == GameState.GameOver)
                gameOverClock += deltaTime;
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.VisibleHeight; y++)
                    lockGlow[x, y] = Mathf.Max(0f, lockGlow[x, y] - deltaTime);
            DrawSettledBlocks(game);
            if (!game.IsFinished)
            {
                Color pieceColor = PieceColors[(int)game.CurrentType];
                Color ghost = new Color(pieceColor.r, pieceColor.g, pieceColor.b, 0.28f);
                DrawActive(game, game.GhostY, ghost);
                DrawActive(game, game.CurrentY, pieceColor);
            }

            for (int slot = 0; slot < previews.Length; slot++)
            {
                Vector2 center = new Vector2(RightPanelX, 6.2f - slot * 3f);
                DrawPreview(previews[slot], game.GetUpcoming(slot), center, true);
            }

            bool hasHold = game.HeldType.HasValue;
            foreach (SpriteRenderer cell in holdCells)
                cell.enabled = hasHold;
            if (hasHold)
                DrawPreview(holdCells, game.HeldType.Value, new Vector2(LeftPanelX, 6.2f), game.CanHold);

            UpdateSpecialVisuals(game);
            UpdateFlashes(deltaTime);
            Effects.Tick(deltaTime);
        }

        void UpdateTheme(Game game, float deltaTime)
        {
            if (themeFade < 1f)
            {
                themeFade = Mathf.Min(1f, themeFade + deltaTime / ThemeFadeSeconds);
                backgroundColor = Color.Lerp(fadeBackground, targetTheme.Background, themeFade);
                wellColor = Color.Lerp(fadeWell, targetTheme.Well, themeFade);
                emptyCellColor = Color.Lerp(fadeEmptyCell, targetTheme.EmptyCell, themeFade);
                panelColor = Color.Lerp(fadePanel, targetTheme.Panel, themeFade);
                accentColor = Color.Lerp(fadeAccent, targetTheme.Accent, themeFade);
                foreach (SpriteRenderer panel in panels)
                    panel.color = panelColor;
            }

            // The well glows red and pulses as the stack nears the top.
            float tension = game.State == GameState.Playing ? Tension(game) : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (4f + tension * 5f));
            Color well = Color.Lerp(wellColor, DangerColor, tension * (0.45f + 0.55f * pulse));
            wellRenderer.color = well;
        }

        void DrawSettledBlocks(Game game)
        {
            // After game over the stack goes grey row by row, from the top down.
            int greyFrom = game.State == GameState.GameOver
                ? Board.VisibleHeight - Mathf.FloorToInt(gameOverClock * 32f)
                : int.MaxValue;
            for (int x = 0; x < Board.Width; x++)
            {
                for (int y = 0; y < Board.VisibleHeight; y++)
                {
                    int value = game.Board.Get(x, y);
                    if (value == Board.Empty)
                    {
                        grid[x, y].color = emptyCellColor;
                        continue;
                    }
                    Color color = PieceColors[value];
                    if (y >= greyFrom)
                    {
                        float grey = color.grayscale * 0.55f;
                        color = new Color(grey, grey, grey * 1.05f, 1f);
                    }
                    else if (lockGlow[x, y] > 0f)
                    {
                        color = Color.Lerp(color, Color.white, lockGlow[x, y] / LockGlowDuration * 0.75f);
                    }
                    grid[x, y].color = color;
                }
            }
        }

        void DrawActive(Game game, int originY, Color color)
        {
            foreach (Cell c in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
            {
                int x = game.CurrentX + c.X;
                int y = originY + c.Y;
                if (y < Board.VisibleHeight)
                    grid[x, y].color = color;
            }
        }

        void DrawPreview(SpriteRenderer[] cells, PieceType type, Vector2 center, bool active)
        {
            Cell[] shape = PieceShapes.Get(type, 0);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Cell c in shape)
            {
                minX = Mathf.Min(minX, c.X);
                maxX = Mathf.Max(maxX, c.X);
                minY = Mathf.Min(minY, c.Y);
                maxY = Mathf.Max(maxY, c.Y);
            }

            Color color = active ? PieceColors[(int)type] : DisabledColor;
            for (int i = 0; i < cells.Length; i++)
            {
                float x = (shape[i].X - (minX + maxX) / 2f) * PreviewScale;
                float y = (shape[i].Y - (minY + maxY) / 2f) * PreviewScale;
                cells[i].transform.localPosition = new Vector3(center.x + x, center.y + y, 0f);
                cells[i].color = color;
            }
        }

        void UpdateFlashes(float deltaTime)
        {
            for (int row = 0; row < flashTimers.Length; row++)
            {
                bool visible = flashTimers[row] > 0f;
                flashStrips[row].enabled = visible;
                if (!visible)
                    continue;
                flashStrips[row].color = new Color(1f, 0.96f, 0.85f, flashTimers[row] / FlashDuration * 0.85f);
                flashTimers[row] -= deltaTime;
            }
        }

        void CreateStaticBackdrop()
        {
            wellRenderer = CreateRenderer("Well", plainSprite, Vector2.zero, 1f, 1);
            wellRenderer.transform.localScale = new Vector3(Board.Width + 0.3f, Board.VisibleHeight + 0.3f, 1f);
            wellRenderer.color = wellColor;

            CreatePanel(new Vector2(RightPanelX, 3.2f), new Vector2(5f, 10.6f));
            CreatePanel(new Vector2(LeftPanelX, 6.2f), new Vector2(5f, 4.6f));
            CreatePanel(new Vector2(LeftPanelX, -1.05f), new Vector2(5f, 9.1f));
        }

        void CreatePanel(Vector2 center, Vector2 size)
        {
            SpriteRenderer panel = CreateRenderer("Panel", plainSprite, center, 1f, 0);
            panel.transform.localScale = new Vector3(size.x, size.y, 1f);
            panel.color = panelColor;
            panels.Add(panel);
        }

        SpriteRenderer[] CreateCellGroup()
        {
            var cells = new SpriteRenderer[4];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = CreateRenderer("PreviewCell", tileSprite, Vector2.zero, PreviewScale * CellScale, 2);
            return cells;
        }

        SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Vector2 position, float scale, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }

        public static Vector2 GridPosition(int x, int y)
        {
            return new Vector2(BoardLeft + x + 0.5f, BoardBottom + y + 0.5f);
        }

        static Sprite CreatePlainSprite()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
        }

        // Rounded tile with a soft top light, so blocks read as glazed ceramic.
        static Sprite CreateTileSprite()
        {
            const int size = 48;
            const float radius = 11f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    float shade = Mathf.Lerp(0.78f, 1f, (y + 0.5f) / size);
                    texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        static Color Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }
    }
}
