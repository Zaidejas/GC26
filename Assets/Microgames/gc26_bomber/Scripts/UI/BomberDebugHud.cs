using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Bomber
{
    /// <summary>
    /// Temporary IMGUI HUD for the first playable step: core heat bar with the Overdrive mark,
    /// core state, cooldowns, score and a pause menu (Esc) with "To menu". Replaced by uGUI later (TDD 12).
    /// </summary>
    public sealed class BomberDebugHud : MonoBehaviour
    {
        BomberContext _ctx;
        Texture2D _white;
        GUIStyle _label, _big, _button;

        public void Init(BomberContext ctx)
        {
            _ctx = ctx;
            _white = new Texture2D(1, 1) { name = "gc26_bomber_hud_white" };
            _white.SetPixel(0, 0, Color.white); _white.Apply();
        }

        void OnDestroy() { if (_white) Destroy(_white); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) _ctx.SetPaused(!_ctx.Paused);
        }

        void OnGUI()
        {
            if (_ctx == null || _ctx.Core == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                _big = new GUIStyle(_label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            }
            float s = Screen.height / 540f;
            var m = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            var core = _ctx.Core;

            // panel
            Rect(8, 8, 300, 92, new Color(0.03f, 0.05f, 0.1f, 0.85f));
            GUI.color = new Color(0.55f, 0.6f, 0.75f); GUI.Label(new Rect(18, 14, 80, 22), "ЯДРО", _label);
            Rect(80, 18, 200, 16, new Color(0.06f, 0.08f, 0.15f));
            var barCol = core.State == CoreState.Overheated ? new Color(1f, 0.13f, 0.27f) : core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : new Color(0.31f, 0.72f, 1f);
            Rect(80, 18, 200 * core.Normalized, 16, barCol);
            Rect(80 + 200 * core.Settings.OverdriveAt / HeatModel.Max - 1, 14, 2, 24, new Color(1f, 0.89f, 0.6f));
            GUI.color = Color.white; GUI.Label(new Rect(232, 40, 70, 22), $"{Mathf.RoundToInt(core.Heat)}%", _label);
            string state = core.State == CoreState.Overheated ? $"ПЕРЕГРЕВ — оружие отключено {core.OverheatLeft:0.0} с"
                         : core.State == CoreState.Overdrive ? $"ФОРСАЖ — урон +{Mathf.RoundToInt(core.Settings.OverdriveDamageBonus * 100)}%" : "Норма";
            GUI.color = core.State == CoreState.Overheated ? new Color(1f, 0.42f, 0.5f) : core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : new Color(0.66f, 0.71f, 0.84f);
            GUI.Label(new Rect(18, 40, 220, 22), state, _label);
            GUI.color = new Color(0.66f, 0.71f, 0.84f);
            var d = _ctx.Drone;
            GUI.Label(new Rect(18, 66, 290, 22), $"R Сброс {(d.VentCd > 0 ? d.VentCd.ToString("0.0") + " с" : "готов")}   Пробел Рывок {(d.DashCd > 0 ? "…" : "готов")}", _label);

            GUI.color = Color.white;
            GUI.Label(new Rect(w - 220, 14, 200, 30), $"Счёт {_ctx.Score}", new GUIStyle(_label) { alignment = TextAnchor.UpperRight, fontSize = 20 });
            GUI.color = new Color(0.55f, 0.6f, 0.75f);
            GUI.Label(new Rect(w - 420, 40, 400, 22), $"Перегревов {core.OverheatCount} · пуль {_ctx.Projectiles.Count} · Esc — пауза", new GUIStyle(_label) { alignment = TextAnchor.UpperRight, fontSize = 13 });

            if (_ctx.Paused)
            {
                GUI.color = Color.white;
                Rect(0, 0, w, 540, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(new Rect(w / 2 - 200, 170, 400, 40), "Пауза", _big);
                if (GUI.Button(new Rect(w / 2 - 120, 240, 240, 44), "Продолжить", _button)) _ctx.SetPaused(false);
                if (GUI.Button(new Rect(w / 2 - 120, 296, 240, 44), "В меню", _button)) { _ctx.SetPaused(false); _ctx.Game.ExitToMenu(_ctx.Score); }
            }
            GUI.matrix = m; GUI.color = Color.white;
        }

        void Rect(float x, float y, float w, float h, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(x, y, w, h), _white); GUI.color = prev;
        }
    }
}
