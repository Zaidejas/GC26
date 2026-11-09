using System;

namespace GC26.Core
{
    /// <summary>
    /// Implemented by exactly one component in a game's entry scene.
    /// </summary>
    public interface IMicrogame
    {
        /// <summary>Called by the shell once the scene is loaded.</summary>
        /// <param name="exit">Call once when the game is over, with the final score (0 if the game has none).</param>
        void Begin(Action<int> exit);
    }
}
