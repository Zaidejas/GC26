using System.Collections.Generic;
using System.Linq;
using GC26.Lines.Core;
using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>
    /// One ball on the board (a mushroom or a glossy ball): a sprite that can be changed and reused.
    /// The ball's own transform is moved and scaled by the animations; the picture hangs on a hinge at its
    /// foot, so it can sway on the spot without disturbing them (GDD → Art → game screen life).
    /// </summary>
    public sealed class BallView : MonoBehaviour
    {
        // The mushrooms stand on a foot about 38 % of their height below the middle (prototype: transform-origin 50% 85–88%).
        const float FootBelowCentre = 0.38f;

        SpriteRenderer spriteRenderer;
        Transform hinge;
        public BallColor Color { get; private set; }

        /// <summary>The picture (a child of the ball, on its foot hinge).</summary>
        public SpriteRenderer Renderer => spriteRenderer;

        /// <summary>False while the ball is selected or moving: then it doesn't sway.</summary>
        public bool Swaying { get; set; } = true;

        /// <summary>The current sway, in degrees (positive leans left).</summary>
        public float SwayAngle => hinge.localEulerAngles.z > 180f ? hinge.localEulerAngles.z - 360f : hinge.localEulerAngles.z;

        public static BallView Create(Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BallView>();
            view.hinge = new GameObject("Hinge").transform;
            view.hinge.SetParent(go.transform, false);
            view.spriteRenderer = new GameObject("Picture").AddComponent<SpriteRenderer>();
            view.spriteRenderer.transform.SetParent(view.hinge, false);
            view.spriteRenderer.sortingOrder = BoardView.BallOrder;
            return view;
        }

        /// <summary>Leans the picture by <paramref name="degrees"/> around its foot.</summary>
        public void SetSway(float degrees) => hinge.localRotation = Quaternion.Euler(0f, 0f, degrees);

        /// <summary>Shows <paramref name="color"/> using <paramref name="pieces"/> and sizes it to fit a cell.</summary>
        public void SetColor(BallColor color, PieceSet pieces)
        {
            Color = color;
            var sprite = pieces.SpriteFor(color);
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = UnityEngine.Color.white;
            var size = sprite.bounds.size;
            float foot = FootBelowCentre * size.y; // in the ball's own units (its scale applies on top)
            hinge.localPosition = new Vector3(0f, -foot, 0f);
            spriteRenderer.transform.localPosition = new Vector3(0f, foot, 0f);
            SetSway(0f);
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
        public const int GlowOrder = -2, RimOrder = -1, BoardOrder = 0, CellOrder = 1, CellLightOrder = 2, DotOrder = 3, BallOrder = 4;

        // Prototype: a 0.45cqw rim and a soft glow just outside the board; a light at 72% of each cell.
        // The glow sprite's edge is its rounded corner plus the fade; only the fade sticks out past the board.
        const float RimInCells = 0.05f, GlowEdgeInCells = 0.9f, CellLightSize = 0.85f;

        SpriteRenderer background, rim, glow;
        readonly SpriteRenderer[] cells = new SpriteRenderer[GameConfig.BoardSize * GameConfig.BoardSize];
        readonly SpriteRenderer[] cellLights = new SpriteRenderer[GameConfig.BoardSize * GameConfig.BoardSize];
        readonly Dictionary<Cell, BallView> balls = new Dictionary<Cell, BallView>();

        // ---- Sway: every ball rocks gently on its foot, each at its own rhythm (teacher's feedback, prototype) ----
        public const float SwayDegrees = 3f, SwayHalfPeriod = 3f, SwayStagger = 0.37f;

        /// <summary>The selected ball (it bounces instead of swaying), or null.</summary>
        public Cell? Held { get; set; }

        float swayClock;

        void Update()
        {
            if (Application.isPlaying) AdvanceSway(Time.deltaTime);
        }

        /// <summary>
        /// Moves the sway on by <paramref name="seconds"/> (every frame; tests call it directly). The phase comes
        /// from one clock and the ball's cell, so redrawing the board never makes a ball jump.
        /// </summary>
        public void AdvanceSway(float seconds)
        {
            swayClock += seconds;
            foreach (var pair in balls)
            {
                var ball = pair.Value;
                if (!ball.Swaying || Held == pair.Key) { ball.SetSway(0f); continue; }
                float phase = (swayClock + (pair.Key.Row * 9 + pair.Key.Column) * SwayStagger) % (2f * SwayHalfPeriod) / SwayHalfPeriod;
                ball.SetSway(-SwayDegrees * Mathf.Cos(Mathf.PI * phase)); // eases in and out at each end, like the prototype
            }
        }
        readonly Stack<BallView> pool = new Stack<BallView>();
        Transform ballRoot, poolRoot;
        PieceSet pieces = PieceSet.Balls();

        public PieceSet Pieces => pieces;

        /// <summary>Switches between mushrooms and balls; balls already on the board change at once.</summary>
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
            glow = NewSprite("Glow", transform, Palette.BoardGlow, GlowOrder);
            rim = NewSprite("Rim", transform, Palette.BoardRim, RimOrder);
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
                var light = new GameObject("Light").AddComponent<SpriteRenderer>();
                light.transform.SetParent(sr.transform, false);
                light.transform.localPosition = new Vector3(0f, 0.12f, 0f); // the prototype's light sits a little above the middle
                light.sprite = SpriteFactory.SoftDisc();
                light.color = Palette.CellLight;
                light.sortingOrder = CellLightOrder;
                cellLights[row * GameConfig.BoardSize + column] = light;
            }
            // The board's own parts (not balls or previews), with their colours, for the entrance fade.
            frame.Add((glow, glow.color));
            frame.Add((rim, rim.color));
            frame.Add((background, background.color));
            foreach (var sr in cells) frame.Add((sr, sr.color));
            foreach (var light in cellLights) frame.Add((light, light.color));
            fireflies = Fireflies.Create(transform);
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
            rim.sprite = SpriteFactory.RoundRect(layout.BoardCornerWorld + RimInCells);
            rim.size = Vector2.one * (layout.BoardSizeWorld + 2f * RimInCells);
            glow.sprite = SpriteFactory.SoftGlow(GlowEdgeInCells);
            glow.size = Vector2.one * (layout.BoardSizeWorld + 2f * GlowEdgeInCells * SpriteFactory.SoftGlowFadeShare);
            var cellSprite = SpriteFactory.RoundRect(layout.CellCornerWorld);
            foreach (var sr in cells)
            {
                sr.sprite = cellSprite;
                sr.size = Vector2.one * layout.CellSizeWorld;
            }
            foreach (var light in cellLights)
                light.transform.localScale = Vector3.one * (layout.CellSizeWorld * CellLightSize);
            fireflies.ApplyLayout(layout);
        }

        // ---- Fireflies and the entrance (teacher's feedback, prototype) ----
        readonly List<(SpriteRenderer sr, Color color)> frame = new List<(SpriteRenderer, Color)>();
        Fireflies fireflies;

        /// <summary>The fireflies drifting behind the board.</summary>
        public Fireflies Fireflies => fireflies;

        public const float EntranceBoardSeconds = 0.35f, EntranceStartScale = 0.94f, EntrancePopSeconds = 0.3f;
        public const float EntranceFirstPop = 0.22f, EntranceMaxGap = 0.055f, EntranceSpread = 0.6f, EntranceDotsAfter = 0.08f;

        /// <summary>
        /// When the game screen opens: the board fades in and grows from 94 % into place (0.35 s), then the balls
        /// pop up one after another from top-left to bottom-right, the previews last (about 0.6–1 s in all).
        /// </summary>
        public void PlayEntrance()
        {
            Tween.Kill(this);
            Tween.To(this, EntranceBoardSeconds, p =>
            {
                transform.localScale = Vector3.one * Mathf.Lerp(EntranceStartScale, 1f, p);
                foreach (var (sr, color) in frame) sr.color = new Color(color.r, color.g, color.b, color.a * p);
            }, Ease.OutQuad);

            var ordered = balls.OrderBy(b => b.Key.Row * GameConfig.BoardSize + b.Key.Column).Select(b => b.Value).ToList();
            float gap = Mathf.Min(EntranceMaxGap, EntranceSpread / Mathf.Max(1, ordered.Count));
            for (int k = 0; k < ordered.Count; k++)
            {
                var ball = ordered[k];
                Tween.Kill(ball.transform); // replaces a new game's grow-in, so nothing pops twice
                Tween.Kill(ball);
                var full = ball.FullScale;
                ball.transform.localScale = Vector3.zero;
                float delay = EntranceFirstPop + k * gap;
                var t = ball.transform;
                Tween.To(ball, delay + EntrancePopSeconds, p =>
                {
                    float q = Mathf.Clamp01((p * (delay + EntrancePopSeconds) - delay) / EntrancePopSeconds);
                    t.localScale = full * Easing.Evaluate(Ease.OutBack, q);
                }).OnComplete(() => { if (t != null) t.localScale = full; });
            }

            float dotsDelay = EntranceFirstPop + ordered.Count * gap + EntranceDotsAfter;
            foreach (var dot in dots)
            {
                if (!dot.gameObject.activeSelf) continue;
                var ghost = PieceSet.GhostColor;
                dot.color = new Color(ghost.r, ghost.g, ghost.b, 0f);
                Tween.Kill(dot);
                Tween.To(dot, dotsDelay + EntrancePopSeconds, p =>
                {
                    float q = Mathf.Clamp01((p * (dotsDelay + EntrancePopSeconds) - dotsDelay) / EntrancePopSeconds);
                    dot.color = new Color(ghost.r, ghost.g, ghost.b, ghost.a * q);
                }).OnComplete(() => { if (dot != null) dot.color = ghost; });
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
                dots[i].color = PieceSet.GhostColor;
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
