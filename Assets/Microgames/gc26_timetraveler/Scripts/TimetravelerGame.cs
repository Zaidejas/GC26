using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Entry point of Time Traveler Runner inside the GC26 shell.
    /// The shared project rotates freely, so this game locks the screen to portrait
    /// while it runs and restores the previous orientation when it is unloaded (rule 6).
    /// </summary>
    public class TimetravelerGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_timetraveler";

        Action<int> _exit;
        bool _started;

        ScreenOrientation _previousOrientation;
        bool _orientationLocked;

        public void Begin(Action<int> exit)
        {
            _started = true;
            _exit = exit;
            LockPortrait();
            StartGreyboxRunner();
            // TODO: load the save (MicrogameSave.Folder(Id)) and show the title screen
        }

        /// <summary>Week 1-2: go straight into the greybox runner for feel testing.</summary>
        void StartGreyboxRunner()
        {
            if (GetComponent<RunnerSession>() != null) return;
            var cam = FindOwnCamera();
            if (cam == null)
            {
                Debug.LogError($"[{Id}] No camera in the game scene.");
                return;
            }
            gameObject.AddComponent<RunnerSession>().Init(cam);
        }

        /// <summary>The camera in this game's own scene (the shell may have its own camera).</summary>
        Camera FindOwnCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var cam = root.GetComponentInChildren<Camera>(true);
                if (cam != null) return cam;
            }
            return null;
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }

        void OnDisable()
        {
            RestoreOrientation();
        }

        /// <summary>Call from the pause menu ("To menu") or the game-over screen.</summary>
        public void ExitToMenu(int score)
        {
            // TODO: save progress before leaving
            RestoreOrientation();
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }

        void LockPortrait()
        {
            if (_orientationLocked) return;
            _previousOrientation = Screen.orientation;
            Screen.orientation = ScreenOrientation.Portrait;
            _orientationLocked = true;
        }

        void RestoreOrientation()
        {
            if (!_orientationLocked) return;
            Screen.orientation = _previousOrientation;
            _orientationLocked = false;
        }
    }
}
