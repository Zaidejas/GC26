using System;
using System.Collections.Generic;
using GC26.Lines.Core;

namespace GC26.Lines.Game
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

        /// <summary>
        /// New balls appear in <paramref name="cells"/>, with <paramref name="colors"/> in the same order;
        /// <paramref name="frozen"/>: which of them arrive frozen (L15).
        /// </summary>
        void PlaySpawn(IReadOnlyList<Cell> cells, IReadOnlyList<BallColor> colors, IReadOnlyList<bool> frozen, Action done);

        /// <summary>L15: the player tapped a frozen ball (shake, ice crack, "Frozen — can't move").</summary>
        void ShowFrozen(Cell cell);

        /// <summary>L16: the 3×3 square around <paramref name="centre"/> is aimed at (gold square, trembling balls), or none.</summary>
        void ShowAim(Cell? centre);

        /// <summary>L16: a puffball bursts the balls of <paramref name="burst"/> (flash, spores, ice shards).</summary>
        void PlayBurst(BurstResult burst, Action done);

        /// <summary>The board is full.</summary>
        void ShowGameOver(int score);
    }
}
