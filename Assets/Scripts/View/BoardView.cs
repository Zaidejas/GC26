using System.Collections.Generic;
using Lines.Core;
using UnityEngine;

namespace Lines.View
{
    /// <summary>One ball on the board (a creature or a glossy ball): a sprite that can be changed and reused.</summary>
    public sealed class BallView : MonoBehaviour
    {
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

        /// <summary>Shows <paramref name="color"/> using <paramref name="pieces"/> and sizes it to fit a cell.</summary>
        public void SetColor(BallColor color, PieceSet pieces)
        {
            Color = color;
            var sprite = pieces.SpriteFor(color);
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = UnityEngine.Color.white;
            var size = sprite.bounds.size;
            transform.localScale = Vector3.one * (pieces.SizeInCell / Mathf.Max(size.x, size.y));
            FullScale = transform.localScale;
        }

        /// <summary>The scale at which the piece fills its spot in the cell.</summary>
        public Vector3 FullScale { get; private set; } = Vector3.one;

        public void SetAlpha(float alpha) => spriteRenderer.color = new UnityEngine.Color(1f, 1f, 1f, alpha);
    }

    /// <summary>
    /// Draws the board and its balls from code. It only shows what the Core <see cref="Board"/> says;
    /// it never changes the game. Cells sit 1 world unit apart; (0,0) is the board centre.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const int BoardOrder = 0, CellOrder = 1, DotOrder = 2, BallOrder = 3;

        SpriteRenderer background;
        readonly SpriteRenderer[] cells = new SpriteRenderer[GameConfig.BoardSize * GameConfig.BoardSize];
        readonly Dictionary<Cell, BallView> balls = new Dictionary<Cell, BallView>();
        readonly Stack<BallView> pool = new Stack<BallView>();
        Transform ballRoot, poolRoot;
        PieceSet pieces = PieceSet.Balls();

        public PieceSet Pieces => pieces;

        /// <summary>Switches between creatures and balls; balls already on the board change at once.</summary>
        public void SetPieces(PieceSet set)
        {
            pieces = set ?? PieceSet.Balls();
            foreach (var ball in balls.Values) ball.SetColor(ball.Color, pieces);
            RedrawDots();
        }

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

        // ---- Preview dots: small pieces in the cells where the next balls will appear ----

        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        readonly List<(Cell cell, BallColor color)> dotData = new List<(Cell, BallColor)>();

        /// <summary>Shows a small piece in each preview cell that is still empty.</summary>
        public void ShowPreviewDots(Preview preview, Board board)
        {
            dotData.Clear();
            for (int k = 0; k < preview.Cells.Count && k < preview.Colors.Count; k++)
                if (board.IsEmpty(preview.Cells[k]))
                    dotData.Add((preview.Cells[k], preview.Colors[k]));
            RedrawDots();
        }

        /// <summary>Hides the dot in <paramref name="cell"/> (a ball has just moved onto it).</summary>
        public void HideDotAt(Cell cell)
        {
            dotData.RemoveAll(d => d.cell == cell);
            RedrawDots();
        }

        public IEnumerable<Cell> DotCells
        {
            get { foreach (var d in dotData) yield return d.cell; }
        }

        void RedrawDots()
        {
            while (dots.Count < dotData.Count)
            {
                var sr = new GameObject("Preview dot").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sortingOrder = DotOrder;
                dots.Add(sr);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                bool show = i < dotData.Count;
                dots[i].gameObject.SetActive(show);
                if (!show) continue;
                var (cell, color) = dotData[i];
                var sprite = pieces.SpriteFor(color);
                dots[i].sprite = sprite;
                dots[i].color = new Color(1f, 1f, 1f, pieces.UsesCreatures ? 0.9f : 0.95f);
                dots[i].transform.localPosition = CellToLocal(cell);
                var size = sprite.bounds.size;
                dots[i].transform.localScale = Vector3.one * (pieces.DotSizeInCell / Mathf.Max(size.x, size.y));
            }
        }

        /// <summary>Puts one new ball into <paramref name="cell"/> (used by the spawn animation).</summary>
        public BallView AddBall(Cell cell, BallColor color)
        {
            if (balls.TryGetValue(cell, out var existing)) Release(existing);
            var ball = Take(cell, color);
            balls[cell] = ball;
            return ball;
        }

        /// <summary>Takes the ball out of <paramref name="cell"/> (after its pop animation).</summary>
        public void RemoveBall(Cell cell)
        {
            if (!balls.TryGetValue(cell, out var ball)) return;
            balls.Remove(cell);
            Release(ball);
        }

        /// <summary>Records that the ball at <paramref name="from"/> now belongs to <paramref name="to"/> (after its move animation).</summary>
        public void MoveBall(Cell from, Cell to)
        {
            if (!balls.TryGetValue(from, out var ball)) return;
            balls.Remove(from);
            balls[to] = ball;
            ball.transform.localPosition = CellToLocal(to);
        }
        public int BallCount => balls.Count;
        public int PooledCount => pool.Count;

        BallView Take(Cell cell, BallColor color)
        {
            var ball = pool.Count > 0 ? pool.Pop() : BallView.Create(ballRoot);
            ball.transform.SetParent(ballRoot, false);
            ball.transform.localPosition = CellToLocal(cell);
            ball.SetColor(color, pieces);
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
