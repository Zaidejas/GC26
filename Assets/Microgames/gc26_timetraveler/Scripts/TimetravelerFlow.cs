using System;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Switches between the game's screens inside its one scene (the rules forbid extra scenes in Build Settings):
    ///   Menu  -> World map -> Level run -> back to the map
    ///   Menu  -> Endless run -> back to the menu
    ///   Menu  -> "Exit" -> back to the GC26 menu (ExitToMenu)
    /// Each screen is a child object that is destroyed when the next one opens.
    /// Changes are applied in Update, never in the middle of another screen's OnGUI.
    /// </summary>
    public class TimetravelerFlow : MonoBehaviour
    {
        Camera _camera;
        TimetravelerSave _save;
        Action<int> _exitToShell;
        GameObject _screen;
        Action _pending;

        /// <summary>Remembers which map node the player stood on, so the map reopens there.</summary>
        Vector2? _mapPosition;

        public void Init(Camera cam, Action<int> exitToShell)
        {
            _camera = cam;
            _exitToShell = exitToShell;
            _save = new TimetravelerSave();
            ShowMenu();
        }

        void Update()
        {
            if (_pending == null) return;
            var next = _pending;
            _pending = null;
            next();
        }

        GameObject NewScreen(string name)
        {
            if (_screen != null) Destroy(_screen);
            _screen = new GameObject(name);
            _screen.transform.SetParent(transform, false);
            return _screen;
        }

        void ShowMenu()
        {
            TimetravelerLog.Info("Screen: main menu.", this);
            var menu = NewScreen("TimetravelerMenu").AddComponent<TimetravelerMenu>();
            menu.Init(_camera, _save,
                play: () => _pending = ShowMap,
                endless: () => _pending = () => StartRun(null),
                exit: () => _pending = ExitToShell);
        }

        void ShowMap()
        {
            TimetravelerLog.Info("Screen: world map.", this);
            var map = NewScreen("TimetravelerMap").AddComponent<WorldMap>();
            map.Init(_camera, _save, _mapPosition,
                playLevel: level => _pending = () => StartRun(level),
                playEndless: () => _pending = () => StartRun(null),
                back: () => _pending = ShowMenu);
            map.PositionChanged += p => _mapPosition = p;
        }

        void StartRun(TimetravelerLevel level)
        {
            TimetravelerLog.Info(level == null ? "Screen: endless run." : $"Screen: level {level.Title}.", this);
            var session = NewScreen("TimetravelerRun").AddComponent<RunnerSession>();
            session.LeaveRequested = () => _pending = level == null ? (Action)ShowMenu : ShowMap;
            session.Init(_camera, _save, level);
        }

        void ExitToShell()
        {
            if (_screen != null) Destroy(_screen);
            _screen = null;
            _exitToShell?.Invoke(_save.BestMeters);
            // Scene opened on its own (no GC26 menu to return to): show our menu again.
            if (!GC26.Core.MicrogameRunner.IsRunning) _pending = ShowMenu;
        }
    }
}
