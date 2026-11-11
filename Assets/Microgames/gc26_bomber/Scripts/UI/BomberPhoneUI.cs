using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TouchPhaseIS = UnityEngine.InputSystem.TouchPhase;

namespace GC26.Bomber
{
    /// <summary>
    /// Phone (portrait) screen: HUD on top, the whole room in a neon frame in the middle (never cut by the screen
    /// edges or covered by controls), and a control panel at the bottom. Layout is in reference units 544 wide,
    /// fitted to Screen.safeArea; taller phones get a bigger room view. Movement joystick on the RIGHT (right thumb),
    /// ability buttons on the LEFT. Shooting is automatic. In the Editor the mouse acts as a finger and the
    /// keyboard also works (WASD, Space, Shift, R, G, V, Esc).
    /// </summary>
    public sealed class BomberPhoneUI : MonoBehaviour
    {
        public const float RefW = 544f, Top = 126f, PanelH = 220f, RefH = 1086f; // RefH: minimum frame height

        BomberContext _ctx;
        Font _font;
        RectTransform _safe, _frame, _bottom; // _bottom: RefW x RefH box glued to the bottom of the frame (controls live here)
        float _frameH = -1f, _wx, _wy, _ws = 1f; Image _vignette, _lowHp, _panelBg, _panelLine; readonly Image[] _border = new Image[4];
        RawImage _worldImage;
        Image _fade, _flash;

        // HUD
        readonly List<Image> _hp = new List<Image>();
        Image _heatFill, _odMark, _hudLine; Text _score, _heatText, _scrap, _status, _room, _combo, _toast, _weapon; Image _toastBg;
        readonly List<Image> _route = new List<Image>(); Image _routeCur;
        readonly List<Text> _floatTexts = new List<Text>();
        readonly List<Text> _doorLabels = new List<Text>();

        // controls
        sealed class Btn { public bool Top; public string Id; public Vector2 Pos; public float R; public Color Col; public Image Bg, Cd, Rim, Glow; public Text Label, Sub; public bool Visible = true; }
        readonly List<Btn> _btns = new List<Btn>();
        Image _joyBase, _joyKnob, _joyHint; Text _joyHintText;
        int _joyId = int.MinValue; Vector2 _joyStart, _joyNow;
        readonly Dictionary<int, string> _held = new Dictionary<int, string>();

        // panels
        GameObject _panel; Text _panelTitle, _panelText; readonly List<(RectTransform rt, System.Action act, Text label)> _panelButtons = new List<(RectTransform, System.Action, Text)>();
        string _panelKind;

        // intent collected this frame
        BomberIntent _intent; float _toastT;
        readonly Queue<(string, float)> _toasts = new Queue<(string, float)>();

        public bool Paused => _panelKind == "pause";

        public void Init(BomberContext ctx, RenderTexture worldTexture)
        {
            _ctx = ctx;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = ctx.Spawn("gc26_bomber_Canvas", transform);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var bg = NewImage("Backdrop", canvasGo.transform, ctx.Art.UiWhite, new Color(0.024f, 0.035f, 0.063f)); Stretch(bg.rectTransform);
            _safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>(); _safe.SetParent(canvasGo.transform, false); Stretch(_safe);
            _frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>(); _frame.SetParent(_safe, false);
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f); _frame.pivot = new Vector2(0.5f, 0.5f); _frame.sizeDelta = new Vector2(RefW, RefH);

            _worldImage = new GameObject("World", typeof(RectTransform)).AddComponent<RawImage>(); _worldImage.rectTransform.SetParent(_frame, false);
            _worldImage.texture = worldTexture; Place(_worldImage.rectTransform, 0, Top, RefW, 960f);
            _vignette = NewImage("Vignette", _frame, ctx.Art.UiVignette, new Color(0f, 0f, 0f, 0.55f)); Place(_vignette.rectTransform, 0, Top, RefW, 960f);
            _lowHp = NewImage("LowHp", _frame, ctx.Art.UiVignette, new Color(1f, 0.12f, 0.24f, 0f)); Place(_lowHp.rectTransform, 0, Top, RefW, 960f);
            _bottom = new GameObject("Bottom", typeof(RectTransform)).GetComponent<RectTransform>(); _bottom.SetParent(_frame, false);
            _bottom.anchorMin = _bottom.anchorMax = new Vector2(0f, 0f); _bottom.pivot = new Vector2(0f, 0f); _bottom.anchoredPosition = Vector2.zero; _bottom.sizeDelta = new Vector2(RefW, RefH);
            for (int i = 0; i < 4; i++) _border[i] = NewImage("Border", _frame, ctx.Art.UiWhite, new Color(0.29f, 0.44f, 0.65f));
            _panelBg = NewImage("ControlPanel", _bottom, ctx.Art.UiWhite, new Color(0.027f, 0.043f, 0.086f, 0.96f)); Place(_panelBg.rectTransform, 0, RefH - PanelH, RefW, PanelH);
            _panelLine = NewImage("ControlPanelLine", _bottom, ctx.Art.UiWhite, new Color(0.5f, 0.89f, 1f, 0.6f)); Place(_panelLine.rectTransform, 0, RefH - PanelH, RefW, 2);
            _fade = NewImage("Fade", _frame, ctx.Art.UiWhite, Color.black); Place(_fade.rectTransform, 0, Top, RefW, 960f);

            BuildHud();
            BuildControls();
            BuildPanel();
            _flash = NewImage("Flash", _frame, ctx.Art.UiWhite, new Color(1f, 0.24f, 0.31f, 0f)); Place(_flash.rectTransform, 0, 0, RefW, RefH); _flash.raycastTarget = false;
        }

        // ================= building =================
        void BuildHud()
        {
            var panel = NewImage("Hud", _frame, _ctx.Art.UiWhite, new Color(0.027f, 0.043f, 0.086f, 0.92f)); Place(panel.rectTransform, 0, 0, RefW, Top);
            _hudLine = NewImage("HudLine", _frame, _ctx.Art.UiWhite, new Color(1f, 0.35f, 0.43f)); Place(_hudLine.rectTransform, 0, 0, RefW, 3);
            for (int i = 0; i < 12; i++) { var c = NewImage("Hp", _frame, _ctx.Art.UiWhite, Color.red); Place(c.rectTransform, 12 + i * 24, 12, 20, 18); _hp.Add(c); }
            _score = NewText("Score", _frame, 24, TextAnchor.MiddleRight, Color.white, FontStyle.Bold); Place(_score.rectTransform, RefW - 260, 8, 190, 30);
            var heatBg = NewImage("HeatBg", _frame, _ctx.Art.UiWhite, new Color(0.055f, 0.08f, 0.15f)); Place(heatBg.rectTransform, 12, 44, RefW - 120, 20);
            _heatFill = NewImage("HeatFill", _frame, _ctx.Art.UiWhite, new Color(0.31f, 0.72f, 1f)); Place(_heatFill.rectTransform, 12, 44, 0, 20);
            _odMark = NewImage("OdMark", _frame, _ctx.Art.UiWhite, new Color(1f, 0.89f, 0.6f)); Place(_odMark.rectTransform, 12 + (RefW - 120) * 0.8f - 1, 40, 3, 28);
            _heatText = NewText("HeatText", _frame, 15, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold); Place(_heatText.rectTransform, 20, 44, 200, 20);
            var scrapIcon = NewImage("ScrapIcon", _frame, _ctx.Art.UiWhite, new Color(1f, 0.81f, 0.36f)); Place(scrapIcon.rectTransform, RefW - 96, 48, 12, 12);
            _scrap = NewText("Scrap", _frame, 18, TextAnchor.MiddleLeft, new Color(1f, 0.81f, 0.36f), FontStyle.Bold); Place(_scrap.rectTransform, RefW - 78, 42, 70, 24);
            _status = NewText("Status", _frame, 16, TextAnchor.MiddleLeft, new Color(0.66f, 0.71f, 0.84f), FontStyle.Bold); Place(_status.rectTransform, 12, 70, RefW - 24, 24);
            for (int i = 0; i < 7; i++) { var n = NewImage("Route", _frame, _ctx.Art.UiWhite, Color.gray); Place(n.rectTransform, 14 + i * 22, 101, 15, 15); _route.Add(n); }
            _routeCur = NewImage("RouteCur", _frame, _ctx.Art.UiRing, Color.white);
            _room = NewText("Room", _frame, 15, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold); Place(_room.rectTransform, 14 + 7 * 22 + 6, 98, 170, 22);
            _weapon = NewText("Weapon", _frame, 14, TextAnchor.MiddleRight, new Color(0.5f, 0.89f, 1f), FontStyle.Bold); Place(_weapon.rectTransform, RefW - 220, 98, 208, 22);
            _combo = NewText("Combo", _frame, 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); Place(_combo.rectTransform, 0, Top + 12, RefW, 30);
            _toastBg = NewImage("ToastBg", _bottom, _ctx.Art.UiWhite, new Color(0.04f, 0.06f, 0.12f, 0.88f));
            _toast = NewText("Toast", _bottom, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal); _toast.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_toastBg.rectTransform, 40, RefH - PanelH - 76, RefW - 80, 60); Place(_toast.rectTransform, 50, RefH - PanelH - 76, RefW - 100, 60);
        }

        void BuildControls()
        {
            // left-hand ability buttons (mirrored prototype layout), right thumb drives the joystick
            float py = RefH - PanelH; // top of the control panel
            AddBtn("tac", 62, py + 58, 36, "СБРОС", new Color(1f, 0.6f, 0.87f));
            AddBtn("emp", 150, py + 52, 30, "ЭМИ", new Color(0.62f, 0.82f, 1f));
            AddBtn("od", 240, py + 58, 36, "ФОРСАЖ", new Color(1f, 0.7f, 0.28f));
            AddBtn("dash", 66, py + 154, 48, "РЫВОК", new Color(0.5f, 0.89f, 1f));
            AddBtn("ab", 172, py + 166, 36, "РАЗГОН", new Color(0.5f, 0.89f, 1f));
            AddBtn("alt", 266, py + 166, 32, "ЗАРЯД", new Color(1f, 0.82f, 0.48f));
            AddBtn("pause", RefW - 34, 30, 24, "II", new Color(0.51f, 0.58f, 0.74f), _frame);
            _joyHint = NewImage("JoyHint", _bottom, _ctx.Art.UiRing, new Color(0.5f, 0.89f, 1f, 0.5f)); PlaceCircle(_joyHint.rectTransform, RefW - 112, RefH - PanelH / 2f, 62);
            _joyHintText = NewText("JoyHintText", _bottom, 14, TextAnchor.MiddleCenter, new Color(0.81f, 0.88f, 1f), FontStyle.Bold); _joyHintText.text = "Веди пальцем\nздесь";
            Place(_joyHintText.rectTransform, RefW - 192, RefH - PanelH / 2f - 30, 160, 60);
            _joyBase = NewImage("JoyBase", _frame, _ctx.Art.UiRing, new Color(0.5f, 0.89f, 1f, 0.55f));
            _joyKnob = NewImage("JoyKnob", _frame, _ctx.Art.UiCircle, new Color(0.5f, 0.89f, 1f, 0.55f));
        }

        void AddBtn(string id, float x, float y, float r, string label, Color col, RectTransform parent = null)
        {
            var _bottom = parent ? parent : this._bottom; // buttons are laid out in the bottom box unless told otherwise
            var b = new Btn { Id = id, Pos = new Vector2(x, y), R = r, Col = col, Top = parent != null };
            b.Glow = NewImage(id + "Glow", _bottom, _ctx.Art.UiCircle, new Color(col.r, col.g, col.b, 0f)); PlaceCircle(b.Glow.rectTransform, x, y, r + 8);
            b.Bg = NewImage(id, _bottom, _ctx.Art.UiCircle, new Color(0.027f, 0.043f, 0.086f, 0.72f)); PlaceCircle(b.Bg.rectTransform, x, y, r);
            b.Cd = NewImage(id + "Cd", _bottom, _ctx.Art.UiCircle, new Color(0, 0, 0, 0.6f)); PlaceCircle(b.Cd.rectTransform, x, y, r);
            b.Cd.type = Image.Type.Filled; b.Cd.fillMethod = Image.FillMethod.Radial360; b.Cd.fillOrigin = (int)Image.Origin360.Top; b.Cd.fillClockwise = false;
            b.Rim = NewImage(id + "Rim", _bottom, _ctx.Art.UiRing, col); PlaceCircle(b.Rim.rectTransform, x, y, r);
            b.Label = NewText(id + "Label", _bottom, r >= 46 ? 15 : label.Length > 5 ? 12 : 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); b.Label.text = label;
            Place(b.Label.rectTransform, x - r, y - r, r * 2, r * 2 - 10);
            b.Sub = NewText(id + "Sub", _bottom, 15, TextAnchor.MiddleCenter, col, FontStyle.Bold); Place(b.Sub.rectTransform, x - r, y, r * 2, r);
            _btns.Add(b);
        }

        void BuildPanel()
        {
            _panel = new GameObject("Panel", typeof(RectTransform)); _panel.transform.SetParent(_frame, false); Place((RectTransform)_panel.transform, 0, 0, RefW, RefH);
            var dim = NewImage("Dim", _panel.transform, _ctx.Art.UiWhite, new Color(0.016f, 0.027f, 0.055f, 0.86f)); Place(dim.rectTransform, 0, 0, RefW, RefH);
            var card = NewImage("Card", _panel.transform, _ctx.Art.UiWhite, new Color(0.05f, 0.08f, 0.14f, 1f)); Place(card.rectTransform, 30, 320, RefW - 60, 460);
            var line = NewImage("CardLine", _panel.transform, _ctx.Art.UiWhite, new Color(0.5f, 0.89f, 1f)); Place(line.rectTransform, 30, 320, RefW - 60, 3);
            _panelTitle = NewText("Title", _panel.transform, 32, TextAnchor.MiddleCenter, new Color(0.5f, 0.89f, 1f), FontStyle.Bold); Place(_panelTitle.rectTransform, 40, 340, RefW - 80, 50);
            _panelText = NewText("Body", _panel.transform, 17, TextAnchor.UpperCenter, new Color(0.75f, 0.8f, 0.9f), FontStyle.Normal); _panelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_panelText.rectTransform, 50, 400, RefW - 100, 170);
            _panel.SetActive(false);
        }

        void ShowPanel(string kind, string title, string body, params (string label, System.Action act)[] buttons)
        {
            _panelKind = kind; _panel.SetActive(true); _panelTitle.text = title; _panelText.text = body;
            foreach (var b in _panelButtons) Destroy(b.rt.gameObject);
            _panelButtons.Clear();
            for (int i = 0; i < buttons.Length; i++)
            {
                var img = NewImage("Btn", _panel.transform, _ctx.Art.UiWhite, i == 0 ? new Color(0.5f, 0.89f, 1f) : new Color(0.15f, 0.2f, 0.32f));
                Place(img.rectTransform, 70, 590 + i * 76, RefW - 140, 60);
                var t = NewText("Label", img.transform, 20, TextAnchor.MiddleCenter, i == 0 ? new Color(0.02f, 0.07f, 0.1f) : Color.white, FontStyle.Bold); t.text = buttons[i].label;
                Stretch(t.rectTransform);
                _panelButtons.Add((img.rectTransform, buttons[i].act, t));
            }
        }

        void HidePanel() { _panelKind = null; _panel.SetActive(false); }

        // ================= per-frame =================
        public BomberIntent ReadIntent()
        {
            _intent = new BomberIntent();
            var w = _ctx.World;

            // keyboard (Editor / PC testing)
            var kb = Keyboard.current; var kbMove = Vector2.zero;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1; if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y -= 1; if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y += 1;
                if (_panelKind == null)
                {
                    _intent.Dash |= kb.spaceKey.wasPressedThisFrame; _intent.Vent |= kb.rKey.wasPressedThisFrame; _intent.Emp |= kb.gKey.wasPressedThisFrame;
                    _intent.Ability |= kb.leftShiftKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame; _intent.ForsageToggle |= kb.vKey.wasPressedThisFrame;
                }
                if (kb.escapeKey.wasPressedThisFrame) { if (_panelKind == null) Pause(); else if (_panelKind == "pause") HidePanel(); }
            }

            // touches (mouse acts as finger id -1)
            if (Touchscreen.current != null)
                foreach (var t in Touchscreen.current.touches)
                {
                    var ph = t.phase.ReadValue(); if (ph == TouchPhaseIS.None) continue;
                    Pointer(t.touchId.ReadValue(), t.position.ReadValue(), ph == TouchPhaseIS.Began, ph == TouchPhaseIS.Ended || ph == TouchPhaseIS.Canceled);
                }
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
                Pointer(-1, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);

            var move = kbMove;
            if (_joyId != int.MinValue) { var v = (_joyNow - _joyStart) / 56f; if (v.magnitude > 0.18f) move = Vector2.ClampMagnitude(v, 1f); }
            _intent.Move = Vector2.ClampMagnitude(move, 1f);
            _intent.AltHeld = _held.ContainsValue("alt");
            if (w.Phase != BomberPhase.Play && _panelKind == null) ShowEnd(w);
            return _intent;
        }

        void Pointer(int id, Vector2 screen, bool began, bool ended)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame, screen, null, out var local)) return;
            var p = new Vector2(local.x + RefW / 2f, RefH / 2f - local.y); // top-left origin, y down
            if (began)
            {
                if (_panelKind != null)
                {
                    foreach (var (rt, act, _) in _panelButtons)
                    {
                        var pos = rt.anchoredPosition; var size = rt.sizeDelta; float x0 = pos.x, y0 = -pos.y;
                        if (p.x > x0 && p.x < x0 + size.x && p.y > y0 && p.y < y0 + size.y) { act(); return; }
                    }
                    return;
                }
                foreach (var b in _btns)
                {
                    var bp = b.Top ? p : new Vector2(p.x, p.y - Mathf.Max(0f, _frameH - RefH));
                    if (!b.Visible || Vector2.Distance(b.Pos, bp) > b.R + 10f) continue;
                    _held[id] = b.Id;
                    switch (b.Id)
                    {
                        case "pause": Pause(); break;
                        case "dash": _intent.Dash = true; break;
                        case "ab": _intent.Ability = true; break;
                        case "tac": _intent.Vent = true; break;
                        case "emp": _intent.Emp = true; break;
                        case "od": _intent.ForsageToggle = true; break;
                        case "alt": _intent.AltPressed = true; break;
                    }
                    return;
                }
                if (_joyId == int.MinValue && p.y > Top) { _joyId = id; _joyStart = _joyNow = p; }
                return;
            }
            if (id == _joyId) _joyNow = p;
            if (ended) { if (id == _joyId) _joyId = int.MinValue; _held.Remove(id); }
        }

        void Pause()
        {
            ShowPanel("pause", "Пауза", "Стрельба автоматическая. Джойстик — справа, способности — слева.\nФорсаж разгоняет ядро: больше урона, но можно перегреться.",
                ("Продолжить", HidePanel), ("В меню", () => _ctx.ExitToMenu()));
        }

        void ShowEnd(BomberWorld w)
        {
            bool win = w.Phase == BomberPhase.SectorClear;
            ShowPanel("end", win ? "Сектор пройден!" : "Дрон уничтожен",
                $"Счёт: {w.Score}\nКомнат: {w.Rooms} · Убийств: {w.Kills} · Макс. комбо: {w.MaxCombo}\nСкрап: {w.P.Scrap}" + (win ? "\n\nБоссы, награды и следующие секторы — следующие этапы переноса." : ""),
                ("Заново", () => { HidePanel(); _ctx.Restart(); }), ("В меню", () => _ctx.ExitToMenu()));
        }

        public void Toast(string msg, float t) { _toasts.Enqueue((msg, t)); if (_toastT <= 0f) NextToast(); }
        void NextToast()
        {
            if (_toasts.Count == 0) { _toastT = 0f; _toast.text = ""; _toastBg.enabled = false; return; }
            var (m, t) = _toasts.Dequeue(); _toast.text = m; _toastT = t; _toastBg.enabled = true;
        }

        public void Sync(float dt)
        {
            var sa = Screen.safeArea; var full = new Vector2(Screen.width, Screen.height);
            if (full.x > 0 && full.y > 0) { _safe.anchorMin = new Vector2(sa.xMin / full.x, sa.yMin / full.y); _safe.anchorMax = new Vector2(sa.xMax / full.x, sa.yMax / full.y); _safe.offsetMin = _safe.offsetMax = Vector2.zero; }
            var room = _safe.rect.size;
            if (room.x > 1f && room.y > 1f)
            {
                // fit the width; a taller phone gets a taller frame, so the room can be drawn bigger
                float scale = room.x / RefW, h = room.y / scale;
                if (h < RefH) { scale = room.y / RefH; h = RefH; }
                _frame.localScale = Vector3.one * scale;
                if (_frameH < 0f || Mathf.Abs(h - _frameH) > 0.5f) Resize(h);
            }
            if (_toastT > 0f) { _toastT -= dt; if (_toastT <= 0f) NextToast(); } else if (_toastBg.enabled && _toasts.Count == 0) NextToast();

            var w = _ctx.World; var p = w.P; var core = w.Core;
            _fade.color = new Color(0, 0, 0, Mathf.Clamp01(w.Room.Fade / 0.35f));
            var acc = (Color)BomberData.Sectors[w.Sector].Accent; acc = Color.Lerp(acc, Color.white, 0.25f); acc.a = 0.85f;
            foreach (var bi in _border) bi.color = p.Hp <= 2 ? new Color(1f, 0.25f, 0.35f, 0.6f + 0.3f * Mathf.Sin(w.Time * 6f)) : acc;
            _lowHp.color = new Color(1f, 0.12f, 0.24f, p.Hp <= 2 && w.Phase == BomberPhase.Play ? 0.35f + 0.2f * Mathf.Sin(w.Time * 6f) : 0f);
            _flash.color = new Color(1f, 0.24f, 0.31f, Mathf.Max(0f, w.Flash));
            _hudLine.color = p.Hp <= 2 ? new Color(1f, 0.13f, 0.27f) : new Color(1f, 0.35f, 0.43f);
            for (int i = 0; i < _hp.Count; i++)
            {
                bool show = i < p.MaxHp, on = i < p.Hp, low = p.Hp <= 2 && on && Mathf.Sin(w.Time * 8f) > 0f;
                _hp[i].enabled = show; _hp[i].color = on ? (low ? new Color(1f, 0.6f, 0.66f) : new Color(1f, 0.35f, 0.43f)) : new Color(0.16f, 0.11f, 0.15f);
            }
            _score.text = w.Score.ToString("N0").Replace(',', ' ');
            float bw = RefW - 120;
            _heatFill.rectTransform.sizeDelta = new Vector2(bw * core.Normalized, 20);
            _heatFill.color = core.State == CoreState.Overheated ? (Mathf.Sin(w.Time * 20f) > 0 ? new Color(1f, 0.13f, 0.27f) : new Color(0.4f, 0.06f, 0.13f)) : core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : new Color(0.31f, 0.72f, 1f);
            _heatText.text = $"ЯДРО {Mathf.RoundToInt(core.Heat)}%";
            _scrap.text = p.Scrap.ToString();
            _status.text = w.StatusLine;
            _status.color = core.State == CoreState.Overheated ? new Color(1f, 0.42f, 0.5f) : w.Governor.Forsage || core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : p.AbT > 0f ? new Color(0.5f, 0.89f, 1f) : new Color(0.66f, 0.71f, 0.84f);
            var plan = w.Plan[w.Sector];
            for (int i = 0; i < _route.Count; i++)
            {
                bool show = i < plan.Count; _route[i].enabled = show; if (!show) continue;
                var t = plan[i][0]; var col = (Color)BomberData.RoomColor[t];
                _route[i].color = i == w.Step ? col : i < w.Step ? new Color(col.r, col.g, col.b, 0.35f) : new Color(col.r * 0.45f, col.g * 0.45f, col.b * 0.5f);
            }
            PlaceCircle(_routeCur.rectTransform, 14 + w.Step * 22 + 7.5f, 108.5f, 12);
            var curT = w.Room.Type == "start" ? "combat" : w.Room.Type;
            _room.text = $"С{w.Sector + 1} · {BomberData.RoomLabel[curT]}"; _room.color = (Color)BomberData.RoomColor[curT];
            _weapon.text = "Бластер";
            _combo.rectTransform.anchoredPosition = new Vector2(0f, -(_wy + 6f));
            _combo.text = w.Combo >= 2 ? $"КОМБО ×{w.Combo}{(w.Combo >= 10 ? " · В УДАРЕ" : "")}" : "";
            _combo.color = w.Combo >= 10 ? new Color(1f, 0.84f, 0.36f) : Color.white;

            // buttons
            foreach (var b in _btns)
            {
                float ready = 1f; string sub = "";
                switch (b.Id)
                {
                    case "dash": ready = Mathf.Clamp01(1f - p.DashCd / 0.8f); break;
                    case "ab": if (p.AbT > 0f) sub = p.AbT.ToString("0.0"); else { ready = Mathf.Clamp01(1f - p.AbCd / 16f); if (ready < 1f) sub = Mathf.CeilToInt(p.AbCd).ToString(); } break;
                    case "tac": ready = Mathf.Clamp01(1f - p.TacCd / 8f); if (ready < 1f) sub = Mathf.CeilToInt(p.TacCd).ToString(); break;
                    case "emp": ready = p.Emp > 0 ? 1f : 0f; sub = "×" + p.Emp; break;
                    case "od": ready = core.CanFire ? 1f : 0f; b.Label.text = w.Governor.Forsage ? "ФОРСАЖ\nВКЛ" : "ФОРСАЖ"; if (!core.CanFire) sub = core.OverheatLeft.ToString("0.0"); break;
                    case "alt": ready = p.AltCd <= 0f ? 1f : 0.3f; break;
                }
                bool down = _held.ContainsValue(b.Id), isReady = ready >= 1f;
                b.Cd.fillAmount = 1f - ready;
                b.Bg.color = down ? new Color(0.16f, 0.24f, 0.4f, 0.88f) : new Color(0.027f, 0.043f, 0.086f, 0.72f);
                b.Rim.color = isReady ? b.Col : new Color(0.43f, 0.51f, 0.67f, 0.6f);
                b.Label.color = isReady ? Color.white : new Color(0.54f, 0.59f, 0.71f);
                b.Label.rectTransform.anchoredPosition = new Vector2(b.Pos.x - b.R, -(b.Pos.y - b.R + (sub.Length > 0 ? -2 : 4)));
                b.Sub.text = sub;
                bool glow = b.Id == "od" && w.Governor.Forsage;
                b.Glow.color = new Color(b.Col.r, b.Col.g, b.Col.b, glow ? 0.35f + 0.25f * Mathf.Sin(w.Time * 10f) : 0f);
            }

            // joystick
            bool joy = _joyId != int.MinValue;
            _joyBase.enabled = _joyKnob.enabled = joy; _joyHint.enabled = _joyHintText.enabled = !joy && _panelKind == null;
            if (joy)
            {
                PlaceCircle(_joyBase.rectTransform, _joyStart.x, _joyStart.y, 60);
                var d = Vector2.ClampMagnitude(_joyNow - _joyStart, 56f);
                PlaceCircle(_joyKnob.rectTransform, _joyStart.x + d.x, _joyStart.y + d.y, 26);
            }
            else _joyHint.color = new Color(0.5f, 0.89f, 1f, 0.4f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f));

            // floating texts and door labels (world px == reference units, world drawn at y = Top)
            int n = 0;
            foreach (var t in w.Texts)
            {
                if (n >= 40) break;
                var tx = GetText(_floatTexts, n++, "Float"); tx.text = t.S; tx.fontSize = Mathf.RoundToInt((t.Big ? 30 : t.S.Length <= 3 ? 16 : 14) * Mathf.Max(0.85f, _ws));
                var c = (Color)t.Color; c.a = Mathf.Min(1f, t.T * 2f); tx.color = c;
                Place(tx.rectTransform, _wx + t.X * _ws - 150, _wy + t.Y * _ws - 15, 300, 30);
            }
            for (int i = n; i < _floatTexts.Count; i++) _floatTexts[i].enabled = false;
            int m = 0;
            foreach (var d in w.Doors)
            {
                var tx = GetText(_doorLabels, m++, "Door"); tx.text = BomberData.RoomLabel[d.Type]; tx.fontSize = 15;
                tx.color = (Color)BomberData.RoomColor[d.Type]; Place(tx.rectTransform, _wx + (d.X0 + d.X1) / 2f * _ws - 80, _wy + (BomberData.Cell + 4) * _ws, 160, 22);
            }
            for (int i = m; i < _doorLabels.Count; i++) _doorLabels[i].enabled = false;
        }

        void Resize(float h)
        {
            _frameH = h; _frame.sizeDelta = new Vector2(RefW, h);
            // the whole room (544 x 960 world px) fits between the HUD and the control panel, centred, with a frame
            const float pad = 10f;
            float areaTop = Top + pad, areaH = h - PanelH - Top - pad * 2f;
            _ws = Mathf.Min((RefW - pad * 2f) / BomberData.W, areaH / BomberData.H);
            float ww = BomberData.W * _ws, wh = BomberData.H * _ws;
            _wx = (RefW - ww) / 2f; _wy = areaTop + (areaH - wh) / 2f;
            foreach (var rt in new[] { _worldImage.rectTransform, _fade.rectTransform, _vignette.rectTransform, _lowHp.rectTransform }) Place(rt, _wx, _wy, ww, wh);
            const float b = 3f;
            Place(_border[0].rectTransform, _wx - b, _wy - b, ww + b * 2f, b); Place(_border[1].rectTransform, _wx - b, _wy + wh, ww + b * 2f, b);
            Place(_border[2].rectTransform, _wx - b, _wy, b, wh); Place(_border[3].rectTransform, _wx + ww, _wy, b, wh);
            _worldImage.texture = _ctx.View.SetViewHeight(BomberData.H);
        }

        Text GetText(List<Text> list, int i, string name)
        {
            while (list.Count <= i) { var t = NewText(name, _frame, 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); t.raycastTarget = false; list.Add(t); var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); }
            var tx = list[i]; tx.enabled = true; tx.transform.SetSiblingIndex(_fade.transform.GetSiblingIndex() + 1); return tx;
        }

        // ================= helpers =================
        Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false; return img;
        }

        Text NewText(string name, Transform parent, int size, TextAnchor align, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = _font; t.fontSize = size; t.alignment = align; t.color = color; t.fontStyle = style; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }

        /// <summary>Places a rect by its top-left corner in reference units (y down).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceCircle(RectTransform rt, float cx, float cy, float r) => Place(rt, cx - r, cy - r, r * 2f, r * 2f);
        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    }
}
