using System;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Settings screen (from the main menu): music and sound volume, camera shake, tutorial slow motion,
    /// on-screen controls, rhythm offset for the boss fight, reset. At the bottom, "Exit to GC26 menu"
    /// (the GC26 shell needs a way back; it lives here instead of on the main menu).
    /// Every change is saved straight away. Greybox IMGUI, big touch-friendly rows.
    /// </summary>
    public class TimetravelerSettingsScreen : MonoBehaviour
    {
        static readonly Color Background = new Color(0.12f, 0.1f, 0.16f);

        TimetravelerSettings _settings;
        Action _back, _exitToShell;
        GUIStyle _title, _label, _button;
        readonly TimetravelerUi _ui = new TimetravelerUi();
        bool _confirmExit;

        public void Init(Camera cam, TimetravelerSettings settings, Action back, Action exitToShell)
        {
            _settings = settings;
            _back = back;
            _exitToShell = exitToShell;
            cam.backgroundColor = Background;
        }

        void Update()
        {
            _ui.Update();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_confirmExit) _confirmExit = false;
                else _back?.Invoke();
            }
        }

        void OnGUI()
        {
            var unit = Screen.height / 40f;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = true };
                _button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }
            _title.fontSize = Mathf.RoundToInt(unit * 1.8f);
            _label.fontSize = Mathf.RoundToInt(unit * 0.9f);
            _button.fontSize = Mathf.RoundToInt(unit * 0.9f);

            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            var x = safe.x + unit;
            var w = safe.width - unit * 2f;
            var rowH = unit * 2.4f;
            var y = top + unit;

            if (_ui.Button(new Rect(x, y, unit * 5f, unit * 2f), "Back", _button)) _back?.Invoke();
            GUI.Label(new Rect(x, y, w, unit * 2f), "SETTINGS", _title);
            y += unit * 3.2f;

            if (_confirmExit)
            {
                GUI.Label(new Rect(x, y, w, unit * 3f), "Leave Time Traveler and go back to the GC26 menu?\nYour progress is saved.", _label);
                y += unit * 3.5f;
                if (_ui.Button(new Rect(x, y, w, rowH), "Yes, exit", _button)) _exitToShell?.Invoke();
                y += rowH + unit * 0.5f;
                if (_ui.Button(new Rect(x, y, w, rowH), "No, stay", _button)) _confirmExit = false;
                return;
            }

            Volume(ref y, x, w, rowH, unit, "Music", _settings.MusicVolume, v => _settings.MusicVolume = v);
            Volume(ref y, x, w, rowH, unit, "Sounds", _settings.SoundVolume, v => _settings.SoundVolume = v);

            var shakeNames = new[] { "OFF", "WEAK", "ON" };
            if (Row(ref y, x, w, rowH, unit, "Camera shake", shakeNames[_settings.Shake]))
                _settings.Shake = (_settings.Shake + 1) % 3;
            if (Row(ref y, x, w, rowH, unit, "Tutorial slow motion (first time only)", _settings.TutorialSlowMotion ? "ON" : "OFF"))
                _settings.TutorialSlowMotion = !_settings.TutorialSlowMotion;
            if (Row(ref y, x, w, rowH, unit, "Controls help on screen", _settings.ShowControls ? "ON" : "OFF"))
                _settings.ShowControls = !_settings.ShowControls;

            // Rhythm offset: - / value / +
            GUI.Label(new Rect(x, y, w * 0.5f, rowH), "Boss rhythm offset\n(if sound comes late: +)", _label);
            var bw = unit * 3f;
            if (_ui.Button(new Rect(x + w - bw * 3.2f, y, bw, rowH), "-", _button)) _settings.RhythmOffsetMs -= 10;
            GUI.Label(new Rect(x + w - bw * 2.15f, y, bw * 1.1f, rowH), $"{_settings.RhythmOffsetMs:+0;-0;0} ms", _label);
            if (_ui.Button(new Rect(x + w - bw, y, bw, rowH), "+", _button)) _settings.RhythmOffsetMs += 10;
            y += rowH + unit * 0.6f;

            y += unit * 0.6f;
            if (_ui.Button(new Rect(x, y, w, rowH), "Reset settings", _button)) _settings.ResetToDefaults();

            // The way back to the GC26 shell (required), at the bottom, behind a confirmation.
            var exitRect = new Rect(x, Screen.height - safe.y - unit - rowH, w, rowH);
            if (_ui.Button(exitRect, "Exit to GC26 menu", _button)) _confirmExit = true;
        }

        void Volume(ref float y, float x, float w, float rowH, float unit, string name, float value, Action<float> set)
        {
            GUI.Label(new Rect(x, y, w * 0.45f, rowH), name, _label);
            var bw = unit * 3f;
            if (_ui.Button(new Rect(x + w - bw * 3.2f, y, bw, rowH), "-", _button)) set(Mathf.Round((value - 0.1f) * 10f) / 10f);
            GUI.Label(new Rect(x + w - bw * 2.15f, y, bw * 1.1f, rowH), $"{Mathf.RoundToInt(value * 100f)}%", _label);
            if (_ui.Button(new Rect(x + w - bw, y, bw, rowH), "+", _button)) set(Mathf.Round((value + 0.1f) * 10f) / 10f);
            y += rowH + unit * 0.6f;
        }

        /// <summary>A row with a name and a button showing the current value; true when tapped.</summary>
        bool Row(ref float y, float x, float w, float rowH, float unit, string name, string value)
        {
            GUI.Label(new Rect(x, y, w * 0.6f, rowH), name, _label);
            var tapped = _ui.Button(new Rect(x + w - unit * 6f, y, unit * 6f, rowH), value, _button);
            y += rowH + unit * 0.6f;
            return tapped;
        }
    }
}
