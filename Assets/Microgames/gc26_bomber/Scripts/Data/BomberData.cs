using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    public sealed class BomberEnemyDef
    {
        public string Name; public float Hp, R, Speed; public int Cost; public Color32 Color;
    }

    public sealed class BomberBossDef
    {
        public string Key, Name; public float Hp, R; public Color32 Color; public float[] Phases, Idle; public int Sides;
        public BomberEnemyDef Def;
    }

    public sealed class BomberAffixDef { public string Name; public Color32 Color; }

    public sealed class BomberSectorDef
    {
        public string Name, Line; public Color32 Tint, Floor, Accent;
    }

    /// <summary>
    /// Game numbers ported from the HTML prototype (Prototype~/last-signal.html). Read-only tables.
    /// Units are prototype pixels: 1 room cell = 32 px = 1 Unity unit.
    /// </summary>
    public static class BomberData
    {
        // world (portrait phone room)
        public const float Cell = 32f;
        public const int GW = 17, GH = 30;
        public const float W = GW * Cell, H = GH * Cell;

        // difficulty (prototype HARD)
        public const float BulletMul = 1.2f, BossHp = 1.4f, IFrames = 0.5f, Interval = 0.8f, EnemySpeed = 1.1f;
        public const int Budget = 9;
        public static readonly float[] HpMul = { 1.0f, 1.4f, 1.85f };
        public static readonly float[] EliteChance = { 0.08f, 0.18f, 0.28f };
        public static readonly float[] SectorMult = { 0.85f, 1.6f, 2.3f };

        public static readonly Dictionary<string, BomberEnemyDef> Enemies = new Dictionary<string, BomberEnemyDef>
        {
            ["beetle"]   = new BomberEnemyDef { Name = "Жук-сварщик", Hp = 20, R = 10, Speed = 125, Cost = 1, Color = C("#ff8a5c") },
            ["turret"]   = new BomberEnemyDef { Name = "Турель", Hp = 40, R = 12, Speed = 0, Cost = 2, Color = C("#ffcf5c") },
            ["scout"]    = new BomberEnemyDef { Name = "Дрон-разведчик", Hp = 15, R = 9, Speed = 155, Cost = 1, Color = C("#ff6fa8") },
            ["kamikaze"] = new BomberEnemyDef { Name = "Камикадзе", Hp = 10, R = 8, Speed = 175, Cost = 1, Color = C("#ff4d4d") },
            ["splitter"] = new BomberEnemyDef { Name = "Делитель", Hp = 40, R = 12, Speed = 95, Cost = 2, Color = C("#7fd1ff") },
            ["mini"]     = new BomberEnemyDef { Name = "Осколок", Hp = 12, R = 7, Speed = 155, Cost = 0, Color = C("#7fd1ff") },
            ["shield"]   = new BomberEnemyDef { Name = "Щитоносец", Hp = 80, R = 13, Speed = 58, Cost = 4, Color = C("#c98bff") },
            ["sniper"]   = new BomberEnemyDef { Name = "Снайпер", Hp = 30, R = 10, Speed = 90, Cost = 3, Color = C("#ff5a6e") },
            ["mortar"]   = new BomberEnemyDef { Name = "Мортира", Hp = 35, R = 12, Speed = 42, Cost = 3, Color = C("#ffa94d") },
            ["brute"]    = new BomberEnemyDef { Name = "Громила", Hp = 110, R = 15, Speed = 72, Cost = 5, Color = C("#ff6b3d") },
            ["repair"]   = new BomberEnemyDef { Name = "Ремонтник", Hp = 25, R = 10, Speed = 100, Cost = 3, Color = C("#7dffa8") },
            ["phantom"]  = new BomberEnemyDef { Name = "Фантом", Hp = 28, R = 9, Speed = 120, Cost = 3, Color = C("#d98cff") },
            ["laser"]    = new BomberEnemyDef { Name = "Лазерная турель", Hp = 60, R = 12, Speed = 0, Cost = 4, Color = C("#ff3df2") },
        };
        /// <summary>Enemy kinds by sector; a sector uses its own pool and all earlier ones.</summary>
        public static readonly string[][] Pools =
        {
            new[] { "beetle", "turret", "scout", "kamikaze", "splitter" },
            new[] { "shield", "sniper", "mortar", "brute" },
            new[] { "repair", "phantom", "laser" },
        };
        /// <summary>At most this many of a kind in one wave.</summary>
        public static readonly Dictionary<string, int> WaveCap = new Dictionary<string, int> { ["sniper"] = 2, ["repair"] = 1, ["laser"] = 2, ["brute"] = 2, ["mortar"] = 2 };

        public static readonly Dictionary<string, BomberAffixDef> Affixes = new Dictionary<string, BomberAffixDef>
        {
            ["fast"] = new BomberAffixDef { Name = "Быстрый", Color = C("#6dffb0") },
            ["armored"] = new BomberAffixDef { Name = "Бронированный", Color = C("#9fb4d8") },
            ["volatile"] = new BomberAffixDef { Name = "Взрывной", Color = C("#ff7a3d") },
            ["regen"] = new BomberAffixDef { Name = "Регенерация", Color = C("#7dffa8") },
            ["shielded"] = new BomberAffixDef { Name = "Щит", Color = C("#9fd0ff") },
        };
        public static readonly string[] AffixKeys = { "fast", "armored", "volatile", "regen", "shielded" };

        /// <summary>Sector bosses: sector 1 gets 0 or 1, sector 2 gets 2 or 3, sector 3 is always ОРАКУЛ.</summary>
        public static readonly BomberBossDef[] Bosses =
        {
            Boss("loader", "Грузчик МК-II", 900, 30, "#ffb347", new[] { 0.5f }, 6, new[] { 1.0f, 0.7f }),
            Boss("hive", "Матка роя", 1000, 32, "#9cff6d", new[] { 0.5f }, 9, new[] { 1.1f, 0.8f }),
            Boss("smelter", "Плавильщик", 1300, 28, "#ff7a3d", new[] { 0.5f }, 8, new[] { 0.95f, 0.65f }),
            Boss("warden", "Надзиратель", 1450, 30, "#6dc8ff", new[] { 0.5f }, 4, new[] { 0.95f, 0.65f }),
            Boss("oracle", "ОРАКУЛ", 1800, 30, "#ff5ad1", new[] { 0.66f, 0.33f }, 5, new[] { 0.85f, 0.6f, 0.45f }),
        };
        static BomberBossDef Boss(string key, string name, float hp, float r, string col, float[] phases, int sides, float[] idle)
            => new BomberBossDef { Key = key, Name = name, Hp = hp, R = r, Color = C(col), Phases = phases, Sides = sides, Idle = idle, Def = new BomberEnemyDef { Name = name, Hp = hp, R = r, Color = C(col) } };

        public static readonly BomberSectorDef[] Sectors =
        {
            new BomberSectorDef { Name = "Доки", Tint = C("#0b1220"), Floor = C("#0d1626"), Accent = C("#4a6fa5"), Line = "ОРАКУЛ: Обнаружен ремонтный юнит. Назначена утилизация." },
            new BomberSectorDef { Name = "Реакторный блок", Tint = C("#140d12"), Floor = C("#1a1016"), Accent = C("#a5584a"), Line = "ОРАКУЛ: Реактор встретит тебя тепло." },
            new BomberSectorDef { Name = "Ядро ОРАКУЛА", Tint = C("#110b1c"), Floor = C("#150e22"), Accent = C("#8a4aa5"), Line = "ОРАКУЛ: Ты не должен был сюда дойти." },
        };

        public static readonly Dictionary<string, string> RoomLabel = new Dictionary<string, string>
        {
            ["combat"] = "БОЙ", ["elite"] = "ЭЛИТА", ["shop"] = "МАГАЗИН", ["chest"] = "СУНДУК", ["boss"] = "БОСС", ["next"] = "СЛЕД. СЕКТОР",
            ["start"] = "БОЙ", ["workbench"] = "ВЕРСТАК", ["altar"] = "АЛТАРЬ", ["rescue"] = "СПАСЕНИЕ",
            ["launch"] = "НАЧАТЬ ЗАБЕГ", ["hub"] = "В ХАБ", ["tutorial"] = "ОБУЧЕНИЕ",
        };
        public static readonly Dictionary<string, Color32> RoomColor = new Dictionary<string, Color32>
        {
            ["combat"] = C("#8fb4ff"), ["elite"] = C("#ffd65c"), ["shop"] = C("#6dffb0"), ["chest"] = C("#7fe3ff"), ["boss"] = C("#ff5a6e"), ["next"] = C("#7fe3ff"),
            ["start"] = C("#8fb4ff"), ["workbench"] = C("#ffb347"), ["altar"] = C("#ff5ad1"), ["rescue"] = C("#ff9adf"),
            ["launch"] = C("#6dffb0"), ["hub"] = C("#6dffb0"), ["tutorial"] = C("#b48cff"),
        };

        public static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
