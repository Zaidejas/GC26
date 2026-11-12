using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Entry point of Last Bastion: the only IMicrogame in gc26_towerdefence_Main.
    /// The shell calls Begin(exit); opened on its own, the scene starts itself for testing.
    /// Other systems (LevelFlow, WaveSpawner, HUD) subscribe to Started instead of using Start().
    /// </summary>
    public class TowerdefenceGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_towerdefence";

        /// <summary>Raised once, when the game begins (from the shell or the standalone test start).</summary>
        public event Action Started;

        public bool IsStarted => _started;

        Action<int> _exit;
        bool _started;

        public void Begin(Action<int> exit)
        {
            if (_started)
            {
                Debug.LogWarning($"[{Id}] Begin called twice, ignored");
                return;
            }

            _started = true;
            _exit = exit;
            Started?.Invoke();
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }

        /// <summary>Call from the pause menu ("Main menu") or the win/lose screen.</summary>
        public void ExitToMenu(int score)
        {
            // TODO: save progress (MicrogameSave.Folder(Id)) before leaving
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }

        void OnDisable()
        {
            // Rule 6: pause and the 1x/2x toggle change Time.timeScale; give the shell a clean value back.
            Time.timeScale = 1f;
        }
    }
}
