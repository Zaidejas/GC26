using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Towerdefence
{
    public class TowerdefenceGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_towerdefence";

        Action<int> _exit;
        bool _started;

        public void Begin(Action<int> exit)
        {
            _started = true;
            _exit = exit;
            // TODO: load the save (MicrogameSave.Folder(Id)) and start the game
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }

        /// <summary>Call from the pause menu ("To menu") or the game-over screen.</summary>
        public void ExitToMenu(int score)
        {
            // TODO: save progress before leaving
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }
    }
}
