using System;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The game's own title screen (inside the GC26 shell): Continue (saved game) or New game, Endless run,
    /// Settings. A new game over existing progress asks first (it cannot be undone).
    /// The way back to the GC26 menu (required by the shell) is in Settings, and Esc here.
    /// Art slots: menu_background (270x480, fills the screen), menu_logo (any size, kept in proportion).
    /// Greybox UI with IMGUI; real UI comes later.
    /// </summary>
    public class TimetravelerMenu : MonoBehaviour
    {
        static readonly Color Background = new Color(0.12f, 0.1f, 0.16f);

        TimetravelerSave _save;
        Action _play, _newGame, _endless, _settings, _exit;
        bool _confirmNewGame;
        Texture2D _backgroundArt, _logoArt;
        GUIStyle _title, _text, _button;
        readonly TimetravelerUi _ui = new TimetravelerUi();

        /// <param name="play">Continue the saved game (world map).</param>
        /// <param name="newGame">Wipe the progress and start from level 1-1.</param>
        public void Init(Camera cam, TimetravelerSave save, Action play, Action newGame, Action endless, Action settings,
            Action exit)
        {
            _settings = settings;
            _save = save;
            _play = play;
            _newGame = newGame;
            _endless = endless;
            _exit = exit;
            cam.backgroundColor = Background;
            _backgroundArt = TimetravelerArt.Texture(TimetravelerArt.MenuBackground);
            _logoArt = TimetravelerArt.Texture(TimetravelerArt.MenuLogo);
        }

        void Update()
        {
            _ui.Update();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (_confirmNewGame)
            {
                if (kb.escapeKey.wasPressedThisFrame) _confirmNewGame = false;
                return;
            }
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                if (_save.HasProgress) _play?.Invoke();
                else _newGame?.Invoke();
            }
            else if (kb.escapeKey.wasPressedThisFrame) _exit?.Invoke();
        }

        void OnGUI()
        {
            var unit = Screen.height / 40f;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }
            _title.fontSize = Mathf.RoundToInt(unit * 2f);
            _text.fontSize = Mathf.RoundToInt(unit * 0.8f);
            _button.fontSize = Mathf.RoundToInt(unit * 1f);

            if (_backgroundArt != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _backgroundArt, ScaleMode.ScaleAndCrop);

            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            var logoRect = new Rect(safe.x + unit, top + Screen.height * 0.08f, safe.width - unit * 2f, Screen.height * 0.2f);
            if (_logoArt != null) GUI.DrawTexture(logoRect, _logoArt, ScaleMode.ScaleToFit);
            else GUI.Label(logoRect, "TIME TRAVELER\nRUNNER", _title);

            GUI.Label(new Rect(0, top + Screen.height * 0.3f, Screen.width, unit * 1.5f),
                $"Coins: {_save.Coins}     Best endless run: {_save.BestMeters} m", _text);
            if (_save.HasProgress)
                GUI.Label(new Rect(0, top + Screen.height * 0.3f + unit * 1.4f, Screen.width, unit * 1.5f),
                    $"Saved game: {_save.LevelsDone} / {TimetravelerLevels.All.Length} levels, {PlayTime(_save.PlaySeconds)} played" +
                    (string.IsNullOrEmpty(_save.SavedAt) ? "" : $"  ({_save.SavedAt})"), _text);

            var w = Mathf.Min(Screen.width * 0.7f, unit * 14f);
            var x = (Screen.width - w) / 2f;
            var h = unit * 2.6f;
            var y = Screen.height * 0.42f;

            if (_confirmNewGame)
            {
                DrawConfirm(unit, x, y, w, h);
                return;
            }

            if (_save.HasProgress)
            {
                if (_ui.Button(new Rect(x, y, w, h), "Continue", _button)) _play?.Invoke();
                y += h + unit;
                if (_ui.Button(new Rect(x, y, w, h), "New game", _button)) _confirmNewGame = true;
            }
            else if (_ui.Button(new Rect(x, y, w, h), "New game", _button)) _newGame?.Invoke();
            y += h + unit;

            GUI.enabled = _save.EndlessOpen;
            var endlessLabel = _save.EndlessOpen ? "Endless run" : "Endless run (finish the Age of Dinosaurs)";
            if (_ui.Button(new Rect(x, y, w, h), endlessLabel, _button)) _endless?.Invoke();
            GUI.enabled = true;
            if (Application.isEditor && _save.LevelsDone < TimetravelerLevels.Era1.Length)
                GUI.Label(new Rect(x, y + h, w, unit * 1.2f), "(open in the editor for testing)", _text);
            y += h + unit * 1.6f;

            if (_ui.Button(new Rect(x, y, w, h), "Settings", _button)) _settings?.Invoke();

            GUI.Label(new Rect(0, Screen.height - safe.y - unit * 2f, Screen.width, unit * 1.5f), "by Ilona Kostiuk", _text);
        }

        /// <summary>"Start over?" with what would be lost. No is the safe default (Esc).</summary>
        void DrawConfirm(float unit, float x, float y, float w, float h)
        {
            var box = new Rect(x - unit, y - unit, w + unit * 2f, h * 2f + unit * 6.5f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(x, y, w, unit * 4.5f),
                $"Start a new game?\nYour saved progress will be lost: {_save.LevelsDone} levels, {_save.Coins} coins, best run {_save.BestMeters} m.",
                _text);
            y += unit * 5f;
            if (_ui.Button(new Rect(x, y, w, h), "Yes, start over", _button))
            {
                _confirmNewGame = false;
                _newGame?.Invoke();
            }
            y += h + unit * 0.5f;
            if (_ui.Button(new Rect(x, y, w, h), "No, keep my progress", _button)) _confirmNewGame = false;
        }

        static string PlayTime(float seconds)
        {
            var m = Mathf.FloorToInt(seconds / 60f);
            return m >= 60 ? $"{m / 60} h {m % 60} min" : $"{m} min";
        }
    }
}
