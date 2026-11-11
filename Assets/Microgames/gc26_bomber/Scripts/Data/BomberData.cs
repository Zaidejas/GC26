using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    public sealed class BomberEnemyDef
    {
        public string Name; public float Hp, R, Speed; public int Cost; public Color32 Color;
    }

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
        };
        public static readonly string[][] Pools = { new[] { "beetle", "turret", "scout", "kamikaze", "splitter" } };

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
        };
        public static readonly Dictionary<string, Color32> RoomColor = new Dictionary<string, Color32>
        {
            ["combat"] = C("#8fb4ff"), ["elite"] = C("#ffd65c"), ["shop"] = C("#6dffb0"), ["chest"] = C("#7fe3ff"), ["boss"] = C("#ff5a6e"), ["next"] = C("#7fe3ff"),
            ["start"] = C("#8fb4ff"), ["workbench"] = C("#ffb347"), ["altar"] = C("#ff5ad1"), ["rescue"] = C("#ff9adf"),
        };

        public static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
