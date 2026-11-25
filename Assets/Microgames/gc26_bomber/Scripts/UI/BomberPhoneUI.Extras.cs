using System.Collections.Generic;
using System.Text;
using GC26.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Bomber
{
    // Objective arrows, control layout editor (drag buttons, left-handed mode, opacity), drag-to-aim for tactics and the
    // Sapper's dynamite, the bug journal, developer tools and the FPS counter.
    public sealed partial class BomberPhoneUI
    {
        // ---- arrows ----
        readonly List<Image> _arrows = new List<Image>(); readonly List<Text> _arrowLabels = new List<Text>(); int _arrowN, _arrowLabelN;

        // ---- control layout ----
        readonly Dictionary<string, Vector2> _custom = new Dictionary<string, Vector2>(); // button id -> position / screen size
        bool _leftHand, _editCtl; float _btnAlpha = 1f;
        Btn _dragBtn; int _dragBtnTouch = int.MinValue;
        Image _editBg; Text _editHint; readonly List<(Image bg, Text tx, Rect r, System.Action act)> _editBtns = new List<(Image, Text, Rect, System.Action)>();

        // ---- drag-to-aim ----
        int _aimTouch = int.MinValue; Btn _aimBtn; Vector2 _aimNow; Image _aimRing, _aimDot, _aimCancel;

        // ---- debug ----
        Text _fps, _errBadge; bool _showFps; float _fpsT, _fpsAcc; int _fpsN; float _errToastT;
        bool DevTools => Application.isEditor || Debug.isDebugBuild;
        string Pref(string k) => MicrogameSave.Key(BomberGame.Id, k);

        void BuildExtras(Transform t)
        {
            _aimRing = Img("AimRing", t, _ctx.Art.UiRing, new Color(1f, 0.84f, 0.36f, 0.9f)); _aimRing.enabled = false;
            _aimDot = Img("AimDot", t, _ctx.Art.UiCircle, new Color(1f, 0.84f, 0.36f, 0.6f)); _aimDot.enabled = false;
            _aimCancel = Img("AimRange", t, _ctx.Art.UiRing, new Color(1f, 1f, 1f, 0.25f)); _aimCancel.enabled = false;
            _editBg = Img("EditBg", t, _ctx.Art.UiWhite, new Color(0.02f, 0.03f, 0.07f, 0.55f)); _editBg.enabled = false;
            _editHint = Txt("EditHint", t, 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); _editHint.horizontalOverflow = HorizontalWrapMode.Wrap; _editHint.enabled = false;
            _editHint.text = "Перетаскивай кнопки пальцем туда, где удобно";
            AddEditBtn("Готово", () => { SaveControls(); _editCtl = false; });
            AddEditBtn("Сброс", () => { _custom.Clear(); _fw = -1f; UpdateScale(); });
            AddEditBtn("Джойстик", () => { _leftHand = !_leftHand; _custom.Clear(); _fw = -1f; UpdateScale(); });
            AddEditBtn("Прозрачность", () => { _btnAlpha = _btnAlpha <= 0.45f ? 1f : _btnAlpha - 0.2f; });
            _fps = Txt("Fps", t, 14, TextAnchor.UpperLeft, new Color(0.43f, 1f, 0.69f), FontStyle.Bold); _fps.enabled = false;
            _errBadge = Txt("ErrBadge", t, 14, TextAnchor.MiddleRight, new Color(1f, 0.42f, 0.5f), FontStyle.Bold); _errBadge.enabled = false;
            LoadControls();
        }

        void AddEditBtn(string label, System.Action act)
        {
            var bg = Img("EditBtn", _safe, _ctx.Art.UiWhite, new Color(0.15f, 0.2f, 0.32f, 0.95f)); bg.enabled = false;
            var tx = Txt("EditBtnLabel", _safe, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); tx.text = label; tx.enabled = false;
            _editBtns.Add((bg, tx, default, act));
        }

        void LoadControls()
        {
            _leftHand = PlayerPrefs.GetInt(Pref("left_hand"), 0) == 1;
            _btnAlpha = Mathf.Clamp(PlayerPrefs.GetFloat(Pref("button_alpha"), 1f), 0.4f, 1f);
            _custom.Clear();
            foreach (var part in PlayerPrefs.GetString(Pref("controls"), "").Split(';'))
            {
                var f = part.Split(':'); if (f.Length != 3) continue;
                if (float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)) _custom[f[0]] = new Vector2(x, y);
            }
        }

        void SaveControls()
        {
            var sb = new StringBuilder();
            foreach (var kv in _custom) sb.Append(kv.Key).Append(':').Append(kv.Value.x.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(kv.Value.y.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            PlayerPrefs.SetString(Pref("controls"), sb.ToString()); PlayerPrefs.SetInt(Pref("left_hand"), _leftHand ? 1 : 0); PlayerPrefs.SetFloat(Pref("button_alpha"), _btnAlpha); PlayerPrefs.Save();
        }

        /// <summary>Default button position, mirrored for the left-handed layout, or where the player dragged it.</summary>
        Vector2 AdjustPos(Btn b, Vector2 pos, float fw, float fh)
        {
            if (_custom.TryGetValue(b.Id, out var n)) return new Vector2(Mathf.Clamp(n.x * fw, b.Rad, fw - b.Rad), Mathf.Clamp(n.y * fh, Top + b.Rad, fh - b.Rad));
            if (_leftHand && !b.AtTop) pos.x = fw - pos.x;
            return pos;
        }

        void PlaceBtn(Btn b, float bk)
        {
            Circle(b.Glow.rectTransform, b.Pos, b.Rad + 8 * bk); Circle(b.Bg.rectTransform, b.Pos, b.Rad); Circle(b.Cd.rectTransform, b.Pos, b.Rad); Circle(b.Rim.rectTransform, b.Pos, b.Rad);
            Place(b.Sub.rectTransform, b.Pos.x - b.Rad, b.Pos.y, b.Rad * 2, b.Rad);
        }

        void LayoutExtras(float fw, float fh)
        {
            Place(_editBg.rectTransform, 0, 0, fw, fh + _bottomInset);
            Place(_editHint.rectTransform, 20, Top + 12, fw - 40, 40);
            float bw = Mathf.Min(150f, (fw - 50f) / 4f), x0 = (fw - bw * 4f - 30f) / 2f;
            for (int i = 0; i < _editBtns.Count; i++)
            {
                var e = _editBtns[i]; var r = new Rect(x0 + i * (bw + 10f), Top + 60f, bw, 44f);
                Place(e.bg.rectTransform, r.x, r.y, r.width, r.height); Place(e.tx.rectTransform, r.x, r.y, r.width, r.height);
                _editBtns[i] = (e.bg, e.tx, r, e.act);
            }
            Place(_fps.rectTransform, 8, Top + 4, 200, 20);
            Place(_errBadge.rectTransform, fw - 300, 128, 240, 20);
        }

        /// <summary>Opens the layout editor (from the pause menu). The game stays paused while it is open.</summary>
        void EditControls() { HidePanel(); _editCtl = true; }

        void EditPointer(int id, Vector2 p, bool began, bool ended)
        {
            if (began)
            {
                foreach (var e in _editBtns) if (e.r.Contains(p)) { e.act(); return; }
                foreach (var b in _btns) if (b.Id != "pause" && Vector2.Distance(b.Pos, p) < b.Rad + 12f) { _dragBtn = b; _dragBtnTouch = id; return; }
                return;
            }
            if (id != _dragBtnTouch || _dragBtn == null) return;
            var bb = _dragBtn;
            bb.Pos = new Vector2(Mathf.Clamp(p.x, bb.Rad, _fw - bb.Rad), Mathf.Clamp(p.y, Top + bb.Rad, _fh - bb.Rad));
            PlaceBtn(bb, bb.Rad / bb.R);
            if (ended) { _custom[bb.Id] = new Vector2(bb.Pos.x / _fw, bb.Pos.y / _fh); _dragBtn = null; _dragBtnTouch = int.MinValue; }
        }

        // ---- drag-to-aim ----
        bool StartAim(int id, Btn b)
        {
            if (_ctx.World.AimKind(b.Id) == null) return false;
            _aimTouch = id; _aimBtn = b; _aimNow = b.Pos; return true;
        }

        void AimPointer(int id, Vector2 p, bool ended)
        {
            if (id != _aimTouch) return;
            _aimNow = p;
            if (!ended) return;
            var w = _ctx.World; var b = _aimBtn; _aimTouch = int.MinValue; _aimBtn = null;
            var kind = w.AimKind(b.Id); if (kind == null) return;
            var drag = (_aimNow - b.Pos) / Mathf.Max(0.5f, b.Rad / b.R); // the same finger distance on any button size
            if (drag.magnitude > 18f) { _intent.HasAim = true; _intent.AimAt = w.AimTarget(kind, drag); }
            if (b.Id == "tac") _intent.Vent = true; else _intent.Ability = true;
        }

        Vector2 WorldToUi(float x, float y) => new Vector2(_wx + x * _ws, _wy + (y - _scroll0) * _ws);

        // ================= per frame =================
        void SyncExtras(BomberWorld w, float dt)
        {
            bool play = _panelKind == null && !_editCtl && w.Phase == BomberPhase.Play;
            // objective arrows
            _arrowN = 0; _arrowLabelN = 0;
            if (play)
            {
                var objs = w.Objectives();
                var pu = WorldToUi(w.P.X, w.P.Y);
                for (int i = 0; i < objs.Count; i++)
                {
                    var (pos, label, col) = objs[i]; var c = (Color)col;
                    var u = WorldToUi(pos.x, pos.y); float viewTop = _wy, viewBot = _wy + _viewH * _ws;
                    bool on = u.y > viewTop + 20f && u.y < viewBot - 20f;
                    float bob = Mathf.Sin(Time.unscaledTime * 6f) * 6f;
                    if (on)
                    {
                        bool door = pos.y < BomberData.Cell * 1.5f; // door at the top: chevron under it, pointing up
                        Arrow(new Vector2(u.x, u.y + (door ? 46f + bob : -40f - bob)), door ? 90f : -90f, c, 30f);
                    }
                    else
                    {
                        float ey = u.y < viewTop ? viewTop + 40f : viewBot - 40f;
                        var ep = new Vector2(Mathf.Clamp(u.x, _wx + 30f, _wx + BomberData.W * _ws - 30f), ey + (u.y < viewTop ? -bob : bob) * 0.5f);
                        Arrow(ep, u.y < viewTop ? 90f : -90f, c, 34f);
                        ArrowLabel(ep + new Vector2(0f, u.y < viewTop ? 30f : -30f), label, c);
                    }
                    // compass near the drone toward the first objective
                    if (i == 0 && Vector2.Distance(u, pu) > 150f * _ws)
                    {
                        var d = (u - pu).normalized; float ang = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
                        var cc = c; cc.a = 0.85f; Arrow(pu + d * (44f * Mathf.Max(1f, _ws)), ang, cc, 22f);
                    }
                }
                // last enemies off screen
                if (w.HuntMode)
                    foreach (var e in w.Enemies)
                    {
                        var u = WorldToUi(e.X, e.Y); float viewTop = _wy, viewBot = _wy + _viewH * _ws;
                        if (u.y > viewTop && u.y < viewBot) continue;
                        var ep = new Vector2(Mathf.Clamp(u.x, _wx + 24f, _wx + BomberData.W * _ws - 24f), u.y < viewTop ? viewTop + 28f : viewBot - 28f);
                        Arrow(ep, u.y < viewTop ? 90f : -90f, new Color(1f, 0.35f, 0.43f, 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 8f)), 24f);
                    }
            }
            for (int i = _arrowN; i < _arrows.Count; i++) _arrows[i].enabled = false;
            for (int i = _arrowLabelN; i < _arrowLabels.Count; i++) _arrowLabels[i].enabled = false;

            // aim preview while a button is dragged
            bool aiming = _aimTouch != int.MinValue && _aimBtn != null && play;
            _aimRing.enabled = _aimDot.enabled = _aimCancel.enabled = aiming;
            if (aiming)
            {
                var kind = w.AimKind(_aimBtn.Id);
                if (kind == null) { _aimTouch = int.MinValue; }
                else
                {
                    var drag = (_aimNow - _aimBtn.Pos) / Mathf.Max(0.5f, _aimBtn.Rad / _aimBtn.R);
                    var tg = drag.magnitude > 18f ? w.AimTarget(kind, drag) : new Vector2(w.P.X, w.P.Y);
                    float r = kind == "blink" ? 18f : kind == "grav" ? 90f : 95f;
                    Circle(_aimRing.rectTransform, WorldToUi(tg.x, tg.y), r * _ws); Circle(_aimDot.rectTransform, WorldToUi(tg.x, tg.y), 6f);
                    Circle(_aimCancel.rectTransform, WorldToUi(w.P.X, w.P.Y), BomberWorld.AimRange(kind) * _ws);
                    _aimRing.color = drag.magnitude > 18f ? new Color(1f, 0.84f, 0.36f, 0.9f) : new Color(1f, 1f, 1f, 0.4f);
                }
            }

            // control editor overlay
            _editBg.enabled = _editHint.enabled = _editCtl;
            for (int i = 0; i < _editBtns.Count; i++)
            {
                var e = _editBtns[i]; e.bg.enabled = e.tx.enabled = _editCtl;
                if (i == 2) e.tx.text = _leftHand ? "Джойстик: слева" : "Джойстик: справа";
                if (i == 3) e.tx.text = $"Видимость {Mathf.RoundToInt(_btnAlpha * 100)}%";
            }

            // FPS and error badge
            _fpsAcc += Time.unscaledDeltaTime; _fpsN++; _fpsT -= Time.unscaledDeltaTime;
            if (_fpsT <= 0f) { _fps.text = $"{Mathf.RoundToInt(_fpsN / Mathf.Max(0.001f, _fpsAcc))} FPS"; _fpsT = 0.5f; _fpsAcc = 0f; _fpsN = 0; }
            _fps.enabled = _showFps;
            int unseen = _ctx.Log != null ? _ctx.Log.Unseen : 0;
            _errBadge.enabled = unseen > 0 && _panelKind == null; _errBadge.text = $"⚠ ошибок: {unseen} (пауза → журнал)";
            if (_errToastT > 0f) _errToastT -= dt;
        }

        void Arrow(Vector2 at, float deg, Color c, float size)
        {
            while (_arrows.Count <= _arrowN) _arrows.Add(Img("Arrow", _safe, _ctx.Art.UiArrow, Color.white));
            var a = _arrows[_arrowN++]; a.enabled = true; a.color = c; a.transform.SetSiblingIndex(_fade.transform.GetSiblingIndex() + 1);
            var rt = a.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(at.x, -at.y); rt.sizeDelta = new Vector2(size, size); rt.localEulerAngles = new Vector3(0f, 0f, deg);
        }

        void ArrowLabel(Vector2 at, string s, Color c)
        {
            var tx = GetText(_arrowLabels, _arrowLabelN++); tx.text = s; tx.fontSize = 14; tx.color = c; tx.alignment = TextAnchor.MiddleCenter;
            Place(tx.rectTransform, at.x - 90f, at.y - 11f, 180f, 22f);
        }

        /// <summary>Called by the bug journal for every new error: a short notice, not more often than every 5 s.</summary>
        public void OnBugLogged()
        {
            if (_errToastT > 0f) return;
            _errToastT = 5f; Toast("Ошибка записана в журнал (пауза → Журнал ошибок)", 2.2f);
        }

        // ================= panels =================
        void ShowJournal()
        {
            var log = _ctx.Log; log.MarkSeen();
            var rows = new List<(string, System.Action)>();
            rows.Add(("Скопировать отчёт", () => { GUIUtility.systemCopyBuffer = log.Report(); Toast("Отчёт скопирован — вставь его в сообщение", 2f); }));
            rows.Add(("Очистить журнал", () => { log.Clear(); ShowJournal(); }));
            for (int i = log.Entries.Count - 1; i >= 0 && rows.Count < 32; i--)
            {
                var e = log.Entries[i]; string col = e.Type == LogType.Warning ? "#ffb347" : "#ff8a9a";
                string msg = BomberDebugLog.Short(e); if (msg.Length > 160) msg = msg.Substring(0, 160) + "…";
                rows.Add(($"<size=14><color={col}>{Escape(msg)}</color>\n<color=#8090b4>{Escape(e.State ?? "")}</color></size>", () => { }));
            }
            rows.Add(("<b>Закрыть</b>", Pause));
            ShowPanelEx("journal", "Журнал ошибок",
                log.Entries.Count == 0 ? "Ошибок нет. Если что-то сломается, запись появится здесь." : $"Записей: {log.Entries.Count}. Файл: {log.FilePath}",
                66f, 120f, true, rows.ToArray());
        }

        static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");

        void ShowDev()
        {
            var w = _ctx.World; var rows = new List<(string, System.Action)>();
            if (w.Mode == BomberMode.Run) rows.Add(("Сразу к боссу", () => { HidePanel(); w.DevToBoss(); }));
            if (w.Mode != BomberMode.Hub) rows.Add(("Зачистить комнату", () => { HidePanel(); w.DevClearRoom(); }));
            rows.Add(($"Неуязвимость: {(w.DevGod ? "вкл" : "выкл")}", () => { w.DevGod = !w.DevGod; ShowDev(); }));
            rows.Add(("+500 Данных", () => { w.DevAddData(500); ShowDev(); }));
            if (w.Mode == BomberMode.Run) rows.Add(("+200 скрапа", () => { w.DevAddScrap(200); ShowDev(); }));
            rows.Add(("Открыть всё", () => { w.DevUnlockAll(); Toast("Открыты все дроны, оружие, тактики и достижения", 2f); ShowDev(); }));
            rows.Add(($"Счётчик FPS: {(_showFps ? "вкл" : "выкл")}", () => { _showFps = !_showFps; ShowDev(); }));
            rows.Add(("Тестовая ошибка (проверить журнал)", () => { Debug.LogError($"[{BomberGame.Id}] test error from the developer menu"); ShowDev(); }));
            rows.Add(("Сбросить весь прогресс", () => { HidePanel(); w.DevResetProgress(); }));
            rows.Add(("<b>Назад</b>", Pause));
            ShowPanelEx("dev", "Разработка", "Видно только в редакторе и в development-сборке.", 52f, 110f, false, rows.ToArray());
        }

        void ShowTactics()
        {
            var w = _ctx.World; var m = w.Meta; var rows = new List<(string, System.Action)>();
            foreach (var t in BomberMetaData.Tactics)
            {
                bool un = m.TacticUnlocked(t.Id), sel = m.Tactic == t.Id; string id = t.Id;
                string state = sel ? "<color=#6dffb0>ВЫБРАНА</color>" : un ? "<color=#7fe3ff>выбрать</color>" : $"<color={(m.Data >= t.Cost ? "#ffd65c" : "#ff8a9a")}>купить · {t.Cost} Данных</color>";
                rows.Add(($"<size=18><b>{t.Name}</b></size>  {state}  <size=12><color=#8090b4>перезарядка {t.Cd:0} с</color></size>\n<size=13><color=#a9b6d6>{t.Desc}</color></size>",
                    () => { if (!(un ? w.SelectTactic(id) : w.BuyTactic(id))) Toast("Не хватает Данных", 1.2f); ShowTactics(); }));
            }
            rows.Add(("<b>Закрыть</b>", HidePanel));
            ShowPanelEx("tactics", "Тактика", $"Левая кнопка, общая для всех дронов. Блинк и Гравиграната целятся: потяни кнопку и отпусти. <color=#b48cff>{m.Data} Данных</color>", 92f, 110f, true, rows.ToArray());
        }
    }
}
