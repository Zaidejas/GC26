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
    /// Steps without an animation yet finish at once; done: bounce (13), rolling along the path (14),
    /// score and preview (15); still to come: grow-in and pop (18), shake and "No path" alert (19).
    /// </summary>
    public sealed class BoardPresenter : IBoardPresenter
    {
        // Prototype: the selected ball bounces about a fifth of a cell, 0.36 s up and 0.36 s down.
        public const float BounceHeight = 0.17f;
        public const float BounceHalfPeriod = 0.36f;
        public const float SecondsPerCell = 0.04f;

        readonly BoardView board;
        readonly HudView hud; // optional (tests can leave it out)
        TweenHandle bounce;

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
            StopBounce();
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

        /// <summary>Adds the points with a floating "+N" (the shrink-and-burst animation comes in task 18).</summary>
        public void PlayClear(IReadOnlyList<Cell> cells, int points, Action done)
        {
            hud?.AddScore(points);
            done();
        }

        public void PlaySpawn(IReadOnlyList<Cell> cells, Action done) => done(); // task 18: grow in

        public void ShowGameOver(int score) { /* task 23: game-over pop-up */ }
    }
}
