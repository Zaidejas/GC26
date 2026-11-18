using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Developer hotkeys, only in the Unity editor and in development builds (never in a release build).
    /// F1 help, F2 god mode, F3 to the checkpoint, F4 to the boss arena (1-5), F5 finish the level,
    /// F6 game speed x1 / x2 / x0.5, F7 show hitboxes, F8 restart the run, F9 +100 coins,
    /// F10 open every level, F11 show the tutorials again, F12 win the boss fight.
    /// The game speed is put back to normal when the tools go away (rule 6).
    /// </summary>
    public class TimetravelerDevTools : MonoBehaviour
    {
        public static bool Allowed => Application.isEditor || Debug.isDebugBuild;

        static readonly float[] Speeds = { 1f, 2f, 0.5f };

        TimetravelerFlow _flow;
        Camera _camera;
        TimetravelerSave _save;
        bool _help;
        bool _hitboxes;
        int _speed;
        string _toast = "";
        float _toastTime;
        GUIStyle _style;

        public void Init(TimetravelerFlow flow, Camera cam, TimetravelerSave save)
        {
            _flow = flow;
            _camera = cam;
            _save = save;
            TimetravelerLog.Info("Developer hotkeys on (F1 for the list).", this);
        }

        RunnerSession Session => _flow != null && _flow.CurrentScreen != null ? _flow.CurrentScreen.GetComponent<RunnerSession>() : null;

        void Update()
        {
            _toastTime -= Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            if (kb == null || _flow == null) return;
            var run = Session;

            if (kb.f1Key.wasPressedThisFrame) _help = !_help;
            if (kb.f2Key.wasPressedThisFrame && run != null)
            {
                run.DevGodMode = !run.DevGodMode;
                Toast(run.DevGodMode ? "God mode ON" : "God mode OFF");
            }
            if (kb.f3Key.wasPressedThisFrame && run != null && !run.IsEndless)
            {
                run.DevSkipToCheckpoint();
                Toast("Skipped to the checkpoint");
            }
            if (kb.f4Key.wasPressedThisFrame && run != null)
            {
                if (run.Boss == null) Toast("No boss in this level (1-5 has one)");
                else
                {
                    run.DevSkipToArena();
                    Toast("Skipped to the end of the chase");
                }
            }
            if (kb.f5Key.wasPressedThisFrame && run != null && !run.IsEndless)
            {
                run.DevFinish();
                Toast("Level finished");
            }
            if (kb.f6Key.wasPressedThisFrame)
            {
                _speed = (_speed + 1) % Speeds.Length;
                Time.timeScale = Speeds[_speed];
                Toast($"Game speed x{Speeds[_speed]}");
            }
            if (kb.f7Key.wasPressedThisFrame)
            {
                _hitboxes = !_hitboxes;
                Toast(_hitboxes ? "Hitboxes ON" : "Hitboxes OFF");
            }
            if (kb.f8Key.wasPressedThisFrame && run != null)
            {
                run.DevRestart();
                Toast("Run restarted");
            }
            if (kb.f9Key.wasPressedThisFrame)
            {
                _save.AddCoins(100);
                Toast($"+100 coins ({_save.Coins})");
            }
            if (kb.f10Key.wasPressedThisFrame)
            {
                _save.DevUnlockAll();
                Toast("Every level open");
                if (_flow.CurrentScreen != null && _flow.CurrentScreen.GetComponent<WorldMap>() != null) _flow.ShowMap();
            }
            if (kb.f11Key.wasPressedThisFrame)
            {
                _save.DevResetTutorials();
                Toast("Tutorials will show again");
            }
            if (kb.f12Key.wasPressedThisFrame && run != null)
            {
                run.DevBossWin();
                Toast(run.Boss != null && run.Boss.InControl ? "T-Rex defeated" : "Not in a boss fight");
            }
        }

        void Toast(string text)
        {
            _toast = text;
            _toastTime = 2f;
            TimetravelerLog.Info("[dev] " + text, this);
        }

        void OnGUI()
        {
            var unit = Screen.height / 40f;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            _style.fontSize = Mathf.RoundToInt(unit * 0.7f);
            GUI.depth = -100;

            if (_hitboxes) DrawHitboxes();

            var run = Session;
            var status = "DEV  F1 help";
            if (_speed != 0) status += $"   speed x{Speeds[_speed]}";
            if (run != null && run.DevGodMode) status += "   GOD";
            Shadow(new Rect(unit * 0.5f, Screen.height - unit * 1.6f, Screen.width, unit * 1.4f), status, new Color(0.6f, 1f, 0.6f));

            if (_toastTime > 0f)
                Shadow(new Rect(unit * 0.5f, Screen.height - unit * 3f, Screen.width, unit * 1.4f), _toast, new Color(1f, 0.9f, 0.4f));

            if (!_help) return;
            var box = new Rect(unit, Screen.height * 0.25f, Screen.width - unit * 2f, unit * 15f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = old;
            Shadow(new Rect(box.x + unit * 0.6f, box.y + unit * 0.4f, box.width - unit * 1.2f, box.height),
                "DEVELOPER HOTKEYS (editor / dev builds)\n" +
                "F1  this list\n" +
                "F2  god mode (hazards do not kill)\n" +
                "F3  jump to the checkpoint\n" +
                "F4  boss level: to the end of the chase\n" +
                "F5  finish the level now\n" +
                "F6  game speed x1 / x2 / x0.5\n" +
                "F7  show hitboxes\n" +
                "F8  restart the run\n" +
                "F9  +100 coins\n" +
                "F10 open every level\n" +
                "F11 show the tutorials again\n" +
                "F12 win the boss fight", Color.white);
        }

        /// <summary>Outlines of the traveler's real hitbox (green) and of every live track piece around her.</summary>
        void DrawHitboxes()
        {
            var run = Session;
            if (run == null || run.Traveler == null || _camera == null) return;
            Outline(run.Traveler.BodyBounds, Color.green);
            foreach (var piece in run.Track.Pieces)
            {
                if (piece == null) continue;
                var col = piece.GetComponent<Collider2D>();
                if (col == null || !col.enabled) continue;
                var color = piece.GetComponent<RunnerHazard>() != null ? Color.red
                    : piece.GetComponent<RunnerCoin>() != null ? Color.yellow
                    : col.isTrigger ? Color.cyan : new Color(1f, 1f, 1f, 0.35f);
                Outline(col.bounds, color);
            }
        }

        void Outline(Bounds b, Color color)
        {
            var min = _camera.WorldToScreenPoint(b.min);
            var max = _camera.WorldToScreenPoint(b.max);
            if (max.x < 0 || min.x > Screen.width) return;
            var r = Rect.MinMaxRect(min.x, Screen.height - max.y, max.x, Screen.height - min.y);
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Shadow(Rect r, string text, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, _style);
            GUI.color = color;
            GUI.Label(r, text, _style);
            GUI.color = old;
        }

        // Speed changes are ours to undo (rule 6: no Time.timeScale left changed).
        void OnDisable()
        {
            if (_speed != 0) Time.timeScale = 1f;
            _speed = 0;
        }
    }
}
