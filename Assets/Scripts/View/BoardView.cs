using System.Collections.Generic;
using Lines.Core;
using UnityEngine;

namespace Lines.View
{
    /// <summary>One ball on the board: a sprite that can be recoloured and reused.</summary>
    public sealed class BallView : MonoBehaviour
    {
        /// <summary>Ball size compared to a cell (the prototype draws balls at 80%).</summary>
        public const float SizeInCell = 0.8f;

        SpriteRenderer spriteRenderer;
        public BallColor Color { get; private set; }

        public static BallView Create(Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BallView>();
            view.spriteRenderer = go.AddComponent<SpriteRenderer>();
            view.spriteRenderer.sortingOrder = BoardView.BallOrder;
            return view;
        }

        public void SetColor(BallColor color)
        {
            Color = color;
            spriteRenderer.sprite = SpriteFactory.Ball(color);
        }
    }

    /// <summary>
    /// Draws the board and its balls from code. It only shows what the Core <see cref="Board"/> says;
    /// it never changes the game. Cells sit 1 world unit apart; (0,0) is the board centre.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const int BoardOrder = 0, CellOrder = 1, BallOrder = 3;

        SpriteRenderer background;
        readonly SpriteRenderer[] cells = new SpriteRenderer[GameConfig.BoardSize * GameConfig.BoardSize];
        readonly Dictionary<Cell, BallView> balls = new Dictionary<Cell, BallView>();
        readonly Stack<BallView> pool = new Stack<BallView>();
        Transform ballRoot, poolRoot;

        public static BoardView Create(Transform parent = null)
        {
            var go = new GameObject("Board");
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.Build();
            return view;
        }

        void Build()
        {
            background = NewSprite("Background", transform, Palette.Board, BoardOrder);
            var cellRoot = new GameObject("Cells").transform;
            cellRoot.SetParent(transform, false);
            for (int row = 0; row < GameConfig.BoardSize; row++)
            for (int column = 0; column < GameConfig.BoardSize; column++)
            {
                var cell = new Cell(column, row);
                var sr = NewSprite($"Cell {column},{row}", cellRoot, Palette.Cell, CellOrder);
                sr.transform.localPosition = CellToLocal(cell);
                cells[row * GameConfig.BoardSize + column] = sr;
            }
            ballRoot = new GameObject("Balls").transform;
            ballRoot.SetParent(transform, false);
            poolRoot = new GameObject("Pool").transform;
            poolRoot.SetParent(transform, false);
            poolRoot.gameObject.SetActive(false);
        }

        /// <summary>Moves and sizes the board to fit the screen layout.</summary>
        public void ApplyLayout(BoardLayout layout)
        {
            transform.localPosition = layout.BoardCenterWorld;
            background.sprite = SpriteFactory.RoundRect(layout.BoardCornerWorld);
            background.size = Vector2.one * layout.BoardSizeWorld;
            var cellSprite = SpriteFactory.RoundRect(layout.CellCornerWorld);
            foreach (var sr in cells)
            {
                sr.sprite = cellSprite;
                sr.size = Vector2.one * layout.CellSizeWorld;
            }
        }

        /// <summary>Position of a cell's centre inside the board (row 0 is the top row).</summary>
        public static Vector3 CellToLocal(Cell cell)
        {
            float middle = (GameConfig.BoardSize - 1) / 2f;
            return new Vector3(cell.Column - middle, middle - cell.Row, 0f);
        }

        /// <summary>Shows exactly the balls on <paramref name="board"/>, reusing ball objects.</summary>
        public void ShowBoard(Board board)
        {
            foreach (var ball in balls.Values) Release(ball);
            balls.Clear();
            for (int row = 0; row < board.Size; row++)
            for (int column = 0; column < board.Size; column++)
            {
                var cell = new Cell(column, row);
                var color = board.Get(cell);
                if (color != BallColor.None) balls[cell] = Take(cell, color);
            }
        }

        public BallView BallAt(Cell cell) => balls.TryGetValue(cell, out var ball) ? ball : null;
        public int BallCount => balls.Count;
        public int PooledCount => pool.Count;

        BallView Take(Cell cell, BallColor color)
        {
            var ball = pool.Count > 0 ? pool.Pop() : BallView.Create(ballRoot);
            ball.transform.SetParent(ballRoot, false);
            ball.transform.localPosition = CellToLocal(cell);
            ball.transform.localScale = Vector3.one * BallView.SizeInCell;
            ball.SetColor(color);
            return ball;
        }

        void Release(BallView ball)
        {
            Tween.Kill(ball.transform);
            ball.transform.SetParent(poolRoot, false);
            pool.Push(ball);
        }

        static SpriteRenderer NewSprite(string name, Transform parent, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sprite = SpriteFactory.RoundRect(0.1f);
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
