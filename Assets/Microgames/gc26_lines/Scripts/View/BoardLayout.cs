using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>
    /// Where everything goes on the game screen, following the prototype's proportions
    /// (docs/prototype/lines-prototype.html) and the GDD rule for 16:9 and 20:9 screens:
    /// the board takes the full width minus small margins; the score/preview sit above it and
    /// Undo/Reset below; on tall screens the extra height is split evenly, so the board stays centred.
    ///
    /// Measurements are in "units" = 1% of the content width (the prototype's cqw), so the
    /// layout scales with the phone. All rectangles are in screen pixels, origin bottom-left
    /// (like Screen.safeArea). The camera is set so that 1 world unit = 1 board cell.
    /// </summary>
    public readonly struct BoardLayout
    {
        // Prototype measurements, in units (cqw).
        public const float SidePadding = 4f;
        public const float BoardSize = 92f;
        public const float BoardInset = 1.2f;   // board edge to the first cell
        public const float CellGap = 0.7f;
        public const float HudHeight = 14f;     // score + next balls
        public const float Gap = 6f;            // between HUD, board and buttons
        public const float ActionsHeight = 12f; // Undo / Reset
        public const float TopPadding = 15f;    // room for the back button
        public const float BottomPadding = 6f;
        public const float BackButtonTop = 3.5f;
        public const float BackButtonSize = 10f;
        public const float BlockHeight = HudHeight + Gap + BoardSize + Gap + ActionsHeight;           // 130
        public const float MinScreenHeight = TopPadding + BlockHeight + BottomPadding;                // 151

        /// <summary>Pixels per unit (1% of the content width).</summary>
        public float Unit { get; }
        public Vector2 ScreenSize { get; }
        public Rect SafeArea { get; }
        public Rect Hud { get; }
        public Rect Board { get; }
        public Rect Actions { get; }
        public Rect BackButton { get; }
        /// <summary>The gear (Settings) in the top-right corner: the house button's mirror image across the board.</summary>
        public Rect SettingsButton => new Rect(Board.xMax - BackButton.width, BackButton.y, BackButton.width, BackButton.height);
        /// <summary>Distance between neighbouring cell centres, in pixels.</summary>
        public float CellPitch { get; }

        /// <summary>Camera orthographic size that makes 1 world unit = 1 cell.</summary>
        public float OrthographicSize => ScreenSize.y / (2f * CellPitch);
        /// <summary>Board centre in world units (camera at the origin).</summary>
        public Vector2 BoardCenterWorld => ScreenToWorld(Board.center);
        /// <summary>The board's outer size in world units (cells are 1 unit apart).</summary>
        public float BoardSizeWorld => Board.width / CellPitch;
        /// <summary>A cell's visible size in world units (pitch minus the gap).</summary>
        public float CellSizeWorld => 1f - CellGap * Unit / CellPitch;
        public float BoardCornerWorld => 2.6f * Unit / CellPitch;
        public float CellCornerWorld => 1.1f * Unit / CellPitch;

        BoardLayout(float unit, Vector2 screen, Rect safe, Rect hud, Rect board, Rect actions, Rect back)
        {
            Unit = unit;
            ScreenSize = screen;
            SafeArea = safe;
            Hud = hud;
            Board = board;
            Actions = actions;
            BackButton = back;
            CellPitch = (BoardSize - 2f * BoardInset) * unit / 9f;
        }

        public static BoardLayout Compute(Vector2 screen, Rect safeArea)
        {
            // Normally the width decides; on unusually wide screens (tablets) the height does.
            float unit = Mathf.Min(safeArea.width / 100f, safeArea.height / MinScreenHeight);
            float contentLeft = safeArea.x + (safeArea.width - 100f * unit) / 2f;
            float safeTop = safeArea.yMax;

            // The block (HUD, board, buttons) is centred in the height left after the paddings.
            float free = safeArea.height - (TopPadding + BottomPadding + BlockHeight) * unit;
            float blockTop = safeTop - TopPadding * unit - free / 2f;

            float left = contentLeft + SidePadding * unit;
            float width = BoardSize * unit;
            var hud = new Rect(left, blockTop - HudHeight * unit, width, HudHeight * unit);
            float boardTop = hud.y - Gap * unit;
            var board = new Rect(left, boardTop - BoardSize * unit, width, BoardSize * unit);
            var actions = new Rect(left + 2f * unit, board.y - Gap * unit - ActionsHeight * unit, width - 4f * unit, ActionsHeight * unit);
            var back = new Rect(left, safeTop - (BackButtonTop + BackButtonSize) * unit, BackButtonSize * unit, BackButtonSize * unit);

            return new BoardLayout(unit, screen, safeArea, hud, board, actions, back);
        }

        /// <summary>Converts a screen position (pixels, origin bottom-left) to world units.</summary>
        public Vector2 ScreenToWorld(Vector2 pixels) =>
            new Vector2((pixels.x - ScreenSize.x / 2f) / CellPitch, (pixels.y - ScreenSize.y / 2f) / CellPitch);

        /// <summary>
        /// The board cell under a screen position (pixels, origin bottom-left). Each cell owns the
        /// whole square around it, gaps included. False when the position is outside the board.
        /// </summary>
        public bool TryGetCell(Vector2 pixels, out GC26.Lines.Core.Cell cell)
        {
            var local = ScreenToWorld(pixels) - BoardCenterWorld;
            float middle = (GC26.Lines.Core.GameConfig.BoardSize - 1) / 2f;
            int column = Mathf.FloorToInt(local.x + middle + 0.5f);
            int row = Mathf.FloorToInt(middle - local.y + 0.5f);
            cell = new GC26.Lines.Core.Cell(column, row);
            return column >= 0 && column < GC26.Lines.Core.GameConfig.BoardSize
                && row >= 0 && row < GC26.Lines.Core.GameConfig.BoardSize;
        }

        /// <summary>Converts a world position back to screen pixels.</summary>
        public Vector2 WorldToScreen(Vector2 world) =>
            new Vector2(world.x * CellPitch + ScreenSize.x / 2f, world.y * CellPitch + ScreenSize.y / 2f);
    }
}
