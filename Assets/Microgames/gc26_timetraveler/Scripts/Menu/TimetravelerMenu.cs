using System;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The game's own title screen (inside the GC26 shell): Play (world map), Endless run, Exit to the GC26 menu.
    /// Art slots: menu_background (270x480, fills the screen), menu_logo (any size, kept in proportion).
    /// Greybox UI with IMGUI; real UI comes later.
    /// </summary>
    public class TimetravelerMenu : MonoBehaviour
    {
        static readonly Color Background = new Color(0.12f, 0.1f, 0.16f);

        TimetravelerSave _save;
        Action _play, _endless, _exit;
        Texture2D _backgroundArt, _logoArt;
        GUIStyle _title, _text, _button;
        readonly TimetravelerUi _ui = new TimetravelerUi();

        public void Init(Camera cam, TimetravelerSave save, Action play, Action endless, Action exit)
        {
            _save = save;
            _play = play;
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
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) _play?.Invoke();
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

            var w = Mathf.Min(Screen.width * 0.7f, unit * 14f);
            var x = (Screen.width - w) / 2f;
            var h = unit * 2.6f;
            var y = Screen.height * 0.42f;

            if (_ui.Button(new Rect(x, y, w, h), _save.LevelsDone == 0 ? "Play" : "Continue", _button)) _play?.Invoke();
            y += h + unit;

            GUI.enabled = _save.EndlessOpen;
            var endlessLabel = _save.EndlessOpen ? "Endless run" : "Endless run (finish the Age of Dinosaurs)";
            if (_ui.Button(new Rect(x, y, w, h), endlessLabel, _button)) _endless?.Invoke();
            GUI.enabled = true;
            if (Application.isEditor && _save.LevelsDone < TimetravelerLevels.Era1.Length)
                GUI.Label(new Rect(x, y + h, w, unit * 1.2f), "(open in the editor for testing)", _text);
            y += h + unit * 1.6f;

            if (_ui.Button(new Rect(x, y, w, h), "Exit to GC26 menu", _button)) _exit?.Invoke();

            GUI.Label(new Rect(0, Screen.height - safe.y - unit * 2f, Screen.width, unit * 1.5f), "by Ilona Kostiuk", _text);
        }
    }
}
