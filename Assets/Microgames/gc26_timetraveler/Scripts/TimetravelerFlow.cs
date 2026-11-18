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
        TimetravelerSettings _settings;
        Action<int> _exitToShell;
        GameObject _screen;
        /// <summary>Tests: the current screen object (menu, map or run).</summary>
        internal GameObject CurrentScreen => _screen;
        Action _pending;

        /// <summary>Remembers which map node the player stood on, so the map reopens there.</summary>
        Vector2? _mapPosition;

        public void Init(Camera cam, Action<int> exitToShell)
        {
            _camera = cam;
            _exitToShell = exitToShell;
            _save = new TimetravelerSave();
            _settings = TimetravelerSettings.Load();
            _mapPosition = _save.MapPosition;   // continue where the traveler stood
            if (TimetravelerDevTools.Allowed) gameObject.AddComponent<TimetravelerDevTools>().Init(this, cam, _save);
            ShowMenu();
        }

        void Update()
        {
            _save?.AddPlayTime(Time.unscaledDeltaTime);
            if (_pending == null) return;
            var next = _pending;
            _pending = null;
            next();
        }

        GameObject NewScreen(string name)
        {
            _save.Flush();   // autosave on every screen change (map position, play time)
            if (_screen != null) Destroy(_screen);
            _screen = new GameObject(name);
            _screen.transform.SetParent(transform, false);
            return _screen;
        }

        internal void ShowMenu()
        {
            TimetravelerLog.Info("Screen: main menu.", this);
            var menu = NewScreen("TimetravelerMenu").AddComponent<TimetravelerMenu>();
            menu.Init(_camera, _save,
                play: () => _pending = ShowMap,
                newGame: () => _pending = NewGame,
                endless: () => _pending = () => StartRun(null),
                settings: () => _pending = ShowSettings,
                exit: () => _pending = ExitToShell);
        }

        internal void ShowSettings()
        {
            TimetravelerLog.Info("Screen: settings.", this);
            var screen = NewScreen("TimetravelerSettings").AddComponent<TimetravelerSettingsScreen>();
            screen.Init(_camera, _settings,
                back: () => _pending = ShowMenu,
                exitToShell: () => _pending = ExitToShell);
        }

        internal void ShowMap()
        {
            TimetravelerLog.Info("Screen: world map.", this);
            var map = NewScreen("TimetravelerMap").AddComponent<WorldMap>();
            map.Init(_camera, _save, _mapPosition,
                playLevel: level => _pending = () => StartRun(level),
                playEndless: () => _pending = () => StartRun(null),
                back: () => _pending = ShowMenu);
            map.PositionChanged += p =>
            {
                _mapPosition = p;
                _save.SetMapPosition(p);
            };
        }

        internal void StartRun(TimetravelerLevel level)
        {
            TimetravelerLog.Info(level == null ? "Screen: endless run." : $"Screen: level {level.Title}.", this);
            var session = NewScreen("TimetravelerRun").AddComponent<RunnerSession>();
            session.LeaveRequested = () => _pending = level == null ? (Action)ShowMenu : ShowMap;
            session.Init(_camera, _save, level, _settings);
        }

        /// <summary>Wipes the save and opens the map at the first level.</summary>
        internal void NewGame()
        {
            _save.NewGame();
            _mapPosition = null;
            ShowMap();
        }

        // A phone may kill the app in the background without a quit message: save when it goes there.
        void OnApplicationPause(bool paused)
        {
            if (paused) _save?.Flush();
        }

        void OnDestroy() => _save?.Flush();

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
