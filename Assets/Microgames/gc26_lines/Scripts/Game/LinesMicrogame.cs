using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Lines.Game
{
    /// <summary>
    /// The link between Lines and the shared GC26 menu (RULES.md → "Контракт с оболочкой"): the menu loads
    /// gc26_lines_Main, finds this component and calls <see cref="Begin"/>. The game itself starts on its own
    /// through <see cref="GameBootstrap"/>, so a phone build with only Lines works the same with or without
    /// the menu. Not wired up yet: the "Back to games" button that calls <see cref="ExitToMenu"/> and a pause
    /// come when all games are merged into one app.
    /// </summary>
    public class LinesMicrogame : MonoBehaviour, IMicrogame
    {
        /// <summary>Our ID in the team project: the folder, scene prefix, save folder and Resources folder.</summary>
        public const string Id = "gc26_lines";

        Action<int> exit;
        bool started;

        /// <summary>True when the shared menu started the game (false in our own phone builds).</summary>
        public bool InSharedMenu { get; private set; }

        public void Begin(Action<int> exit)
        {
            started = true;
            InSharedMenu = MicrogameRunner.IsRunning;
            this.exit = exit;
        }

        void Start()
        {
            // Opened on its own (our phone builds, Play in the editor): no shared menu to go back to.
            if (!started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }

        /// <summary>Leaves to the shared menu with the score. The game is already saved after every move.</summary>
        public void ExitToMenu(int score)
        {
            var leave = exit;
            exit = null;
            leave?.Invoke(score);
        }
    }
}
