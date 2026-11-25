using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Entry point for the GC26 shell (the only IMicrogame in gc26_bomber_Main).
    /// Builds the whole session under a BomberContext child, so the scene needs just this one object.
    /// </summary>
    public class BomberGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_bomber";

        Action<int> _exit;
        bool _started;
        BomberContext _context;

        public void Begin(Action<int> exit)
        {
            _started = true;
            _exit = exit;
            // meta progress is loaded by BomberContext (BomberMeta, PlayerPrefs via MicrogameSave.Key)
            if (_context == null)
            {
                var go = new GameObject("gc26_bomber_Context");
                go.transform.SetParent(transform, false);
                _context = go.AddComponent<BomberContext>();
                _context.Init(this);
            }
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
            // progress is saved by BomberContext before it calls this
            Time.timeScale = 1f;
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }

        void OnDisable() => Time.timeScale = 1f;
    }
}
