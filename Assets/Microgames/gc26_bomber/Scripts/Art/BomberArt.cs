using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Pixel art generated in code — the same sprites as the HTML prototype (drone, enemies, props),
    /// so the game runs without imported assets. 16 art px = 1 world unit = 1 room cell.
    /// Owned by BomberContext; all textures are destroyed with it (no static caches).
    /// </summary>
    public sealed class BomberArt : IDisposable
    {
        public const int PixelsPerUnit = 16;
        public static Color32 Hex(string hex) => BomberData.C(hex);

        static readonly Color32 MK = Hex("#070912"), MN = Hex("#2b3348"), MM = Hex("#4d5a78"), ML = Hex("#8a9bc0"), MW = Hex("#e8f0ff"),
                                MR = Hex("#ff3048"), MY = Hex("#ffd65c"), MO = Hex("#ff8a3c"), MC = Hex("#9ff4ff"), Outline = Hex("#05070e");

        public Sprite Drone, Rotor, Gun, Lens, Shot, ShotHeavy, EnemyShot, Pixel, Ring, Disc, Scrap, Repair, EmpPickup, Barrel, Wall, TurretHead;
        public Sprite UiCircle, UiRing, UiWhite, UiGradient, UiVignette, Glow, UiArrow;
        public Sprite Terminal, Cell, Dynamite, ForgeTurret, Npc, QuestItem;
        public Sprite Chest, ChestOpen, Altar, AltarUsed, Bench, Stand, Rocket, Saw, Flame, CompGunner, CompGuard, CompMedic, CompStriker;
        readonly Dictionary<string, Sprite> _enemy = new Dictionary<string, Sprite>();
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        public BomberArt()
        {
            Drone = MakeDrone(Hex("#7fe3ff"));
            Rotor = Build(9, 1, c => { for (int x = 0; x < 9; x++) if (x != 4) c.Px(x, 0, Hex("#e6eeff")); }, outline: false);
            Gun = Build(10, 4, c => { c.Fill((x, y) => x > 0 && x < 8 && y > 0.5f && y < 3.5f, MN); c.Px(7, 1, Hex("#7fe3ff")); c.Px(7, 2, Hex("#7fe3ff")); }, pivot: new Vector2(0.1f, 0.5f));
            Lens = Build(2, 2, c => { c.Px(0, 0, MW); c.Px(1, 0, MC); c.Px(0, 1, MC); c.Px(1, 1, MC); }, outline: false);
            Shot = Build(4, 3, c => { c.Fill((x, y) => true, Hex("#bff4ff"), false); c.Px(1, 1, MW); c.Px(2, 1, MW); }, outline: false);
            ShotHeavy = Build(7, 7, c => { c.Fill(c.Disc(3.5f, 3.5f, 3.5f), Hex("#7fe3ff"), false); c.Fill(c.Disc(3.5f, 3.5f, 1.8f), MW, false); }, outline: false);
            EnemyShot = Build(5, 5, c => { c.Fill(c.Disc(2.5f, 2.5f, 2.6f), Hex("#ff4f6a"), false); c.Fill(c.Disc(2.5f, 2.5f, 1.1f), Hex("#ffe0e4"), false); }, outline: false);
            Pixel = Build(1, 1, c => c.Px(0, 0, new Color32(255, 255, 255, 255)), outline: false);
            Ring = Smooth(64, ring: true, PixelsPerUnit * 2); Disc = Smooth(64, ring: false, PixelsPerUnit * 2);
            Scrap = Build(4, 4, c => { c.Fill((x, y) => true, MY); c.Px(1, 1, Hex("#7a5a10")); c.Px(2, 2, Hex("#7a5a10")); }, outline: false);
            Repair = Build(7, 7, c => { c.Fill((x, y) => (x > 2 && x < 4) || (y > 2 && y < 4), Hex("#6dffb0"), false); });
            EmpPickup = Build(8, 8, c => { c.Fill((x, y) => { float d = (x - 4) * (x - 4) + (y - 4) * (y - 4); return d < 14 && d > 6; }, Hex("#9fd0ff"), false); c.Px(3, 3, MW); c.Px(4, 4, MW); });
            Barrel = Build(12, 12, c => { c.Fill(c.Disc(6, 6, 5.5f), Hex("#5a2a20")); c.Fill((x, y) => y > 5 && y < 7 && x > 2 && x < 10, Hex("#ffb347"), false); });
            Wall = Build(16, 16, c =>
            {
                c.Fill((x, y) => true, Hex("#1b2742"), false); c.Fill((x, y) => y < 2, Hex("#2c3d63"), false);
                c.Fill((x, y) => y > 15 || x > 15, Hex("#121a2e"), false); c.Fill((x, y) => x < 1 && y > 2 && y < 15, Hex("#24335a"), false);
                foreach (var (x, y) in new[] { (3, 4), (12, 4), (3, 12), (12, 12) }) c.Px(x, y, Hex("#3a4c78"));
                c.Fill((x, y) => y > 8 && y < 9 && x > 5 && x < 11, Hex("#16203a"), false);
            }, outline: false, mesh: SpriteMeshType.FullRect);
            TurretHead = Build(16, 10, c => { c.Fill(c.Box(8, 3.8f, 15.5f, 6.2f), MN); c.Fill(c.Disc(7, 5, 3.6f), Hex("#ffcf5c")); c.Px(6, 4, MR); });
            Glow = Radial(64, PixelsPerUnit * 2, (d) => Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
            UiVignette = Radial(128, 100, (d) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.9f)), square: true);
            Chest = Build(18, 14, c => { c.Fill(c.Box(0, 2, 18, 14), Hex("#1e3a4a")); c.Fill(c.Box(0, 0, 18, 5), Hex("#2c5468")); c.Rect(8, 4, 2, 4, MY); c.Rect(1, 6, 16, 1, Hex("#7fe3ff")); });
            ChestOpen = Build(18, 14, c => { c.Fill(c.Box(0, 4, 18, 14), Hex("#1b2742")); c.Fill(c.Box(0, 0, 18, 3), Hex("#24335a")); c.Rect(2, 6, 14, 6, MK); });
            Altar = Build(20, 20, c => { c.Fill(c.Poly(new[] { (10f, 0f), (20f, 10f), (10f, 20f), (0f, 10f) }), Hex("#2a1030")); c.Fill(c.Disc(10, 10, 3.2f), Hex("#ff5ad1")); c.Px(10, 9, MW); });
            AltarUsed = Build(20, 20, c => { c.Fill(c.Poly(new[] { (10f, 0f), (20f, 10f), (10f, 20f), (0f, 10f) }), Hex("#1b1424")); c.Fill(c.Disc(10, 10, 3f), Hex("#4a3a5a")); });
            Bench = Build(30, 14, c => { c.Fill(c.Box(0, 0, 30, 14), Hex("#2a1e10")); c.Rect(6, 5, 18, 4, Hex("#ffb347")); c.Rect(2, 2, 3, 3, MM); c.Rect(25, 2, 3, 3, MM); c.Rect(12, 1, 2, 2, ML); });
            Stand = Build(34, 28, c => { c.Fill(c.Box(0, 0, 34, 28), Hex("#121c30")); c.Rect(2, 2, 30, 1, Hex("#6dffb0")); c.Rect(4, 22, 26, 3, Hex("#0b1424")); });
            Rocket = Build(8, 4, c => { c.Fill(c.Box(2, 0.5f, 8, 3.5f), Hex("#ffd6a0"), false); c.Rect(0, 1, 2, 2, MO); c.Px(7, 1, MW); }, outline: false);
            Saw = Build(9, 9, c => { c.Fill((x, y) => { float dx = x - 4.5f, dy = y - 4.5f, d = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx); return d < 3.2f + 1.3f * (Mathf.Sin(a * 8f) > 0 ? 1 : 0) && d > 1.2f; }, Hex("#d8f8ff")); });
            Flame = Build(6, 6, c => { c.Fill(c.Disc(3, 3, 3f), Hex("#ff9a3c"), false); c.Fill(c.Disc(3, 3, 1.5f), Hex("#ffe08a"), false); }, outline: false);
            CompGunner = Build(9, 9, c => { c.Fill(c.Box(1, 1, 8, 8), Hex("#bff4ff")); c.Rect(3, 3, 3, 3, Hex("#2b5c6c")); });
            CompGuard = Build(9, 9, c => { c.Fill(c.Disc(4.5f, 4.5f, 4f), Hex("#9fd0ff")); c.Px(4, 4, MW); });
            CompMedic = Build(9, 9, c => { c.Fill((x, y) => (x > 3 && x < 6) || (y > 3 && y < 6), Hex("#6dffb0")); });
            CompStriker = Build(10, 8, c => { c.Fill(c.Poly(new[] { (10f, 4f), (0f, 0f), (2f, 4f), (0f, 8f) }), Hex("#ffb347")); });
            QuestItem = Build(12, 12, c => { c.Fill(c.Poly(new[] { (6f, 0f), (12f, 6f), (6f, 12f), (0f, 6f) }), Hex("#1c1030")); c.Fill(c.Poly(new[] { (6f, 2f), (10f, 6f), (6f, 10f), (2f, 6f) }), Hex("#b48cff")); c.Rect(5, 5, 2, 2, MW); });
            Terminal = Build(22, 22, c => { c.Fill(c.Box(0, 4, 22, 22), MN); c.Fill(c.Box(2, 0, 20, 14), Hex("#101a2c")); c.Fill(c.Box(4, 2, 18, 12), Hex("#e8f0ff"), false); c.Rect(4, 16, 14, 2, MM); c.Rect(6, 19, 3, 2, ML); c.Rect(13, 19, 3, 2, ML); });
            Cell = Build(26, 26, c => { c.Fill(c.Box(0, 0, 26, 26), Hex("#1a1024")); for (int x = 2; x < 26; x += 4) c.Rect(x, 1, 2, 24, MM); c.Rect(0, 0, 26, 2, ML); c.Rect(0, 24, 26, 2, ML); });
            Dynamite = Build(5, 8, c => { c.Fill(c.Box(0, 2, 5, 8), Hex("#ff4d4d")); c.Rect(2, 0, 1, 2, MY); });
            ForgeTurret = Build(18, 18, c => { c.Fill(c.Box(0, 0, 18, 18), Hex("#2a1e10")); c.Fill(c.Box(2, 2, 16, 16), Hex("#ff9f43")); c.Fill(c.Disc(9, 9, 4f), Hex("#3a2a14")); c.Rect(8, 8, 2, 2, Hex("#ffcf8a")); });
            Npc = Build(10, 12, c => { c.Fill(c.Disc(5, 3.5f, 3.2f), Hex("#f0f4ff")); c.Fill(c.Box(1.5f, 6, 8.5f, 12), Hex("#f0f4ff")); c.Rect(3, 3, 4, 1, MK); });
            UiCircle = Smooth(128, ring: false, 100, ui: true); UiRing = Smooth(128, ring: true, 100, ui: true);
            UiArrow = Build(16, 16, c => { c.Fill(c.Poly(new[] { (15f, 8f), (2f, 1f), (6f, 8f), (2f, 15f) }), new Color32(255, 255, 255, 255), false); }, outline: false); UiArrow.texture.filterMode = FilterMode.Bilinear;
            UiWhite = Build(1, 1, c => c.Px(0, 0, new Color32(255, 255, 255, 255)), outline: false);
            UiGradient = Build(1, 64, c => { for (int y = 0; y < 64; y++) c.Px(0, y, new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, y / 63f) * 255))); }, outline: false); UiGradient.texture.filterMode = FilterMode.Bilinear;
        }

        /// <summary>Destroys a generated sprite and its texture (used for per-room floors).</summary>
        public void Free(Sprite s)
        {
            if (!s) return;
            var t = s.texture; _owned.Remove(s); _owned.Remove(t);
            UnityEngine.Object.Destroy(s); if (t) UnityEngine.Object.Destroy(t);
        }

        public void Dispose()
        {
            foreach (var o in _owned) if (o) UnityEngine.Object.Destroy(o);
            _owned.Clear(); _enemy.Clear();
        }

        /// <summary>Drone sprite in the colours of a playable character.</summary>
        public Sprite DroneOf(BomberCharDef c)
        {
            var key = "drone_" + c.Id;
            if (_enemy.TryGetValue(key, out var s)) return s;
            s = c.Id == "spark" ? Drone : MakeDrone(c.Color);
            _enemy[key] = s; return s;
        }

        // ---------- drone ----------
        Sprite MakeDrone(Color32 body) => Build(20, 20, c =>
        {
            const float cx = 10, cy = 10, hb = 5.5f;
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) c.Line(cx - 0.5f, cy - 0.5f, cx - 0.5f + sx * hb, cy - 0.5f + sy * hb, MN, 2);
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) { c.Fill(c.Disc(cx + sx * hb, cy + sy * hb, 2.2f), MM); c.Px((int)(cx + sx * hb - 0.5f), (int)(cy + sy * hb - 0.5f), MK); }
            c.Fill(c.Disc(cx, cy, 4.6f), body);
            foreach (var (x, y) in new[] { (0, -2), (1, -1), (0, 0), (1, 1) }) c.Px((int)cx + x, (int)cy + y, MW);
            c.Fill(c.Disc(cx, cy, 1.9f), MK, false);
        });

        // ---------- enemies (ENEMY_ART from the prototype; drawn facing right) ----------
        public Sprite Enemy(string kind, float radius, bool frame)
        {
            var key = kind + "_" + radius + "_" + (frame ? 1 : 0);
            if (_enemy.TryGetValue(key, out var s)) return s;
            int size = Mathf.CeilToInt(radius * 2f / 2f) + (kind == "sniper" ? 10 : kind == "brute" ? 6 : 5);
            var col = BomberData.Enemies[kind].Color;
            s = Build(size, size, c => DrawEnemy(c, kind, col, frame ? 1 : 0));
            _enemy[key] = s;
            return s;
        }

        static void DrawEnemy(Canvas c, string kind, Color32 b, int f)
        {
            float cx = c.W / 2f, cy = c.H / 2f;
            switch (kind)
            {
                case "beetle":
                    foreach (var s in new[] { -1, 1 }) foreach (var k in new[] { -3, 0, 3 }) c.Line(cx + k, cy + s * 2.5f, cx + k + (f == 1 ? 1 : -1) * (k == 0 ? 1 : 0.5f), cy + s * 5.6f, MN);
                    c.Fill(c.Ell(cx - 0.5f, cy, 4.6f, 3.6f), b); c.Line(cx - 4, cy - 0.5f, cx + 2, cy - 0.5f, Tone(b, -0.5f));
                    c.Fill(c.Disc(cx + 4, cy, 2.2f), MM); c.Px((int)(cx + 4), (int)(cy - 1.5f), MR); c.Px((int)(cx + 4), (int)(cy + 0.5f), MR); c.Px((int)(cx + 6), (int)(cy - 0.5f), f == 1 ? MY : MW);
                    break;
                case "mini":
                    foreach (var s in new[] { -1, 1 }) foreach (var k in new[] { -2, 1 }) c.Line(cx + k, cy + s * 2, cx + k + (f == 1 ? 1 : -1) * 0.5f, cy + s * 4.2f, MN);
                    c.Fill(c.Ell(cx - 0.5f, cy, 3.4f, 2.6f), b); c.Fill(c.Disc(cx + 3, cy, 1.6f), MM); c.Px((int)(cx + 3), (int)(cy - 0.5f), MR);
                    break;
                case "turret":
                    c.Fill(c.Box(cx - 6, cy - 6, cx + 6, cy + 6), MM); c.Fill(c.Box(cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f), MN);
                    foreach (var (x, y) in new[] { (-5, -5), (4, -5), (-5, 4), (4, 4) }) c.Px((int)(cx + x), (int)(cy + y), ML);
                    for (int i = -4; i < 4; i += 2) { c.Px((int)(cx + i), (int)(cy - 6), b); c.Px((int)(cx + i + 1), (int)(cy + 5), b); }
                    break;
                case "scout":
                    c.Fill(c.Poly(new[] { (cx + 6, cy), (cx - 2, cy - 5.5f), (cx - 5, cy - 5), (cx - 2, cy - 1.5f), (cx - 2, cy + 1.5f), (cx - 5, cy + 5), (cx - 2, cy + 5.5f) }), b);
                    c.Fill(c.Poly(new[] { (cx + 3, cy), (cx - 3, cy - 1.6f), (cx - 3, cy + 1.6f) }), MM); c.Px((int)(cx + 4), (int)(cy - 0.5f), MR); c.Px((int)(cx - 4), (int)(cy - 0.5f), MO); c.Px((int)(cx - 5), (int)(cy - 0.5f), MY);
                    break;
                case "kamikaze":
                    foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) c.Line(cx - 0.5f + sx * 3, cy - 0.5f + sy * 3, cx - 0.5f + sx * 5.5f, cy - 0.5f + sy * 5.5f, MN, 2);
                    c.Fill(c.Disc(cx, cy, 4.4f), b); c.Fill(c.Disc(cx, cy, 1.8f), MK, false);
                    break;
                case "splitter":
                    c.Fill(c.Ell(cx - 2.4f, cy, 4, 4.6f), b); c.Fill(c.Ell(cx + 2.4f, cy, 4, 4.6f), b); c.Line(cx - 0.5f, cy - 4, cx - 0.5f, cy + 3.5f, Tone(b, -0.6f));
                    c.Fill(c.Disc(cx - 2.5f, cy - 0.5f, 1.3f), MW, false); c.Fill(c.Disc(cx + 2.5f, cy + 0.5f, 1.3f), MW, false);
                    break;
                case "shield":
                    c.Fill(c.Disc(cx, cy, 5.6f), b); c.Fill(c.Box(cx + 0.5f, cy - 3, cx + 4.5f, cy + 3), MN); c.Px((int)(cx + 3), (int)(cy - 1), MC); c.Px((int)(cx + 3), (int)cy, MC);
                    c.Fill(c.Disc(cx - 2, cy, 1.5f), Tone(b, 0.5f), false);
                    break;
                case "sniper":
                    foreach (var t in new[] { 2.2f, 3.4f, 4.6f }) c.Line(cx - 0.5f, cy - 0.5f, cx - 0.5f + Mathf.Cos(t) * 5.5f, cy - 0.5f + Mathf.Sin(t) * 5.5f, MN);
                    c.Fill(c.Box(cx, cy - 1, cx + 8, cy + 1), MN); c.Fill(c.Disc(cx, cy, 3.2f), b); c.Px((int)(cx + 1), (int)(cy - 0.5f), MR); c.Px((int)(cx + 7), (int)(cy - 0.5f), ML);
                    break;
                case "mortar":
                {
                    const float r = 6f, q = 2.6f;
                    c.Fill(c.Poly(new[] { (cx - q, cy - r), (cx + q, cy - r), (cx + r, cy - q), (cx + r, cy + q), (cx + q, cy + r), (cx - q, cy + r), (cx - r, cy + q), (cx - r, cy - q) }), MM);
                    c.Fill(c.Disc(cx, cy, 3.8f), b); c.Fill(c.Disc(cx, cy, 2.2f), MK, false); c.Px((int)(cx - 5), (int)(cy - 0.5f), MY); c.Px((int)(cx + 4), (int)(cy - 0.5f), MY);
                    break;
                }
                case "brute":
                    c.Fill(c.Ell(cx - 1, cy - 6, 4, 2.6f), Tone(b, -0.2f)); c.Fill(c.Ell(cx - 1, cy + 6, 4, 2.6f), Tone(b, -0.2f));
                    c.Fill(c.Box(cx - 6, cy - 5, cx + 5, cy + 5), b); c.Fill(c.Box(cx + 4, cy - 6.5f, cx + 7.5f, cy - 2.5f), MM); c.Fill(c.Box(cx + 4, cy + 2.5f, cx + 7.5f, cy + 6.5f), MM);
                    for (int y = -3; y <= 2; y += 2) c.Line(cx + 1, cy + y, cx + 3, cy + y, MK);
                    c.Px((int)(cx + 3), (int)(cy - 2.5f), MY); c.Px((int)(cx + 3), (int)(cy + 1.5f), MY);
                    break;
                case "repair":
                    c.Line(cx - 6, cy - 0.5f, cx - 4, cy - 0.5f, MN, 2); c.Line(cx + 4, cy - 0.5f, cx + 6, cy - 0.5f, MN, 2);
                    c.Fill(c.Disc(cx, cy, 4.8f), b); c.Fill(c.Box(cx - 0.9f, cy - 3.4f, cx + 0.9f, cy + 3.4f), MW, false); c.Fill(c.Box(cx - 3.4f, cy - 0.9f, cx + 3.4f, cy + 0.9f), MW, false);
                    break;
                case "phantom":
                    c.Fill(c.Poly(new[] { (cx + 6.5f, cy), (cx, cy - 4), (cx - 6, cy - 5.5f), (cx - 3, cy), (cx - 6, cy + 5.5f), (cx, cy + 4) }), b); c.Px((int)(cx + 3), (int)(cy - 0.5f), MW); c.Px((int)(cx + 4), (int)(cy - 0.5f), MW);
                    break;
                case "laser":
                    foreach (var (x0, y0, x1, y1) in new[] { (-7, -2, -4, 2), (4, -2, 7, 2), (-2, -7, 2, -4), (-2, 4, 2, 7) }) c.Fill(c.Box(cx + x0, cy + y0, cx + x1, cy + y1), b);
                    c.Fill(c.Box(cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f), MM); c.Fill(c.Disc(cx, cy, 2.6f), MK, false); c.Fill(c.Disc(cx, cy, 1.4f), MW, false);
                    break;
                default:
                    c.Fill(c.Disc(cx, cy, Mathf.Min(cx, cy) - 1), b);
                    break;
            }
        }

        // ---------- bosses: a regular polygon with a reactor core, rivets and red vents (bossSprite in the prototype) ----------
        public Sprite Boss(BomberBossDef d)
        {
            var key = "boss_" + d.Key;
            if (_enemy.TryGetValue(key, out var s)) return s;
            float R = d.R / 2f * 1.08f; int size = Mathf.CeilToInt(R * 2f) + 3, n = d.Sides;
            s = Build(size, size, c =>
            {
                float cx = c.W / 2f - 1f, cy = c.H / 2f - 1f, seg = Mathf.PI * 2f / n;
                var pts = new (float, float)[n]; for (int i = 0; i < n; i++) pts[i] = (cx + Mathf.Cos(i * seg) * R, cy + Mathf.Sin(i * seg) * R);
                var poly = c.Poly(pts); c.Fill(poly, d.Color);
                Color32 dark = Tone(d.Color, -0.4f), light = Tone(d.Color, 0.45f);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f; if (!poly(px, py)) continue;
                    float dx = px - cx, dy = py - cy, r = Mathf.Sqrt(dx * dx + dy * dy), t = Mathf.Atan2(dy, dx), m = ((t % seg) + seg) % seg;
                    Color32? col = null;
                    if (r < R * 0.22f) col = MW; else if (r < R * 0.32f) col = light; else if (r < R * 0.4f) col = MK;
                    else if (r > R * 0.48f && r < R * 0.6f && Mathf.Abs(m - seg / 2f) < 0.18f) col = MR;
                    else if (r > R * 0.4f && (m < 0.06f || m > seg - 0.06f)) col = MO;
                    else if (r > R * 0.68f && r < R * 0.76f) col = dark;
                    if (col.HasValue) c.Px(x, y, col.Value);
                }
            });
            _enemy[key] = s;
            return s;
        }

        // ---------- floor ----------
        public Sprite MakeFloor(int cellsW, int cellsH, BomberSectorDef sec, uint seed)
        {
            var rnd = new BomberRng(seed);
            return Build(cellsW * 16, cellsH * 16, c =>
            {
                for (int ty = 0; ty < cellsH; ty++)
                for (int tx = 0; tx < cellsW; tx++)
                {
                    int ox = tx * 16, oy = ty * 16; float v = rnd.Next() * 8f - 4f;
                    c.Rect(ox, oy, 16, 16, Shade(sec.Floor, v)); c.Rect(ox, oy, 16, 1, Shade(sec.Floor, v + 9)); c.Rect(ox, oy, 1, 16, Shade(sec.Floor, v + 9));
                    c.Rect(ox, oy + 15, 16, 1, Shade(sec.Floor, v - 12)); c.Rect(ox + 15, oy, 1, 16, Shade(sec.Floor, v - 12));
                    float r = rnd.Next();
                    if (r < 0.08f) { for (int k = 3; k < 13; k += 2) c.Rect(ox + 3, oy + k, 10, 1, Shade(sec.Floor, v - 16)); c.Rect(ox + 3, oy + 2, 10, 1, Shade(sec.Floor, v + 6)); }
                    else if (r < 0.16f) { var cc = Shade(sec.Floor, v + 18); c.Px(ox + 2, oy + 2, cc); c.Px(ox + 13, oy + 2, cc); c.Px(ox + 2, oy + 13, cc); c.Px(ox + 13, oy + 13, cc); }
                    else if (r < 0.21f) { c.Rect(ox + 4, oy + 4, 8, 1, Shade(sec.Floor, v - 10)); c.Rect(ox + 4, oy + 4, 1, 8, Shade(sec.Floor, v - 10)); c.Rect(ox + 4, oy + 11, 8, 1, Shade(sec.Floor, v + 8)); c.Rect(ox + 11, oy + 4, 1, 8, Shade(sec.Floor, v + 8)); }
                    for (int k = 0; k < 3; k++) c.Px(ox + 1 + rnd.Range(14), oy + 1 + rnd.Range(14), Shade(sec.Floor, rnd.Next() < 0.5f ? -8 : 8));
                }
                var a = sec.Accent; a.a = 64;
                for (int i = 0; i < 3; i++)
                {
                    float x0 = 30 + rnd.Next() * (cellsW * 16 - 60), amp = 10 + rnd.Next() * 30, ph = rnd.Next() * 6;
                    for (int y = 0; y < cellsH * 16; y++) { int x = (int)(x0 + Mathf.Sin(y / 70f + ph) * amp); c.Blend(x, y, a); c.Blend(x + 1, y, a); }
                }
                // hazard stripes at the bottom entrance and under the top exits
                for (int y = 0; y < 7; y++) for (int x = 0; x < 48; x++) if (((x + y) >> 2 & 1) == 1) c.Blend(56 + x, cellsH * 16 - 23 + y, new Color32(255, 200, 60, 56));
                for (int y = 0; y < 7; y++) for (int x = 0; x < cellsW * 16 - 48; x++) if (((x + y) >> 2 & 1) == 1) c.Blend(24 + x, 16 + y, new Color32(255, 200, 60, 56));
            }, outline: false, mesh: SpriteMeshType.FullRect);
        }

        // ---------- builders ----------
        Sprite Build(int w, int h, Action<Canvas> draw, bool outline = true, Vector2? pivot = null, SpriteMeshType mesh = SpriteMeshType.Tight)
        {
            int pad = outline ? 1 : 0;
            var c = new Canvas(w + pad * 2, h + pad * 2, pad);
            draw(c);
            if (outline) c.AddOutline(Outline);
            var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "gc26_bomber_px" };
            tex.SetPixels32(c.ToTexturePixels()); tex.Apply(false, true);
            var piv = pivot ?? new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(tex, new Rect(0, 0, c.W, c.H), piv, PixelsPerUnit, 0, mesh);
            _owned.Add(tex); _owned.Add(sprite);
            return sprite;
        }

        Sprite Smooth(int size, bool ring, float ppu, bool ui = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "gc26_bomber_round" };
            var px = new Color32[size * size]; float r = size / 2f - 1f, th = ui ? size * 0.05f : 2.2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - size / 2f) * (x + 0.5f - size / 2f) + (y + 0.5f - size / 2f) * (y + 0.5f - size / 2f));
                float a = ring ? Mathf.Clamp01(th / 2f + 0.5f - Mathf.Abs(d - (r - th / 2f))) : Mathf.Clamp01(r - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
            _owned.Add(tex); _owned.Add(sprite);
            return sprite;
        }

        /// <summary>Additive brightness change (floor tiles), like the prototype's shade().</summary>
        /// <summary>Soft radial texture: alpha = f(distance from centre / radius).</summary>
        Sprite Radial(int size, float ppu, Func<float, float> f, bool square = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "gc26_bomber_radial" };
            var px = new Color32[size * size]; float h = size / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h, d = square ? Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 0.6f + Mathf.Sqrt(dx * dx + dy * dy) * 0.4f : Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(f(d)) * 255));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
            _owned.Add(tex); _owned.Add(sprite);
            return sprite;
        }

        static Color32 Shade(Color32 c, float v)
        {
            byte Cl(float x) => (byte)Mathf.Clamp(x, 0, 255);
            return new Color32(Cl(c.r + v), Cl(c.g + v), Cl(c.b + v * 1.3f), 255);
        }

        /// <summary>Blend toward white (f &gt; 0) or near-black (f &lt; 0), like the prototype's tone().</summary>
        static Color32 Tone(Color32 c, float f)
        {
            var t = f < 0 ? new Color32(6, 8, 20, 255) : new Color32(255, 255, 255, 255); float k = Mathf.Abs(f);
            return new Color32((byte)(c.r + (t.r - c.r) * k), (byte)(c.g + (t.g - c.g) * k), (byte)(c.b + (t.b - c.b) * k), c.a);
        }

        /// <summary>CPU canvas with y pointing DOWN (like the prototype); flipped when uploaded.</summary>
        public sealed class Canvas
        {
            public readonly int W, H; readonly int _o; readonly Color32[] _px;
            public Canvas(int w, int h, int offset) { W = w; H = h; _o = offset; _px = new Color32[w * h]; }

            public void Px(int x, int y, Color32 c) { x += _o; y += _o; if (x >= 0 && y >= 0 && x < W && y < H) _px[y * W + x] = c; }
            public void Rect(int x, int y, int w, int h, Color32 c) { for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Px(i, j, c); }
            public void Blend(int x, int y, Color32 c)
            {
                x += _o; y += _o; if (x < 0 || y < 0 || x >= W || y >= H) return;
                var d = _px[y * W + x]; float a = c.a / 255f;
                _px[y * W + x] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), 255);
            }
            /// <summary>Fills a shape (fn gets pixel-centre coords) with a bevel: light top-left, dark bottom-right.</summary>
            public void Fill(Func<float, float, bool> fn, Color32 col, bool bevel = true)
            {
                var L = Tone(col, 0.42f); var D = Tone(col, -0.42f); int w = W - _o * 2, h = H - _o * 2;
                bool In(int x, int y) => x >= -_o && y >= -_o && x < w + _o && y < h + _o && fn(x + 0.5f, y + 0.5f);
                for (int y = -_o; y < h + _o; y++) for (int x = -_o; x < w + _o; x++)
                {
                    if (!In(x, y)) continue;
                    var k = col; if (bevel) { if (!In(x, y - 1) || !In(x - 1, y)) k = L; else if (!In(x, y + 1) || !In(x + 1, y)) k = D; }
                    Px(x, y, k);
                }
            }
            public Func<float, float, bool> Disc(float cx, float cy, float r) => (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r;
            public Func<float, float, bool> Ell(float cx, float cy, float rx, float ry) => (x, y) => (x - cx) / rx * ((x - cx) / rx) + (y - cy) / ry * ((y - cy) / ry) < 1f;
            public Func<float, float, bool> Box(float x0, float y0, float x1, float y1) => (x, y) => x > x0 && x < x1 && y > y0 && y < y1;
            public Func<float, float, bool> Poly((float x, float y)[] p) => (x, y) =>
            {
                bool ins = false;
                for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                    if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) ins = !ins;
                return ins;
            };
            public void Line(float x0, float y0, float x1, float y1, Color32 c, int t = 1)
            {
                int n = (int)(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2) + 1;
                for (int i = 0; i <= n; i++) { float k = i / (float)n; Rect(Mathf.FloorToInt(x0 + (x1 - x0) * k), Mathf.FloorToInt(y0 + (y1 - y0) * k), t, t, c); }
            }
            public void AddOutline(Color32 c)
            {
                var add = new List<int>();
                bool A(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && _px[y * W + x].a > 0;
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                    if (!A(x, y) && (A(x - 1, y) || A(x + 1, y) || A(x, y - 1) || A(x, y + 1))) add.Add(y * W + x);
                foreach (var i in add) _px[i] = c;
            }
            public Color32[] ToTexturePixels()
            {
                var o = new Color32[_px.Length];
                for (int y = 0; y < H; y++) Array.Copy(_px, y * W, o, (H - 1 - y) * W, W);
                return o;
            }
        }
    }
}
