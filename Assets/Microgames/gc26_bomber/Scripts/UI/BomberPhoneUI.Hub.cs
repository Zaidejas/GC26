using System.Collections.Generic;
using GC26.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Bomber
{
    // Hub terminals, survivors, pause / end screens and the tutorial card (prototype renderPanel, endRun screen).
    public sealed partial class BomberPhoneUI
    {
        Image _tutBg, _tutLine, _skipBg; Text _tutText, _tutStep, _skipText;
        Rect _skipRect;

        void BuildTutorialCard(Transform t)
        {
            _tutBg = Img("TutBg", t, _ctx.Art.UiWhite, new Color(0.03f, 0.05f, 0.1f, 0.9f));
            _tutLine = Img("TutLine", t, _ctx.Art.UiWhite, new Color(0.71f, 0.55f, 1f));
            _tutStep = Txt("TutStep", t, 13, TextAnchor.UpperLeft, new Color(0.71f, 0.55f, 1f), FontStyle.Bold);
            _tutText = Txt("TutText", t, 17, TextAnchor.UpperLeft, Color.white, FontStyle.Normal); _tutText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skipBg = Img("SkipBg", t, _ctx.Art.UiWhite, new Color(0.15f, 0.2f, 0.32f, 0.95f));
            _skipText = Txt("SkipText", t, 15, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold); _skipText.text = "ПРОПУСТИТЬ »";
        }

        void LayoutTutorialCard(float ww)
        {
            float x = _wx + 8f, y = _wy + 8f, w = ww - 16f;
            Place(_tutBg.rectTransform, x, y, w, 104f); Place(_tutLine.rectTransform, x, y, 4f, 104f);
            Place(_tutStep.rectTransform, x + 14f, y + 8f, w - 180f, 18f);
            Place(_tutText.rectTransform, x + 14f, y + 28f, w - 28f, 74f);
            _skipRect = new Rect(x + w - 156f, y + 6f, 150f, 30f);
            Place(_skipBg.rectTransform, _skipRect.x, _skipRect.y, _skipRect.width, _skipRect.height); Place(_skipText.rectTransform, _skipRect.x, _skipRect.y, _skipRect.width, _skipRect.height);
        }

        void SyncTutorialCard(BomberWorld w)
        {
            bool on = w.Mode == BomberMode.Tutorial && _panelKind == null;
            foreach (var g in new Graphic[] { _tutBg, _tutLine, _tutStep, _tutText, _skipBg, _skipText }) g.enabled = on;
            if (!on) return;
            _tutStep.text = $"ОБУЧЕНИЕ · ШАГ {Mathf.Min(w.TutStep + 1, BomberWorld.TutSteps)}/{BomberWorld.TutSteps}";
            _tutText.text = w.TutText;
        }

        bool TrySkip(Vector2 p)
        {
            if (_ctx.World.Mode != BomberMode.Tutorial || !_skipRect.Contains(p)) return false;
            _ctx.World.SkipTutorial(); return true;
        }

        // ================= pause =================
        void Pause()
        {
            var w = _ctx.World; var rows = new List<(string, System.Action)> { ("Продолжить", HidePanel) };
            if (w.Mode == BomberMode.Tutorial) rows.Add(("Пропустить обучение", () => { HidePanel(); w.SkipTutorial(); }));
            rows.Add(($"Кнопки крупнее ({Mathf.RoundToInt(_btnScale * 100)}%)", () => SetButtonScale(_btnScale + 0.1f)));
            rows.Add(("Кнопки мельче", () => SetButtonScale(_btnScale - 0.1f)));
            rows.Add(($"Тёмная станция: {(_ctx.View.Dark ? "вкл" : "выкл")}", () => { _ctx.View.SetDark(!_ctx.View.Dark); Pause(); }));
            if (w.Mode == BomberMode.Hub) rows.Add(("Пройти обучение заново", () => { HidePanel(); w.EnterTutorial(); }));
            if (w.Mode == BomberMode.Run) rows.Add(("Сдаться (в хаб, ½ Данных)", () => { HidePanel(); w.Abandon(); }));
            rows.Add(("В меню", () => _ctx.ExitToMenu()));
            ShowPanelEx("pause", "Пауза", "Стрельба автоматическая. Джойстик — справа, способности — слева.", 52f, 130f, false, rows.ToArray());
        }

        // ================= end of run =================
        void ShowEnd(BomberWorld w)
        {
            bool win = w.Phase == BomberPhase.Victory;
            string title = win ? "Сигнал отправлен" : w.LastAbandoned ? "Забег прерван" : "Дрон уничтожен";
            string body = (win ? "ОРАКУЛ повержен. Станция свободна." : $"Сектор {w.Sector + 1}: {BomberData.Sectors[w.Sector].Name}, комната {w.Step + 1}.") + (w.LastBest ? " Новый рекорд!" : "") +
                $"\n<color=#b48cff><b>+{w.LastDataGain} Данных</b></color> (всего {w.Meta.Data})\n" +
                $"Счёт {w.Score} · Комнат {w.Rooms} · Убийств {w.Kills} · Боссов {w.Bosses} · Комбо ×{w.MaxCombo}";
            foreach (var id in w.RescuedNow) { var n = BomberMetaData.Npc(id); body += $"\n<color={Hex(n.Color)}>Спасён: {n.Name}</color> — {n.Bonus}"; }
            foreach (var id in w.NewAch) foreach (var a in BomberMetaData.Achs) if (a.Id == id) body += $"\n<color=#ffd65c>Достижение: {a.Name}</color> → {a.Reward}";
            _panelText.supportRichText = true;
            ShowPanelEx("end", title, body, 56f, 250f, false,
                ("В хаб", () => { HidePanel(); w.EnterHub(); }), ("Новый забег", () => { HidePanel(); _ctx.Restart(); }), ("В меню", () => _ctx.ExitToMenu()));
        }

        // ================= hub terminals =================
        static string Pips(int l, int max) => new string('●', l) + new string('○', Mathf.Max(0, max - l));
        static string Bar(float v, float lo, float hi) { int n = Mathf.Clamp(Mathf.RoundToInt((v - lo) / (hi - lo) * 5f) + 1, 1, 6); return "<color=#7fe3ff>" + new string('■', n) + "</color><color=#2b3348>" + new string('■', 6 - n) + "</color>"; }
        string DataLine => $"<color=#b48cff>{_ctx.World.Meta.Data} Данных</color>";

        void ShowStation(string id)
        {
            if (id.StartsWith("npc:")) { ShowNpc(id.Substring(4)); return; }
            switch (id)
            {
                case "chars": ShowChars(); break;
                case "armory": ShowArmory(); break;
                case "workshop": ShowWorkshop(); break;
                case "records": ShowRecords(); break;
                case "training":
                    ShowPanel("training", "Тренировка", "Пройти обучение ещё раз? Прогресс не теряется.", ("Начать обучение", () => { HidePanel(); _ctx.World.EnterTutorial(); }), ("Закрыть", HidePanel));
                    break;
            }
        }

        void ShowChars()
        {
            var w = _ctx.World; var m = w.Meta; var rows = new List<(string, System.Action)>();
            foreach (var c in BomberMetaData.Chars)
            {
                bool un = m.CharUnlocked(c.Id), sel = m.Char == c.Id; string cid = c.Id;
                int hp = c.Hp + m.CharUp(c.Id, "hp"); float spd = c.Speed * (1f + 0.05f * m.CharUp(c.Id, "spd")), dm = c.Dmg * (1f + 0.06f * m.CharUp(c.Id, "dmg"));
                string state = sel ? "<color=#6dffb0>ВЫБРАН</color>" : un ? "<color=#7fe3ff>выбрать</color>" : c.Cost > 0 ? $"<color={(m.Data >= c.Cost ? "#ffd65c" : "#ff8a9a")}>купить · {c.Cost} Данных</color>"
                    : c.Rescue ? "<color=#ff9adf>спаси в забеге (комната СПАСЕНИЕ)</color>" : $"<color=#8090b4>достижение «{AchName(c.Ach)}»</color>";
                rows.Add(($"<size=19><b><color={Hex(c.Color)}>{c.Name}</color></b></size>  {state}  <size=12><color=#8090b4>ур. {m.CharUpSum(c.Id)}/12</color></size>\n" +
                          $"<size=13>ХП {Bar(hp, 3, 11)} {hp}   Скор. {Bar(spd, 160, 230)} {Mathf.RoundToInt(spd)}   Урон {Bar(dm, 0.85f, 1.35f)} ×{dm:0.00}</size>\n" +
                          $"<size=13><color=#ffd65c>{c.Ability}:</color> <color=#a9b6d6>{c.AbilityDesc}</color>\n<color=#a9b6d6>{c.Trait}</color></size>",
                    () => { if (!(un ? w.SelectChar(cid) : w.BuyChar(cid))) Toast(un ? "" : "Пока закрыт", 1.2f); ShowChars(); }));
            }
            var cur = BomberMetaData.Char(m.Char);
            rows.Add(($"<size=18><b>Улучшения: <color={Hex(cur.Color)}>{cur.Name}</color></b></size>", null));
            foreach (var u in BomberMetaData.CharUps)
            {
                int l = m.CharUp(cur.Id, u.Id), price = w.CharUpPrice(cur.Id, u.Id); string k = u.Id;
                rows.Add(($"<b>{u.Name}</b> <color=#ffb347>{Pips(l, u.Cost.Length)}</color>   {(price < 0 ? "<color=#6dffb0>максимум</color>" : $"<color={(m.Data >= price ? "#ffd65c" : "#ff8a9a")}>{price} Данных</color>")}\n<size=13><color=#a9b6d6>{u.Desc}</color></size>",
                    () => { if (!w.BuyCharUp(cur.Id, k)) Toast("Не хватает Данных", 1.2f); ShowChars(); }));
            }
            rows.Add(("<b>Закрыть</b>", HidePanel));
            ShowPanelEx("chars", "Капсула персонажей", $"Выбери дрона и прокачай его. {DataLine}", 124f, 100f, true, rows.ToArray());
            ResizeRows(58f);
        }

        void ShowArmory()
        {
            var w = _ctx.World; var m = w.Meta; var rows = new List<(string, System.Action)>();
            foreach (var id in BomberItems.BaseWeapons)
            {
                var d = BomberItems.Weapons[id]; bool un = m.WeaponUnlocked(id), start = m.StartWeapon == id; string wid = id;
                BomberMetaData.WeaponUnlock.TryGetValue(id, out var lk);
                string state = start ? "<color=#6dffb0>СТАРТОВОЕ</color>" : un ? "<color=#7fe3ff>брать на старт</color>" : lk.ach != null ? $"<color=#8090b4>достижение «{AchName(lk.ach)}»</color>"
                    : $"<color={(m.Data >= lk.cost ? "#ffd65c" : "#ff8a9a")}>открыть · {lk.cost} Данных</color>";
                rows.Add(($"<size=18><b>{d.Name}</b></size>  {state}\n<size=13><color=#a9b6d6>{d.Desc}</color></size>",
                    () => { if (!(un ? w.SetStartWeapon(wid) : w.BuyWeapon(wid))) Toast(lk.ach != null ? "Открывается достижением" : "Не хватает Данных", 1.4f); ShowArmory(); }));
            }
            rows.Add(("<b>Закрыть</b>", HidePanel));
            ShowPanelEx("armory", "Арсенал", $"Открытое оружие попадает в награды и магазины забегов. {DataLine}", 70f, 100f, true, rows.ToArray());
        }

        void ShowWorkshop()
        {
            var w = _ctx.World; var m = w.Meta; var rows = new List<(string, System.Action)>();
            foreach (var u in BomberMetaData.MetaUps)
            {
                int l = m.Up(u.Id), price = w.MetaUpPrice(u.Id); string k = u.Id;
                rows.Add(($"<b>{u.Name}</b> <color=#ffb347>{Pips(l, u.Cost.Length)}</color>   {(price < 0 ? "<color=#6dffb0>максимум</color>" : $"<color={(m.Data >= price ? "#ffd65c" : "#ff8a9a")}>{price} Данных</color>")}\n<size=13><color=#a9b6d6>{u.Desc}</color></size>",
                    () => { if (!w.BuyMetaUp(k)) Toast("Не хватает Данных", 1.2f); ShowWorkshop(); }));
            }
            rows.Add(("<size=18><b>Выжившие</b></size>", null));
            foreach (var n in BomberMetaData.Npcs)
            {
                if (n.Home) continue; bool ok = m.IsRescued(n.Id);
                rows.Add(($"<color={Hex(n.Color)}><b>{(ok ? "✓ " : "")}{n.Name}</b></color>  {(ok ? "<color=#6dffb0>в хабе</color>" : "<color=#8090b4>ждёт спасения</color>")}\n<size=13><color=#a9b6d6>{n.Bonus}</color></size>", () => { }));
            }
            rows.Add(("<b>Закрыть</b>", HidePanel));
            ShowPanelEx("workshop", "Мастерская", $"Постоянные улучшения для всех дронов. {DataLine}", 62f, 100f, true, rows.ToArray());
        }

        void ShowRecords()
        {
            var m = _ctx.World.Meta; var rows = new List<(string, System.Action)>(); int got = 0;
            foreach (var a in BomberMetaData.Achs)
            {
                bool ok = m.HasAch(a.Id); if (ok) got++;
                rows.Add(($"<b>{(ok ? "<color=#ffd65c>✓ " : "<color=#dbe6ff>")}{a.Name}</color></b>  <size=13><color=#a9b6d6>{a.Desc}</color> → <color=#6dffb0>{a.Reward}</color></size>", () => { }));
            }
            rows.Add(("<b>Закрыть</b>", HidePanel));
            ShowPanelEx("records", "Достижения",
                $"Открыто {got} из {BomberMetaData.Achs.Length}. Рекорд {m.Best} · Забегов {m.Runs} · Побед {m.Wins} · Убийств {m.Kills} · Боссов {m.Bosses} · Лучший сектор {m.BestSector}",
                48f, 110f, true, rows.ToArray());
        }

        void ShowNpc(string id)
        {
            var n = BomberMetaData.Npc(id); if (n == null) return;
            ShowPanel("npc", n.Name, $"«{n.Line}»" + (n.Home ? "" : $"\n\nБонус: {n.Bonus}."), ("Закрыть", HidePanel));
            _panelTitle.color = (Color)n.Color;
        }

        static string AchName(string id) { foreach (var a in BomberMetaData.Achs) if (a.Id == id) return a.Name; return id; }

        /// <summary>Header rows (act == null) of a rich panel are made shorter.</summary>
        void ResizeRows(float headerH)
        {
            // rows are laid out with a fixed step; header rows just leave empty space below — fine for now
        }
    }
}
