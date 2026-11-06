using System;
using System.Collections.Generic;
using Lines.Core;
using Lines.UI;
using Lines.View;
using UnityEngine;

namespace Lines.Game
{
    /// <summary>
    /// Shows the turn on the <see cref="BoardView"/> and the <see cref="HudView"/> (score, next pieces).
    /// Animations: bounce, rolling along the path, score and preview, grow-in, pop and burst,
    /// and the blocked-move shake with the "No path" alert.
    /// </summary>
    public sealed class BoardPresenter : IBoardPresenter
    {
        // Prototype: the selected ball bounces about a fifth of a cell, 0.36 s up and 0.36 s down.
        public const float BounceHeight = 0.17f;
        public const float BounceHalfPeriod = 0.36f;
        public const float SecondsPerCell = 0.04f;
        // Prototype: a blocked move shakes the ball left and right (up to 14 % of a cell) for 0.32 s.
        public const float ShakeStrength = 0.14f;
        public const float ShakeSeconds = 0.32f;
        public const string NoPathText = "No path";

        readonly BoardView board;
        readonly HudView hud; // optional (tests can leave it out)
        TweenHandle bounce, shake;
        Cell? selected;

        public BoardPresenter(BoardView board, HudView hud = null)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.hud = hud;
        }

        /// <summary>
        /// Redraws balls, preview dots, score and next pieces from the game. During a turn the game already
        /// holds the result, so this is only called at the start and at the end of each turn.
        /// </summary>
        public void ShowState(GameState game)
        {
            StopShake();
            StopBounce();
            selected = null;
            board.ShowBoard(game.Board);
            board.ShowPreviewDots(game.Preview, game.Board);
            if (hud != null)
            {
                hud.SetScore(game.Score);
                hud.SetPreview(game.Preview.Colors, board.Pieces);
            }
        }

        /// <summary>The selected ball bounces; the previously selected one settles back into its cell.</summary>
        public void ShowSelected(Cell? cell)
        {
            StopShake();
            StopBounce();
            selected = cell;
            StartBounce();
        }

        void StartBounce()
        {
            if (selected is Cell c && board.BallAt(c) is BallView ball)
                bounce = Tween.Bounce(ball.transform, BounceHeight, BounceHalfPeriod);
        }

        void StopBounce()
        {
            bounce?.Kill(); // puts the ball back where it was
            bounce = null;
        }

        void StopShake()
        {
            shake?.Kill(complete: true); // a shake always ends where it started
            shake = null;
        }

        /// <summary>
        /// The selected ball can't reach the tapped cell: it shakes, "No path" shows over the board,
        /// and the ball stays selected (it bounces again after the shake). Input is not locked.
        /// </summary>
        public void ShowBlocked(Cell from, Cell to)
        {
            hud?.ShowAlert(NoPathText);
            var ball = board.BallAt(from);
            if (ball == null) return;
            StopShake();
            StopBounce();
            shake = Tween.Shake(ball.transform, ShakeStrength, ShakeSeconds).OnComplete(() =>
            {
                shake = null;
                if (selected == from && bounce == null) StartBounce(); // still selected: bounce again
            });
        }

        /// <summary>The ball rolls along the path at an even speed, one cell every 40 ms (as in the prototype).</summary>
        public void PlayMove(IReadOnlyList<Cell> path, Action done)
        {
            var from = path[0];
            var to = path[path.Count - 1];
            var ball = board.BallAt(from);
            if (ball == null || path.Count < 2) { board.MoveBall(from, to); board.HideDotAt(to); done(); return; }

            var points = new Vector3[path.Count - 1];
            for (int i = 1; i < path.Count; i++) points[i - 1] = BoardView.CellToLocal(path[i]);
            Tween.MovePath(ball.transform, points, SecondsPerCell).OnComplete(() =>
            {
                board.MoveBall(from, to);
                board.HideDotAt(to); // the ball now covers that dot; its ball will appear elsewhere
                done();
            });
        }

        // Prototype timings (docs/prototype/lines-prototype.html).
        public const float GrowSeconds = 0.22f;      // new pieces grow in
        public const float SpawnSeconds = 0.24f;     // the turn continues after this
        public const float PopSeconds = 0.24f;       // cleared pieces swell, then shrink away
        public const float ClearSeconds = 0.26f;     // the turn continues (and the score updates) after this
        public const float ParticleSeconds = 0.45f;  // the burst keeps flying a little longer, without holding the turn
        public const int ParticlesPerPiece = 7;

        /// <summary>New pieces grow in from nothing; their preview dots go away.</summary>
        public void PlaySpawn(IReadOnlyList<Cell> cells, IReadOnlyList<BallColor> colors, Action done)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                board.HideDotAt(cells[i]);
                var ball = board.AddBall(cells[i], colors[i]);
                var full = ball.FullScale;
                ball.transform.localScale = Vector3.zero;
                Tween.Scale(ball.transform, full, GrowSeconds, Ease.OutQuad);
            }
            Tween.Delay(SpawnSeconds, done);
        }

        /// <summary>
        /// Cleared pieces swell to 118 %, then shrink and fade, while 7 particles in their color burst out.
        /// Then the pieces are removed and the points float up next to the score.
        /// </summary>
        public void PlayClear(IReadOnlyList<Cell> cells, int points, Action done)
        {
            foreach (var cell in cells)
            {
                var ball = board.BallAt(cell);
                if (ball == null) continue;
                Burst(cell, board.Pieces.ParticleColor(ball.Color));
                Pop(ball);
            }
            Tween.Delay(ClearSeconds, () =>
            {
                foreach (var cell in cells) board.RemoveBall(cell);
                hud?.AddScore(points);
                done();
            });
        }

        static void Pop(BallView ball)
        {
            var t = ball.transform;
            var full = ball.FullScale;
            Tween.To(t, PopSeconds, p =>
            {
                float size = p < 0.4f ? Mathf.Lerp(1f, 1.18f, p / 0.4f) : Mathf.Lerp(1.18f, 0f, (p - 0.4f) / 0.6f);
                t.localScale = full * size;
                ball.SetAlpha(1f - p);
            }, Ease.InQuad);
        }

        void Burst(Cell cell, Color color)
        {
            var centre = BoardView.CellToLocal(cell);
            for (int k = 0; k < ParticlesPerPiece; k++)
            {
                var sr = new GameObject("Particle").AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Disc();
                sr.color = color;
                sr.sortingOrder = BoardView.BallOrder + 1;
                var t = sr.transform;
                t.SetParent(board.transform, false);
                t.localPosition = centre;
                const float size = 0.22f; // prototype: 2.2 % of the width ≈ a fifth of a cell
                t.localScale = Vector3.one * size;

                // Evenly around the circle, a little jitter; 0.5–1 cell away (prototype: 5–10 % of the width).
                float angle = k / (float)ParticlesPerPiece * Mathf.PI * 2f + UnityEngine.Random.value * 0.6f;
                float distance = 0.5f + UnityEngine.Random.value * 0.5f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Tween.To(t, ParticleSeconds, p =>
                {
                    t.localPosition = centre + direction * (distance * p);
                    t.localScale = Vector3.one * (size * (1f - 0.8f * p));
                    sr.color = new Color(color.r, color.g, color.b, 1f - p);
                }, Ease.OutQuad).OnComplete(() => DestroyObject(sr.gameObject));
            }
        }

        static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        public void ShowGameOver(int score) { /* task 24: game-over pop-up */ }
    }
}
