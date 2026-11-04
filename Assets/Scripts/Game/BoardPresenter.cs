using System;
using System.Collections.Generic;
using Lines.Core;
using Lines.View;

namespace Lines.Game
{
    /// <summary>
    /// Shows the turn on the <see cref="BoardView"/>. For now every step finishes at once;
    /// the animations arrive in later tasks: selection bounce (13), rolling along the path (14),
    /// grow-in and pop (18), shake and "No path" alert (19).
    /// </summary>
    public sealed class BoardPresenter : IBoardPresenter
    {
        // Prototype: the selected ball bounces about a fifth of a cell, 0.36 s up and 0.36 s down.
        public const float BounceHeight = 0.17f;
        public const float BounceHalfPeriod = 0.36f;

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

        public void PlayMove(IReadOnlyList<Cell> path, Action done) => done(); // task 14: roll along the path

        public void PlayClear(IReadOnlyList<Cell> cells, int points, Action done) => done(); // task 18: shrink and burst

        public void PlaySpawn(IReadOnlyList<Cell> cells, Action done) => done(); // task 18: grow in

        public void ShowGameOver(int score) { /* task 23: game-over pop-up */ }
    }
}
