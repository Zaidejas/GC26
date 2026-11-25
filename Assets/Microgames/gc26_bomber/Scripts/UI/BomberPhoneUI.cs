using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using GC26.Core;
using TouchPhaseIS = UnityEngine.InputSystem.TouchPhase;

namespace GC26.Bomber
{
    /// <summary>
    /// Phone / tablet screen, adaptive to any resolution and aspect:
    /// - UI units keep about the same PHYSICAL size on every device (from Screen.dpi), so buttons and text are
    ///   thumb-sized on a phone and on a tablet alike; all remaining space goes to the room;
    /// - HUD on top spans the full width; the room fills (almost) the full width in a neon frame down to the bottom
    ///   of the screen, and the view floats after the drone vertically; touch controls are semi-transparent and lie
    ///   over the room (abilities bottom-left, joystick bottom-right), a station shaft continues below the room;
    /// - tablets get a ~30% larger UI than phones so it stays readable at arm's length;
    /// - everything stays inside Screen.safeArea (notch, rounded corners, home bar).
    /// In the Editor the mouse acts as a finger and the keyboard works too (WASD, Space, Shift, R, G, V, Esc).
    /// </summary>
    public sealed partial class BomberPhoneUI : MonoBehaviour
    {
        const float MinW = 544f, MinH = 1086f;            // smallest layout (reference phone)
        const float Top = 126f, PanelH = 220f, Pad = 10f;  // HUD height, control panel height
        const float InchPerUnit = 0.0047f;                 // physical size of one UI unit (iPhone 11 = 1 unit per 1.52 px)

        BomberContext _ctx;
        Font _font;
        CanvasScaler _scaler;
        RectTransform _safe;
        float _btnScale = 1.3f, _bottomInset, _uiS = 1f; // button size chosen by the player (0.8 .. 1.6), saved in PlayerPrefs
        float _fw = -1f, _fh = -1f, _wx, _wy, _ws = 1f, _viewH = 960f, _viewTotal = 960f, _scroll, _scrollTarget; bool _scrollInit;

        RawImage _world; Image _fade, _flash, _vignette, _lowHp, _hudBg, _hudLine, _panelBg, _panelLine; readonly Image[] _border = new Image[4];
        readonly List<Image> _hp = new List<Image>(); Image _heatBg, _heatFill, _odMark, _scrapIcon, _routeCur; Text _score, _heatText, _scrap, _status, _room, _combo, _toast, _weapon; Image _toastBg;
        readonly List<Image> _route = new List<Image>();
        readonly List<Text> _floatTexts = new List<Text>(), _doorLabels = new List<Text>(), _propLabels = new List<Text>(), _tagLabels = new List<Text>();
        Image _bossBg, _bossBack, _bossFill, _bossLine; readonly Image[] _bossMarks = new Image[2]; Text _bossName; float _ww, _bossW;

        sealed class Btn { public string Id; public bool AtTop; public Vector2 Local; public Vector2 Pos; public float R, Rad; public int LabelFont; public bool Visible = true; public Color Col; public Image Bg, Cd, Rim, Glow; public Text Label, Sub; }
        readonly List<Btn> _btns = new List<Btn>();
        Image _joyBase, _joyKnob, _joyHint; Text _joyHintText;
        int _joyId = int.MinValue; Vector2 _joyStart, _joyNow;
        readonly Dictionary<int, string> _held = new Dictionary<int, string>();

        RectTransform _panel; Image _panelDim, _card, _cardLine; Text _panelTitle, _panelText; float _btnH = 60f, _btnTop = 270f;
        readonly List<(RectTransform rt, System.Action act)> _panelButtons = new List<(RectTransform, System.Action)>();
        readonly List<(float top, float h)> _rowPos = new List<(float, float)>();
        RectTransform _rows; Rect _rowsRect; float _pScroll, _pScrollMax, _pScroll0; int _pDragId = int.MinValue; Vector2 _pDragStart; bool _pDragged;
        string _panelKind;

        BomberIntent _intent; float _toastT;
        readonly Queue<(string, float)> _toasts = new Queue<(string, float)>();

        public bool Paused => _panelKind == "pause";
        float _scroll0; // snapped scroll used for drawing this frame (world px)

        // ================= build =================
        public void Init(BomberContext ctx, RenderTexture worldTexture)
        {
            _ctx = ctx;
            _btnScale = Mathf.Clamp(PlayerPrefs.GetFloat(MicrogameSave.Key(BomberGame.Id, "button_scale"), 1.3f), 0.8f, 1.6f);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = ctx.Spawn("gc26_bomber_Canvas", transform);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            _scaler = canvasGo.AddComponent<CanvasScaler>(); _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var bg = Img("Backdrop", canvasGo.transform, ctx.Art.UiWhite, new Color(0.024f, 0.035f, 0.063f)); Stretch(bg.rectTransform);
            _safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>(); _safe.SetParent(canvasGo.transform, false); Stretch(_safe);
            var t = _safe.transform;

            _world = new GameObject("World", typeof(RectTransform)).AddComponent<RawImage>(); _world.rectTransform.SetParent(t, false); _world.texture = worldTexture;
            _vignette = Img("Vignette", t, ctx.Art.UiVignette, new Color(0, 0, 0, 0.5f));
            _lowHp = Img("LowHp", t, ctx.Art.UiVignette, new Color(1f, 0.12f, 0.24f, 0f));
            _fade = Img("Fade", t, ctx.Art.UiWhite, Color.black);
            for (int i = 0; i < 4; i++) _border[i] = Img("Border", t, ctx.Art.UiWhite, Color.white);

            // HUD
            _hudBg = Img("Hud", t, ctx.Art.UiWhite, new Color(0.027f, 0.043f, 0.086f, 0.94f));
            _hudLine = Img("HudLine", t, ctx.Art.UiWhite, new Color(1f, 0.35f, 0.43f));
            for (int i = 0; i < 12; i++) _hp.Add(Img("Hp", t, ctx.Art.UiWhite, Color.red));
            _score = Txt("Score", t, 26, TextAnchor.MiddleRight, Color.white, FontStyle.Bold);
            _heatBg = Img("HeatBg", t, ctx.Art.UiWhite, new Color(0.055f, 0.08f, 0.15f));
            _heatFill = Img("HeatFill", t, ctx.Art.UiWhite, new Color(0.31f, 0.72f, 1f));
            _odMark = Img("OdMark", t, ctx.Art.UiWhite, new Color(1f, 0.89f, 0.6f));
            _heatText = Txt("HeatText", t, 16, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            _scrapIcon = Img("ScrapIcon", t, ctx.Art.UiWhite, new Color(1f, 0.81f, 0.36f));
            _scrap = Txt("Scrap", t, 18, TextAnchor.MiddleLeft, new Color(1f, 0.81f, 0.36f), FontStyle.Bold);
            _status = Txt("Status", t, 17, TextAnchor.MiddleLeft, new Color(0.66f, 0.71f, 0.84f), FontStyle.Bold);
            for (int i = 0; i < 7; i++) _route.Add(Img("Route", t, ctx.Art.UiWhite, Color.gray));
            _routeCur = Img("RouteCur", t, ctx.Art.UiRing, Color.white);
            _room = Txt("Room", t, 16, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            _weapon = Txt("Weapon", t, 15, TextAnchor.MiddleRight, new Color(0.5f, 0.89f, 1f), FontStyle.Bold);
            _combo = Txt("Combo", t, 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            _bossBg = Img("BossBg", t, ctx.Art.UiWhite, new Color(0.027f, 0.043f, 0.086f, 0.86f));
            _bossLine = Img("BossLine", t, ctx.Art.UiWhite, Color.white);
            _bossBack = Img("BossBack", t, ctx.Art.UiWhite, new Color(0.1f, 0.06f, 0.09f));
            _bossFill = Img("BossFill", t, ctx.Art.UiWhite, Color.white);
            for (int i = 0; i < _bossMarks.Length; i++) _bossMarks[i] = Img("BossMark", t, ctx.Art.UiWhite, Color.white);
            _bossName = Txt("BossName", t, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

            // control panel
            _panelBg = Img("ControlShade", t, ctx.Art.UiGradient, new Color(0.01f, 0.015f, 0.03f, 0.5f));
            _panelLine = Img("ControlPanelLine", t, ctx.Art.UiWhite, new Color(0.5f, 0.89f, 1f, 0f));
            AddBtn("tac", 62, 58, 36, "СБРОС", new Color(1f, 0.6f, 0.87f));
            AddBtn("emp", 150, 52, 30, "ЭМИ", new Color(0.62f, 0.82f, 1f));
            AddBtn("od", 240, 58, 36, "ФОРСАЖ", new Color(1f, 0.7f, 0.28f));
            AddBtn("dash", 66, 154, 48, "РЫВОК", new Color(0.5f, 0.89f, 1f));
            AddBtn("ab", 172, 166, 36, "РАЗГОН", new Color(0.5f, 0.89f, 1f));
            AddBtn("alt", 266, 166, 32, "ЗАРЯД", new Color(1f, 0.82f, 0.48f));
            AddBtn("pause", -34, 30, 24, "II", new Color(0.51f, 0.58f, 0.74f), atTop: true);
            AddBtn("swap", -34, 106, 22, "⇄", new Color(0.5f, 0.89f, 1f), atTop: true);
            AddBtn("use", 150, -44, 38, "E", new Color(0.43f, 1f, 0.69f));
            _joyHint = Img("JoyHint", t, ctx.Art.UiRing, new Color(0.5f, 0.89f, 1f, 0.5f));
            _joyHintText = Txt("JoyHintText", t, 15, TextAnchor.MiddleCenter, new Color(0.81f, 0.88f, 1f), FontStyle.Bold); _joyHintText.text = "Веди пальцем\nздесь";
            _joyBase = Img("JoyBase", t, ctx.Art.UiRing, new Color(0.5f, 0.89f, 1f, 0.55f));
            _joyKnob = Img("JoyKnob", t, ctx.Art.UiCircle, new Color(0.5f, 0.89f, 1f, 0.55f));

            _toastBg = Img("ToastBg", t, ctx.Art.UiWhite, new Color(0.04f, 0.06f, 0.12f, 0.88f));
            _toast = Txt("Toast", t, 17, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal); _toast.horizontalOverflow = HorizontalWrapMode.Wrap;

            // pause / end panel
            _panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>(); _panel.SetParent(t, false); Stretch(_panel);
            _panelDim = Img("Dim", _panel, ctx.Art.UiWhite, new Color(0.016f, 0.027f, 0.055f, 0.86f)); Stretch(_panelDim.rectTransform);
            _card = Img("Card", _panel, ctx.Art.UiWhite, new Color(0.05f, 0.08f, 0.14f, 1f));
            _cardLine = Img("CardLine", _panel, ctx.Art.UiWhite, new Color(0.5f, 0.89f, 1f));
            _panelTitle = Txt("Title", _panel, 32, TextAnchor.MiddleCenter, new Color(0.5f, 0.89f, 1f), FontStyle.Bold);
            _panelText = Txt("Body", _panel, 17, TextAnchor.UpperCenter, new Color(0.75f, 0.8f, 0.9f), FontStyle.Normal); _panelText.horizontalOverflow = HorizontalWrapMode.Wrap; _panelText.supportRichText = true;
            _rows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>(); _rows.SetParent(_panel, false); _rows.gameObject.AddComponent<RectMask2D>();
            _panel.gameObject.SetActive(false);

            BuildTutorialCard(t);
            _flash = Img("Flash", t, ctx.Art.UiWhite, new Color(1f, 0.24f, 0.31f, 0f)); Stretch(_flash.rectTransform);
        }

        void AddBtn(string id, float x, float y, float r, string label, Color col, bool atTop = false)
        {
            var t = _safe.transform;
            var b = new Btn { Id = id, Local = new Vector2(x, y), R = r, Col = col, AtTop = atTop };
            b.Glow = Img(id + "Glow", t, _ctx.Art.UiCircle, new Color(col.r, col.g, col.b, 0f));
            b.Bg = Img(id, t, _ctx.Art.UiCircle, new Color(0.027f, 0.043f, 0.086f, 0.72f));
            b.Cd = Img(id + "Cd", t, _ctx.Art.UiCircle, new Color(0, 0, 0, 0.5f));
            b.Cd.type = Image.Type.Filled; b.Cd.fillMethod = Image.FillMethod.Radial360; b.Cd.fillOrigin = (int)Image.Origin360.Top; b.Cd.fillClockwise = false;
            b.Rim = Img(id + "Rim", t, _ctx.Art.UiRing, col);
            b.LabelFont = r >= 46 ? 16 : label.Length > 5 ? 13 : 14;
            b.Label = Txt(id + "Label", t, b.LabelFont, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); b.Label.text = label;
            b.Sub = Txt(id + "Sub", t, 15, TextAnchor.MiddleCenter, col, FontStyle.Bold);
            _btns.Add(b);
        }

        // ================= layout =================
        void UpdateScale()
        {
            var sa = Screen.safeArea; float sw = Screen.width, sh = Screen.height;
            if (sw < 1f || sh < 1f) return;
            _safe.anchorMin = new Vector2(sa.xMin / sw, sa.yMin / sh); _safe.anchorMax = new Vector2(sa.xMax / sw, sa.yMax / sh); _safe.offsetMin = _safe.offsetMax = Vector2.zero;
            // pixels per UI unit: never smaller than needed to fit the reference layout, and on big screens
            // keep the physical size of a phone (so a tablet shows a much bigger room, not giant buttons)
            float fit = Mathf.Min(sa.width / MinW, sa.height / MinH);
            float dpi = Screen.dpi, phys = dpi >= 120f ? dpi * InchPerUnit : fit;
            bool tablet = dpi >= 120f && Mathf.Min(sa.width, sa.height) / dpi > 4.2f; // short side over ~4.2" = tablet
            if (tablet) phys *= 1.3f;                                                   // held further away: bigger UI
            float s = Mathf.Max(0.1f, Mathf.Min(fit, phys));
            if (!Mathf.Approximately(_scaler.scaleFactor, s)) _scaler.scaleFactor = s;
            float fw = sa.width / s, fh = sa.height / s, inset = sa.yMin / s;
            if (Mathf.Abs(fw - _fw) > 0.5f || Mathf.Abs(fh - _fh) > 0.5f || Mathf.Abs(inset - _bottomInset) > 0.5f) { _bottomInset = inset; _uiS = s; Layout(fw, fh); }
        }

        void Layout(float fw, float fh)
        {
            _fw = fw; _fh = fh;
            // HUD (full width, right items glued to the right edge)
            Place(_hudBg.rectTransform, 0, 0, fw, Top); Place(_hudLine.rectTransform, 0, 0, fw, 3);
            for (int i = 0; i < _hp.Count; i++) Place(_hp[i].rectTransform, 12 + i * 24, 12, 20, 18);
            Place(_score.rectTransform, fw - 260, 8, 190, 30);
            float barW = Mathf.Min(fw - 120, 640f);
            Place(_heatBg.rectTransform, 12, 44, barW, 20); Place(_heatFill.rectTransform, 12, 44, 0, 20);
            Place(_odMark.rectTransform, 12 + barW * 0.8f - 1, 40, 3, 28); Place(_heatText.rectTransform, 20, 44, 200, 20);
            Place(_scrapIcon.rectTransform, fw - 96, 48, 12, 12); Place(_scrap.rectTransform, fw - 78, 42, 70, 24);
            Place(_status.rectTransform, 12, 70, fw - 24, 24);
            for (int i = 0; i < _route.Count; i++) Place(_route[i].rectTransform, 14 + i * 22, 101, 15, 15);
            Place(_room.rectTransform, 14 + 7 * 22 + 6, 98, 170, 22);
            Place(_weapon.rectTransform, fw - 280, 98, 212, 22);

            // control panel (full width at the bottom)
            // controls: the left cluster scales with the player's button size (but never wider than ~62% of the screen)
            float k = Mathf.Min(_btnScale, fw * 0.62f / 300f), panelH = PanelH * k, py = fh - panelH;
            Place(_panelBg.rectTransform, 0, py - 40f, fw, panelH + 40f + _bottomInset); Place(_panelLine.rectTransform, 0, py, fw, 0);
            foreach (var b in _btns)
            {
                float bk = b.AtTop ? 1f : k; b.Rad = b.R * bk;
                b.Pos = b.AtTop ? new Vector2(b.Local.x < 0 ? fw + b.Local.x : b.Local.x, b.Local.y) : new Vector2(b.Local.x * k, fh - (PanelH - b.Local.y) * k);
                Circle(b.Glow.rectTransform, b.Pos, b.Rad + 8 * bk); Circle(b.Bg.rectTransform, b.Pos, b.Rad); Circle(b.Cd.rectTransform, b.Pos, b.Rad); Circle(b.Rim.rectTransform, b.Pos, b.Rad);
                Place(b.Sub.rectTransform, b.Pos.x - b.Rad, b.Pos.y, b.Rad * 2, b.Rad);
                b.Label.fontSize = Mathf.RoundToInt(b.LabelFont * bk); b.Sub.fontSize = Mathf.RoundToInt(15 * bk);
            }
            var jh = new Vector2(fw - 112 * k, fh - PanelH * k / 2f);
            Circle(_joyHint.rectTransform, jh, 62 * k); Place(_joyHintText.rectTransform, jh.x - 80, jh.y - 30, 160, 60);

            // the room between the HUD and the panel: (almost) full width; if the whole room would be too small,
            // show a vertical window of it that follows the drone (at most 1.35x bigger than "whole room" scale)
            float areaTop = Top + Pad, areaH = fh - Top - Pad + _bottomInset, areaW = fw - Pad * 2f; // reaches the physical bottom edge
            float fitAll = Mathf.Min(areaW / BomberData.W, (fh - Top - Pad - panelH) / BomberData.H);
            // full width on every device (phones and tablets); the view scrolls vertically. Only on very wide
            // screens keep at least ~480 world px visible in height.
            _ws = Mathf.Min(areaW / BomberData.W, Mathf.Max(fitAll, areaH / 480f));
            // world px hidden under the controls: let the view scroll that far below the room (station shaft there)
            float under = Mathf.Ceil((panelH + _bottomInset) / _ws / 2f) * 2f;
            _viewTotal = BomberData.H + under;
            _viewH = Mathf.Min(_viewTotal, Mathf.Floor(areaH / _ws / 2f) * 2f);
            float ww = BomberData.W * _ws, wh = _viewH * _ws;
            _wx = (fw - ww) / 2f; _wy = areaTop; _scrollInit = false;
            _world.texture = _ctx.View.SetViewHeight(_viewTotal);
            foreach (var rt in new[] { _world.rectTransform, _fade.rectTransform, _vignette.rectTransform, _lowHp.rectTransform }) Place(rt, _wx, _wy, ww, wh);
            const float bw = 3f;
            Place(_border[0].rectTransform, _wx - bw, _wy - bw, ww + bw * 2, bw); Place(_border[1].rectTransform, _wx - bw, _wy + wh, ww + bw * 2, 0);
            Place(_border[2].rectTransform, _wx - bw, _wy, bw, wh); Place(_border[3].rectTransform, _wx + ww, _wy, bw, wh);
            Place(_combo.rectTransform, 0, _wy + 6, fw, 30);
            _ww = ww; _bossW = Mathf.Min(ww - 40f, 520f); float bx = (fw - _bossW) / 2f;
            Place(_bossBg.rectTransform, bx - 10f, _wy + 6f, _bossW + 20f, 52f); Place(_bossLine.rectTransform, bx - 10f, _wy + 6f, _bossW + 20f, 3f);
            Place(_bossBack.rectTransform, bx, _wy + 16f, _bossW, 12f); Place(_bossFill.rectTransform, bx, _wy + 16f, _bossW, 12f);
            Place(_bossName.rectTransform, bx, _wy + 30f, _bossW, 24f);

            float tw = Mathf.Min(fw - 80, 520f);
            Place(_toastBg.rectTransform, (fw - tw) / 2f, py - 76, tw, 60); Place(_toast.rectTransform, (fw - tw) / 2f + 10, py - 76, tw - 20, 60);
            Place(_flash.rectTransform, 0, 0, fw, fh + _bottomInset);

            LayoutPanel(fw, fh);
            LayoutTutorialCard(ww);
        }

        void LayoutPanel(float fw, float fh)
        {
            float cw = Mathf.Min(fw - 60, 520f), cx = (fw - cw) / 2f, cy = Mathf.Max(Top, fh / 2f - 260f);
            // panel: rows of buttons that scroll with a finger when they do not fit the screen
            float gap = _btnH > 100f ? 14f : _btnH < 59f ? 6f : 16f, total = 0f; _rowPos.Clear();
            foreach (var (rt0, act0) in _panelButtons) { float h = act0 == null && _btnH > 56f ? 40f : _btnH; _rowPos.Add((total, h)); total += h + gap; }
            cw = Mathf.Min(fw - 30, _btnH > 100f || _btnH < 59f ? 600f : 520f); cx = (fw - cw) / 2f;
            float cardH = Mathf.Min(_btnTop + 20f + total, fh - 16f);
            cy = Mathf.Clamp(fh / 2f - cardH / 2f, 8f, Mathf.Max(8f, fh - cardH - 8f));
            Place(_card.rectTransform, cx, cy, cw, cardH); Place(_cardLine.rectTransform, cx, cy, cw, 3);
            Place(_panelTitle.rectTransform, cx + 10, cy + 16, cw - 20, 46); Place(_panelText.rectTransform, cx + 20, cy + 64, cw - 40, _btnTop - 70f);
            _rowsRect = new Rect(cx + 16, cy + _btnTop, cw - 32, Mathf.Max(40f, cardH - _btnTop - 12f));
            Place(_rows, _rowsRect.x, _rowsRect.y, _rowsRect.width, _rowsRect.height);
            _pScrollMax = Mathf.Max(0f, total - gap - _rowsRect.height); _pScroll = Mathf.Clamp(_pScroll, 0f, _pScrollMax);
            LayoutRows();
        }

        void LayoutRows()
        {
            for (int i = 0; i < _panelButtons.Count && i < _rowPos.Count; i++) Place(_panelButtons[i].rt, 0f, _rowPos[i].top - _pScroll, _rowsRect.width, _rowPos[i].h);
        }

        void PanelPointer(int id, Vector2 p, bool began, bool ended)
        {
            if (began) { _pDragId = id; _pDragStart = p; _pScroll0 = _pScroll; _pDragged = false; return; }
            if (id != _pDragId) return;
            float dy = p.y - _pDragStart.y;
            if (Mathf.Abs(dy) > 10f) _pDragged = true;
            if (_pDragged && _pScrollMax > 0f) { _pScroll = Mathf.Clamp(_pScroll0 - dy, 0f, _pScrollMax); LayoutRows(); }
            if (!ended) return;
            _pDragId = int.MinValue;
            if (_pDragged || !_rowsRect.Contains(p)) return;
            for (int i = 0; i < _panelButtons.Count && i < _rowPos.Count; i++)
            {
                float top = _rowsRect.y + _rowPos[i].top - _pScroll;
                if (p.y >= top && p.y <= top + _rowPos[i].h) { _panelButtons[i].act?.Invoke(); return; }
            }
        }

        // ================= panels =================
        void ShowPanel(string kind, string title, string body, params (string label, System.Action act)[] buttons) => ShowPanelEx(kind, title, body, 60f, 270f, false, buttons);

        /// <summary>Panel with a list of buttons; rich = left-aligned multi-line cards (rewards, workbench). act == null: a header row.</summary>
        void ShowPanelEx(string kind, string title, string body, float btnH, float btnTop, bool rich, params (string label, System.Action act)[] buttons)
        {
            if (kind != _panelKind) _pScroll = 0f;
            _panelTitle.color = new Color(0.5f, 0.89f, 1f);
            _panelKind = kind; _panel.gameObject.SetActive(true); _panelTitle.text = title; _panelText.text = body; _btnH = btnH; _btnTop = btnTop;
            foreach (var b in _panelButtons) Destroy(b.rt.gameObject);
            _panelButtons.Clear();
            for (int i = 0; i < buttons.Length; i++)
            {
                bool header = buttons[i].act == null, primary = !rich && i == 0;
                var img = Img("Btn", _rows, _ctx.Art.UiWhite, header ? new Color(0, 0, 0, 0) : primary ? new Color(0.5f, 0.89f, 1f) : rich ? new Color(0.09f, 0.13f, 0.22f) : new Color(0.15f, 0.2f, 0.32f));
                var tx = Txt("Label", img.transform, rich ? 16 : 20, rich ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, primary ? new Color(0.02f, 0.07f, 0.1f) : Color.white, rich ? FontStyle.Normal : FontStyle.Bold);
                tx.supportRichText = true; tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.text = buttons[i].label; Stretch(tx.rectTransform);
                if (rich) { tx.rectTransform.offsetMin = new Vector2(14, 4); tx.rectTransform.offsetMax = new Vector2(-14, -4); }
                if (rich && !header) { var edge = Img("Edge", img.transform, _ctx.Art.UiWhite, new Color(0.5f, 0.89f, 1f, 0.8f)); edge.rectTransform.anchorMin = new Vector2(0, 0); edge.rectTransform.anchorMax = new Vector2(0, 1); edge.rectTransform.offsetMin = Vector2.zero; edge.rectTransform.offsetMax = new Vector2(4, 0); }
                _panelButtons.Add((img.rectTransform, buttons[i].act));
            }
            if (_fw > 0f) LayoutPanel(_fw, _fh);
        }

        static string Hex(Color32 c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}";

        void ShowReward(BomberReward r)
        {
            var w = _ctx.World; var opts = new List<(string, System.Action)>();
            for (int i = 0; i < r.Options.Count; i++)
            {
                var it = r.Options[i]; int idx = i; string slot = w.SlotInfo(it);
                bool busy = slot.StartsWith("Заменит") || slot.StartsWith("Слоты заняты") || slot.StartsWith("Улетит");
                opts.Add(($"<color=#ffb347><size=13>{it.Tag.ToUpper()}</size></color>\n<size=21><b>{it.Name}</b></size>\n<color=#a9b6d6>{it.Desc}</color>\n<color={(busy ? "#ffb347" : "#6dffb0")}><size=14>{slot}</size></color>",
                    () => { w.ChooseReward(idx); HidePanel(); }));
            }
            ShowPanelEx("reward", r.Title, r.Sub, 150f, 110f, true, opts.ToArray());
        }

        void ShowBench()
        {
            var w = _ctx.World; var p = w.P; var rows = new List<(string, System.Action)>();
            var f = w.FusionFor();
            if (f.id != null)
            {
                var wa = p.Weapons.Find(x => x.Id == f.a); var wb = p.Weapons.Find(x => x.Id == f.b);
                bool ok = wa.UpSum >= BomberItems.FuseNeed && wb.UpSum >= BomberItems.FuseNeed;
                var fd = BomberItems.Weapons[f.id];
                rows.Add(($"<color=#ffd65c><b>СЛИЯНИЕ: {fd.Name}</b></color>  {(ok ? $"<color=#ffd65c>{w.FusePrice} скрапа</color>" : $"<color=#ff8a9a>нужно ▲{BomberItems.FuseNeed} на обоих</color>")}\n<size=13><color=#a9b6d6>{fd.Desc} {BomberItems.Weapons[f.a].Name} ▲{wa.UpSum}, {BomberItems.Weapons[f.b].Name} ▲{wb.UpSum}</color></size>",
                    ok ? (System.Action)(() => { if (w.Fuse()) HidePanelBench(); else ShowBench(); }) : () => { }));
            }
            for (int wi = 0; wi < p.Weapons.Count; wi++)
            {
                var wpn = p.Weapons[wi]; int idx = wi;
                rows.Add(($"<size=19><b>{wpn.Def.Name}</b></size>  <color=#ffd65c>▲{wpn.UpSum}</color>", null));
                foreach (var u in BomberItems.Upgrades)
                {
                    int l = wpn.Up[u.id]; string pips = new string('●', l) + new string('○', 3 - l); string key = u.id;
                    string desc = u.id == "spec" ? BomberItems.SpecText[wpn.Def.Spec] : u.desc;
                    int price = w.UpgradePrice(wpn, key); bool can = l < 3 && p.Scrap >= price;
                    rows.Add(($"<b>{u.name}</b> <color=#ffb347>{pips}</color>   {(l >= 3 ? "<color=#6dffb0>максимум</color>" : $"<color={(can ? "#ffd65c" : "#ff8a9a")}>{price} скрапа</color>")}\n<size=13><color=#a9b6d6>{desc} за уровень</color></size>",
                        () => { w.BuyUpgrade(idx, key); ShowBench(); }));
                }
            }
            rows.Add(("<b>Закрыть</b>", HidePanelBench));
            ShowPanelEx("bench", "Верстак", $"Скрап: {p.Scrap}" + (w.Room.BenchFree ? " · первое улучшение здесь за полцены" : ""), 58f, 90f, true, rows.ToArray());
        }

        void HidePanelBench() { _ctx.World.CloseBench(); HidePanel(); }

        void HidePanel() { _panelKind = null; _panel.gameObject.SetActive(false); }

        void SetButtonScale(float v)
        {
            _btnScale = Mathf.Clamp(Mathf.Round(v * 10f) / 10f, 0.8f, 1.6f);
            PlayerPrefs.SetFloat(MicrogameSave.Key(BomberGame.Id, "button_scale"), _btnScale); PlayerPrefs.Save();
            _fw = -1f; UpdateScale(); Pause();
        }

        public void Toast(string msg, float t) { _toasts.Enqueue((msg, t)); if (_toastT <= 0f) NextToast(); }
        void NextToast()
        {
            if (_toasts.Count == 0) { _toastT = 0f; _toast.text = ""; _toastBg.enabled = false; return; }
            var (m, t) = _toasts.Dequeue(); _toast.text = m; _toastT = t; _toastBg.enabled = true;
        }

        // ================= input =================
        public BomberIntent ReadIntent()
        {
            UpdateScale();
            _intent = new BomberIntent();
            var w = _ctx.World;
            var kb = Keyboard.current; var kbMove = Vector2.zero;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1; if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y -= 1; if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y += 1;
                if (_panelKind == null)
                {
                    _intent.Dash |= kb.spaceKey.wasPressedThisFrame; _intent.Vent |= kb.rKey.wasPressedThisFrame; _intent.Emp |= kb.gKey.wasPressedThisFrame;
                    _intent.Ability |= kb.leftShiftKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame; _intent.ForsageToggle |= kb.vKey.wasPressedThisFrame;
                    _intent.Use |= kb.eKey.wasPressedThisFrame; _intent.Swap |= kb.qKey.wasPressedThisFrame; _intent.AltPressed |= Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
                }
                if (kb.escapeKey.wasPressedThisFrame) { if (_panelKind == null) Pause(); else if (_panelKind == "pause") HidePanel(); }
            }
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
            if (w.Reward != null && _panelKind == null) ShowReward(w.Reward);
            if (w.BenchOpen && _panelKind == null) ShowBench();
            if (w.PendingPanel != null) { if (_panelKind == null) ShowStation(w.PendingPanel); w.PendingPanel = null; }
            if (_panelKind != null && mouse != null) { float wy = mouse.scroll.ReadValue().y; if (Mathf.Abs(wy) > 0.01f && _pScrollMax > 0f) { _pScroll = Mathf.Clamp(_pScroll - Mathf.Sign(wy) * 60f, 0f, _pScrollMax); LayoutRows(); } }
            return _intent;
        }

        void Pointer(int id, Vector2 screen, bool began, bool ended)
        {
            if (_fw <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, screen, null, out var local)) return;
            var r = _safe.rect; var p = new Vector2(local.x - r.xMin, r.yMax - local.y); // top-left origin, y down
            if (_panelKind != null) { PanelPointer(id, p, began, ended); return; }
            if (began)
            {
                if (TrySkip(p)) return;
                foreach (var b in _btns)
                {
                    if (!b.Visible || Vector2.Distance(b.Pos, p) > b.Rad + 10f) continue;
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
                        case "use": _intent.Use = true; break;
                        case "swap": _intent.Swap = true; break;
                    }
                    return;
                }
                if (_joyId == int.MinValue && p.y > Top) { _joyId = id; _joyStart = _joyNow = p; }
                return;
            }
            if (id == _joyId) _joyNow = p;
            if (ended) { if (id == _joyId) _joyId = int.MinValue; _held.Remove(id); }
        }

        // ================= per-frame =================
        public void Sync(float dt)
        {
            UpdateScale();
            if (_toastT > 0f) { _toastT -= dt; if (_toastT <= 0f) NextToast(); } else if (_toastBg.enabled && _toasts.Count == 0) NextToast();
            var w = _ctx.World; var p = w.P; var core = w.Core;

            // vertical camera window: follow the drone, clamp to the room, snap to whole texels (crisp pixels)
            float maxScroll = Mathf.Max(0f, _viewTotal - _viewH);
            _scrollTarget = Mathf.Clamp(p.Y - _viewH * 0.42f, 0f, maxScroll); // drone sits a bit above the middle, clear of the controls
            if (!_scrollInit || w.Room.Fade > 0.3f) { _scroll = _scrollTarget; _scrollInit = true; }
            else _scroll = Mathf.Lerp(_scroll, _scrollTarget, 1f - Mathf.Exp(-6f * dt));
            float snapped = Mathf.Round(_scroll / 2f) * 2f;
            _world.uvRect = new Rect(0f, (_viewTotal - snapped - _viewH) / _viewTotal, 1f, _viewH / _viewTotal);
            _scroll0 = snapped;

            _fade.color = new Color(0, 0, 0, Mathf.Clamp01(w.Room.Fade / 0.35f));
            _lowHp.color = new Color(1f, 0.12f, 0.24f, p.Hp <= 2 && w.Phase == BomberPhase.Play ? 0.35f + 0.2f * Mathf.Sin(w.Time * 6f) : 0f);
            _flash.color = new Color(1f, 0.24f, 0.31f, Mathf.Max(0f, w.Flash));
            var acc = Color.Lerp((Color)w.Theme.Accent, Color.white, 0.25f); acc.a = 0.85f;
            bool hub = w.Mode == BomberMode.Hub, tut = w.Mode == BomberMode.Tutorial;
            foreach (var bi in _border) bi.color = p.Hp <= 2 ? new Color(1f, 0.25f, 0.35f, 0.6f + 0.3f * Mathf.Sin(w.Time * 6f)) : acc;
            _hudLine.color = p.Hp <= 2 ? new Color(1f, 0.13f, 0.27f) : new Color(1f, 0.35f, 0.43f);
            for (int i = 0; i < _hp.Count; i++)
            {
                bool show = i < p.MaxHp, on = i < p.Hp, low = p.Hp <= 2 && on && Mathf.Sin(w.Time * 8f) > 0f;
                _hp[i].enabled = show; _hp[i].color = on ? (low ? new Color(1f, 0.6f, 0.66f) : new Color(1f, 0.35f, 0.43f)) : new Color(0.16f, 0.11f, 0.15f);
            }
            _score.text = hub ? $"{w.Meta.Data} ДАННЫХ" : w.Score.ToString("N0").Replace(',', ' ');
            _score.color = hub ? new Color(0.71f, 0.55f, 1f) : Color.white;
            _heatFill.rectTransform.sizeDelta = new Vector2(_heatBg.rectTransform.sizeDelta.x * core.Normalized, 20);
            _heatFill.color = core.State == CoreState.Overheated ? (Mathf.Sin(w.Time * 20f) > 0 ? new Color(1f, 0.13f, 0.27f) : new Color(0.4f, 0.06f, 0.13f)) : core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : new Color(0.31f, 0.72f, 1f);
            _heatText.text = $"ЯДРО {Mathf.RoundToInt(core.Heat)}%";
            _heatBg.enabled = _heatFill.enabled = _odMark.enabled = _heatText.enabled = !hub;
            _scrap.text = p.Scrap.ToString();
            _status.text = w.StatusLine;
            _status.color = core.State == CoreState.Overheated ? new Color(1f, 0.42f, 0.5f) : w.Governor.Forsage || core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : p.AbT > 0f ? new Color(0.5f, 0.89f, 1f) : new Color(0.66f, 0.71f, 0.84f);
            var plan = w.Plan[w.Sector] ?? new List<List<string>>();
            _routeCur.enabled = !hub && !tut;
            for (int i = 0; i < _route.Count; i++)
            {
                bool show = i < plan.Count && !hub && !tut; _route[i].enabled = show; if (!show) continue;
                var col = (Color)BomberData.RoomColor[plan[i][0]];
                _route[i].color = i == w.Step ? col : i < w.Step ? new Color(col.r, col.g, col.b, 0.35f) : new Color(col.r * 0.45f, col.g * 0.45f, col.b * 0.5f);
            }
            Circle(_routeCur.rectTransform, new Vector2(14 + w.Step * 22 + 7.5f, 108.5f), 12);
            var curT = w.Room.Type == "start" ? "combat" : w.Room.Type;
            float roomX = hub || tut ? 14f : 14 + 7 * 22 + 6; _room.rectTransform.anchoredPosition = new Vector2(roomX, -98f);
            _room.text = hub ? "ХАБ СТАНЦИИ" : tut ? "ОБУЧЕНИЕ" : $"С{w.Sector + 1} · {BomberData.RoomLabel[curT]}";
            _room.color = hub ? new Color(0.43f, 1f, 0.69f) : tut ? new Color(0.71f, 0.55f, 1f) : (Color)BomberData.RoomColor[curT];
            var wd = p.Weapon.Def; var chd = w.CharDef;
            _weapon.text = (hub ? chd.Name + " · " : "") + wd.Name + (p.Weapon.UpSum > 0 ? $" ▲{p.Weapon.UpSum}" : "") + (p.Weapons.Count > 1 ? $"  {p.Cur + 1}/2" : "");
            _weapon.color = hub ? (Color)chd.Color : wd.Fused ? new Color(1f, 0.84f, 0.36f) : new Color(0.5f, 0.89f, 1f);
            _combo.text = w.Combo >= 2 ? $"КОМБО ×{w.Combo}{(w.Combo >= 10 ? " · В УДАРЕ" : "")}" : "";
            var boss = w.CurrentBoss;
            foreach (var g in new Graphic[] { _bossBg, _bossLine, _bossBack, _bossFill, _bossName }) g.enabled = boss != null;
            for (int i = 0; i < _bossMarks.Length; i++) _bossMarks[i].enabled = boss != null && i < boss.Boss.Phases.Length;
            _combo.rectTransform.anchoredPosition = new Vector2(0f, -(_wy + (boss != null ? 64f : 6f)));
            if (boss != null)
            {
                var bc = (Color)boss.Boss.Color; float bx = (_fw - _bossW) / 2f;
                _bossLine.color = bc; _bossFill.color = bc;
                _bossFill.rectTransform.sizeDelta = new Vector2(_bossW * Mathf.Clamp01(boss.Hp / boss.MaxHp), 12f);
                for (int i = 0; i < boss.Boss.Phases.Length && i < _bossMarks.Length; i++) Place(_bossMarks[i].rectTransform, bx + _bossW * boss.Boss.Phases[i] - 1f, _wy + 14f, 2f, 16f);
                _bossName.text = $"{boss.Boss.Name.ToUpper()} · ФАЗА {boss.Phase + 1}";
            }
            _combo.color = w.Combo >= 10 ? new Color(1f, 0.84f, 0.36f) : Color.white;

            foreach (var b in _btns)
            {
                float ready = 1f; string sub = ""; b.Visible = true;
                switch (b.Id)
                {
                    case "dash": ready = Mathf.Clamp01(1f - p.DashCd / Mathf.Max(0.2f, 0.8f * p.DashK)); break;
                    case "ab": (ready, sub) = w.AbilityState; b.Label.text = w.AbilityLabel; b.Col = (Color)w.CharDef.Color; break;
                    case "tac": ready = Mathf.Clamp01(1f - p.TacCd / 8f); if (ready < 1f) sub = Mathf.CeilToInt(p.TacCd).ToString(); break;
                    case "emp": ready = p.Emp > 0 ? 1f : 0f; sub = "×" + p.Emp; break;
                    case "od": ready = core.CanFire ? 1f : 0f; b.Label.text = w.Governor.Forsage ? "ФОРСАЖ\nВКЛ" : "ФОРСАЖ"; if (!core.CanFire) sub = core.OverheatLeft.ToString("0.0"); break;
                    case "alt": ready = p.AltCd <= 0f ? 1f : 0.3f; b.Visible = w.AltLabel != null; if (b.Visible) b.Label.text = w.AltLabel; break;
                    case "use": b.Visible = w.NearProp() != null || w.NearNpc() != null; break;
                    case "swap": b.Visible = p.Weapons.Count > 1; break;
                }
                if (hub && (b.Id == "tac" || b.Id == "emp" || b.Id == "od" || b.Id == "ab" || b.Id == "alt" || b.Id == "swap")) b.Visible = false;
                if (!w.TutShows(b.Id)) b.Visible = false;
                foreach (var g in new Graphic[] { b.Bg, b.Cd, b.Rim, b.Glow, b.Label, b.Sub }) g.enabled = b.Visible;
                bool down = _held.ContainsValue(b.Id), isReady = ready >= 1f;
                b.Cd.fillAmount = 1f - ready;
                b.Bg.color = down ? new Color(0.16f, 0.24f, 0.4f, 0.8f) : new Color(0.027f, 0.043f, 0.086f, b.AtTop ? 0.72f : 0.45f);
                b.Rim.color = isReady ? b.Col : new Color(0.43f, 0.51f, 0.67f, 0.6f);
                b.Label.color = isReady ? Color.white : new Color(0.54f, 0.59f, 0.71f);
                Place(b.Label.rectTransform, b.Pos.x - b.Rad, b.Pos.y - b.Rad + (sub.Length > 0 ? -2 : 4) * b.Rad / b.R, b.Rad * 2, b.Rad * 2 - 10 * b.Rad / b.R);
                b.Sub.text = sub;
                b.Glow.color = new Color(b.Col.r, b.Col.g, b.Col.b, tut && w.TutButton == b.Id ? 0.45f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f) : b.Id == "od" && w.Governor.Forsage ? 0.35f + 0.25f * Mathf.Sin(w.Time * 10f) : 0f);
            }

            bool joy = _joyId != int.MinValue;
            _joyBase.enabled = _joyKnob.enabled = joy; _joyHint.enabled = _joyHintText.enabled = !joy && _panelKind == null;
            if (joy) { Circle(_joyBase.rectTransform, _joyStart, 60); Circle(_joyKnob.rectTransform, _joyStart + Vector2.ClampMagnitude(_joyNow - _joyStart, 56f), 26); }
            else _joyHint.color = new Color(0.5f, 0.89f, 1f, 0.4f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f));

            int n = 0;
            foreach (var t in w.Texts)
            {
                if (n >= 40) break;
                var tx = GetText(_floatTexts, n++); tx.text = t.S; tx.fontSize = Mathf.RoundToInt((t.Big ? 30 : t.S.Length <= 3 ? 16 : 14) * Mathf.Clamp(_ws, 0.85f, 1.6f));
                var c = (Color)t.Color; c.a = Mathf.Min(1f, t.T * 2f); tx.color = c;
                float ty = t.Y - _scroll0; if (ty < 0f || ty > _viewH) { tx.enabled = false; continue; }
                Place(tx.rectTransform, _wx + t.X * _ws - 150, _wy + ty * _ws - 15, 300, 30);
            }
            for (int i = n; i < _floatTexts.Count; i++) _floatTexts[i].enabled = false;
            int m = 0;
            foreach (var d in w.Doors)
            {
                var tx = GetText(_doorLabels, m++); tx.text = BomberData.RoomLabel[d.Type]; tx.fontSize = 15; tx.color = (Color)BomberData.RoomColor[d.Type];
                float dy = BomberData.Cell + 4 - _scroll0; tx.enabled = dy > 0f && dy < _viewH;
                Place(tx.rectTransform, _wx + (d.X0 + d.X1) / 2f * _ws - 80, _wy + dy * _ws, 160, 22);
            }
            for (int i = m; i < _doorLabels.Count; i++) _doorLabels[i].enabled = false;
            int q = 0; var near = w.NearProp();
            foreach (var pr in w.Props)
            {
                if (pr.Opened || pr.Sold) continue;
                string txt; Color col;
                switch (pr.Kind)
                {
                    case "station": { var st = BomberMetaData.Station(pr.Id); txt = pr == near ? $"{st.Name}\n<size=12>нажми E</size>" : st.Name; col = (Color)st.Color; break; }
                    case "cell": { var npc = BomberMetaData.Npc(pr.Id); txt = w.Room.Cleared ? $"{npc.Name}\n<size=12>{(pr == near ? "E — освободить" : "камера открыта")}</size>" : $"{npc.Name}\n<size=12>зачисти комнату</size>"; col = (Color)npc.Color; break; }
                    case "shop": int price = w.PriceOf(pr); txt = $"{pr.Item.Name}\n<color={(p.Scrap >= price ? "#ffd65c" : "#ff8a8a")}>{price} скрапа</color>" + (pr == near ? $"\n<size=12><color=#a9b6d6>{pr.Item.Desc}</color>\n<color=#ffb347>{w.SlotInfo(pr.Item)}</color></size>" : ""); col = new Color(0.86f, 0.9f, 1f); break;
                    case "chest": txt = pr == near ? "Сундук · нажми E" : "Сундук"; col = new Color(0.5f, 0.89f, 1f); break;
                    case "altar": txt = pr == near ? "Алтарь: −1 корпус за награду · E" : "Алтарь риска"; col = new Color(1f, 0.35f, 0.82f); break;
                    default: txt = pr == near ? "Верстак · нажми E" : "Верстак"; col = new Color(1f, 0.7f, 0.28f); break;
                }
                float ty = pr.Y - 34f - _scroll0; if (ty < 0f || ty > _viewH) continue;
                var tx = GetText(_propLabels, q++); tx.supportRichText = true; tx.text = txt; tx.fontSize = 14; tx.color = col; tx.alignment = TextAnchor.LowerCenter;
                Place(tx.rectTransform, _wx + pr.X * _ws - 140, _wy + ty * _ws - 90, 280, 90);
            }
            foreach (var hn in w.Npcs)
            {
                var d = BomberMetaData.Npc(hn.Id); float ty = hn.Y - 18f - _scroll0; if (ty < 0f || ty > _viewH) continue;
                var tx = GetText(_propLabels, q++); tx.supportRichText = true; tx.text = d.Name + (w.NearNpc() == hn ? "\n<size=12>E — поговорить</size>" : ""); tx.fontSize = 13; tx.color = (Color)d.Color; tx.alignment = TextAnchor.LowerCenter;
                Place(tx.rectTransform, _wx + hn.X * _ws - 140, _wy + ty * _ws - 90, 280, 90);
            }
            for (int i = q; i < _propLabels.Count; i++) _propLabels[i].enabled = false;
            SyncTutorialCard(w);
            // elite affix tags and floor hazard hints
            int g2 = 0;
            foreach (var e in w.Enemies)
            {
                if (!e.Elite || e.Alpha < 0.5f) continue;
                float ty = e.Y - e.R - 12f - _scroll0; if (ty < 0f || ty > _viewH) continue;
                var tx = GetText(_tagLabels, g2++); tx.text = e.Affix != null ? BomberData.Affixes[e.Affix].Name.ToUpper() : "ЭЛИТА"; tx.fontSize = 12;
                tx.color = e.Affix != null ? (Color)BomberData.Affixes[e.Affix].Color : new Color(1f, 0.84f, 0.36f);
                Place(tx.rectTransform, _wx + e.X * _ws - 80, _wy + ty * _ws - 10, 160, 20);
            }
            foreach (var z in w.Zones)
            {
                if (z.Kind != "hot") continue;
                float ty = z.Y + 10f - _scroll0; if (ty < 0f || ty > _viewH) continue;
                var tx = GetText(_tagLabels, g2++); tx.text = "ЖАР +НАГРЕВ"; tx.fontSize = 12; tx.color = new Color(1f, 0.67f, 0.35f, 0.8f);
                Place(tx.rectTransform, _wx + (z.X + z.W / 2f) * _ws - 80, _wy + ty * _ws - 10, 160, 20);
            }
            for (int i = g2; i < _tagLabels.Count; i++) _tagLabels[i].enabled = false;
        }

        Text GetText(List<Text> list, int i)
        {
            while (list.Count <= i) { var t = Txt("Float", _safe, 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); list.Add(t); var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); }
            var tx = list[i]; tx.enabled = true; tx.transform.SetSiblingIndex(_fade.transform.GetSiblingIndex() + 1); return tx;
        }

        // ================= helpers =================
        Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false; return img;
        }

        Text Txt(string name, Transform parent, int size, TextAnchor align, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = _font; t.fontSize = size; t.alignment = align; t.color = color; t.fontStyle = style; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }

        /// <summary>Places a rect by its top-left corner in UI units inside the safe area (y down).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static void Circle(RectTransform rt, Vector2 c, float r) => Place(rt, c.x - r, c.y - r, r * 2f, r * 2f);
        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    }
}
