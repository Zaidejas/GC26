using System;
using System.Collections.Generic;
using Lines.Core;
using Lines.View;
using UnityEngine;

namespace Lines.Game
{
    /// <summary>
    /// Shows the turn on the <see cref="BoardView"/>. Steps without an animation yet finish at once;
    /// the bounce (13) and rolling along the path (14) are done; still to come:
    /// grow-in and pop (18), shake and "No path" alert (19).
    /// </summary>
    public sealed class BoardPresenter : IBoardPresenter
    {
        // Prototype: the selected ball bounces about a fifth of a cell, 0.36 s up and 0.36 s down.
        public const float BounceHeight = 0.17f;
        public const float BounceHalfPeriod = 0.36f;
        public const float SecondsPerCell = 0.04f;

        readonly BoardView board;
        TweenHandle bounce;

        public BoardPresenter(BoardView board)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
        }

        public void ShowState(GameState game)
        {
            StopBounce();
            board.ShowBoard(game.Board);
        }

        /// <summary>The selected ball bounces; the previously selected one settles back into its cell.</summary>
        public void ShowSelected(Cell? cell)
        {
            StopBounce();
            if (cell is Cell c && board.BallAt(c) is BallView ball)
                bounce = Tween.Bounce(ball.transform, BounceHeight, BounceHalfPeriod);
        }

        void StopBounce()
        {
            bounce?.Kill(); // puts the ball back where it was
            bounce = null;
        }

        public void ShowBlocked(Cell from, Cell to) { /* task 19: shake + "No path" */ }

        /// <summary>The ball rolls along the path at an even speed, one cell every 40 ms (as in the prototype).</summary>
        public void PlayMove(IReadOnlyList<Cell> path, Action done)
        {
            var from = path[0];
            var to = path[path.Count - 1];
            var ball = board.BallAt(from);
            if (ball == null || path.Count < 2) { board.MoveBall(from, to); done(); return; }

            var points = new Vector3[path.Count - 1];
            for (int i = 1; i < path.Count; i++) points[i - 1] = BoardView.CellToLocal(path[i]);
            Tween.MovePath(ball.transform, points, SecondsPerCell).OnComplete(() =>
            {
                board.MoveBall(from, to);
                done();
            });
        }

        public void PlayClear(IReadOnlyList<Cell> cells, int points, Action done) => done(); // task 18: shrink and burst

        public void PlaySpawn(IReadOnlyList<Cell> cells, Action done) => done(); // task 18: grow in

        public void ShowGameOver(int score) { /* task 23: game-over pop-up */ }
    }
}
