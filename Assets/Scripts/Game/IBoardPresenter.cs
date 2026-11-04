using System;
using System.Collections.Generic;
using Lines.Core;

namespace Lines.Game
{
    /// <summary>
    /// What the turn controller asks the screen to show. Methods with a <c>done</c> callback play an
    /// animation and must call <c>done</c> exactly once when it has finished; until then input stays locked.
    /// </summary>
    public interface IBoardPresenter
    {
        /// <summary>Redraws everything from the game's current state (no animation).</summary>
        void ShowState(GameState game);

        /// <summary>Highlights the selected ball (it bounces), or clears the highlight when null.</summary>
        void ShowSelected(Cell? cell);

        /// <summary>The selected ball can't reach the tapped cell (shake + "No path").</summary>
        void ShowBlocked(Cell from, Cell to);

        /// <summary>The ball rolls along <paramref name="path"/> (start and target included).</summary>
        void PlayMove(IReadOnlyList<Cell> path, Action done);

        /// <summary>Balls in <paramref name="cells"/> disappear; <paramref name="points"/> were scored.</summary>
        void PlayClear(IReadOnlyList<Cell> cells, int points, Action done);

        /// <summary>New balls appear in <paramref name="cells"/>.</summary>
        void PlaySpawn(IReadOnlyList<Cell> cells, Action done);

        /// <summary>The board is full.</summary>
        void ShowGameOver(int score);
    }
}
