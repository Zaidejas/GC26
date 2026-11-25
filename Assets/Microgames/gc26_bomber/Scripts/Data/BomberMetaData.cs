using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    public sealed class BomberCharDef
    {
        public string Id, Name, Ability, AbilityDesc, Trait, Desc, Ach; public float Dmg, Cool, Dash, Speed, Cd; public int Hp, Cost; public bool Rescue; public Color32 Color;
    }

    public sealed class BomberUpDef { public string Id, Name, Desc; public int[] Cost; }
    public sealed class BomberNpcDef { public string Id, Name, Bonus, Line; public bool Home, Char; public Color32 Color; }
    public sealed class BomberAchDef { public string Id, Name, Desc, Reward; public int Data; }
    public sealed class BomberStationDef { public string Id, Name; public float X, Y; public Color32 Color; }

    /// <summary>Hub and meta progression tables from the prototype: drones, upgrades bought with Data, survivors, achievements.</summary>
    public static class BomberMetaData
    {
        static Color32 C(string h) => BomberData.C(h);

        public static readonly BomberCharDef[] Chars =
        {
            new BomberCharDef { Id = "spark", Name = "Искра", Dmg = 1.0f, Cool = 1.0f, Dash = 1.0f, Hp = 6, Speed = 192, Color = C("#7fe3ff"), Ability = "Разгон", AbilityDesc = "5 с выстрелы не греют ядро.", Cd = 16, Trait = "Сборщик: +1 скрап с каждого врага.", Desc = "Сбалансированный ремонтный дрон." },
            new BomberCharDef { Id = "whisper", Name = "Шёпот", Dmg = 1.15f, Cool = 0.95f, Dash = 0.85f, Hp = 5, Speed = 205, Color = C("#b48cff"), Ability = "Невидимость", AbilityDesc = "4 с невидим: враги теряют тебя и не стреляют. Первый залп из невидимости — урон ×3.", Cd = 14, Cost = 80, Trait = "Охотник: +25% урона по элитам и боссам.", Desc = "Хрупкий дрон-разведчик." },
            new BomberCharDef { Id = "bastion", Name = "Бастион", Dmg = 0.95f, Cool = 0.9f, Dash = 1.3f, Hp = 8, Speed = 165, Color = C("#ffd65c"), Ability = "Броня", AbilityDesc = "5 с неуязвим и таранит врагов при касании на 25 урона.", Cd = 18, Ach = "sector3", Trait = "Толстая броня: неуязвимость после попадания на 50% дольше.", Desc = "Тяжёлый и медленный, трудно убить." },
            new BomberCharDef { Id = "sapper", Name = "Сапёр", Dmg = 1.05f, Cool = 1.0f, Dash = 1.1f, Hp = 6, Speed = 185, Color = C("#ff8a5c"), Ability = "Динамит", AbilityDesc = "Бросает динамит в ближайшего врага: фитиль 0.7 с, 80 урона по площади. 3 заряда, каждый восстанавливается за 5 с.", Cd = 5, Cost = 60, Trait = "Взрывотехник: радиус всех твоих взрывов +25%.", Desc = "Дрон-подрывник." },
            new BomberCharDef { Id = "dart", Name = "Стриж", Dmg = 0.9f, Cool = 1.05f, Dash = 0.6f, Hp = 4, Speed = 215, Color = C("#6dffb0"), Ability = "Адреналин", AbilityDesc = "5 с: скорость +60%, рывок без перезарядки, скорострельность +30%.", Cd = 15, Ach = "nodmg10", Trait = "Скользкий: рывок на 40% длиннее.", Desc = "Самый быстрый и самый хрупкий." },
            new BomberCharDef { Id = "forge", Name = "Кузня", Dmg = 0.95f, Cool = 1.15f, Dash = 1.1f, Hp = 7, Speed = 175, Color = C("#ff9f43"), Ability = "Турель", AbilityDesc = "Ставит турель на 9 с, она стреляет в ближайших врагов.", Cd = 13, Rescue = true, Trait = "Инженер: ядро греется на 40% медленнее, но Форсажа нет.", Desc = "Инженерный дрон. Спасается в забеге." },
            new BomberCharDef { Id = "chrono", Name = "Хронос", Dmg = 1.0f, Cool = 1.2f, Dash = 1.0f, Hp = 5, Speed = 195, Color = C("#5ce1e6"), Ability = "Замедление", AbilityDesc = "4 с враги, боссы и их пули движутся в 3 раза медленнее.", Cd = 18, Rescue = true, Trait = "Предвидение: вражеские пули на 12% медленнее.", Desc = "Экспериментальный дрон. Спасается в забеге." },
            new BomberCharDef { Id = "leech", Name = "Пиявка", Dmg = 1.1f, Cool = 0.95f, Dash = 0.9f, Hp = 5, Speed = 200, Color = C("#ff4d8d"), Ability = "Вихрь", AbilityDesc = "3 с вихрь стягивает врагов и наносит 30 урона/с.", Cd = 15, Rescue = true, Trait = "Утилизатор: каждые 12 убийств чинят 1 ячейку.", Desc = "Дрон-утилизатор. Спасается в забеге." },
        };
        public static BomberCharDef Char(string id) { foreach (var c in Chars) if (c.Id == id) return c; return Chars[0]; }

        /// <summary>Per-drone upgrades bought in the character capsule.</summary>
        public static readonly BomberUpDef[] CharUps =
        {
            new BomberUpDef { Id = "hp", Name = "Корпус", Desc = "+1 к здоровью", Cost = new[] { 40, 80, 130 } },
            new BomberUpDef { Id = "dmg", Name = "Орудия", Desc = "+6% урона", Cost = new[] { 40, 80, 130 } },
            new BomberUpDef { Id = "spd", Name = "Двигатели", Desc = "+5% скорости, рывок −8% перезарядки", Cost = new[] { 30, 60, 100 } },
            new BomberUpDef { Id = "abil", Name = "Способность", Desc = "−10% перезарядки способности", Cost = new[] { 50, 90, 140 } },
        };

        /// <summary>Permanent upgrades for every drone (workshop).</summary>
        public static readonly BomberUpDef[] MetaUps =
        {
            new BomberUpDef { Id = "hp", Name = "Усиленный корпус", Desc = "+1 к максимальному здоровью", Cost = new[] { 40, 80, 140 } },
            new BomberUpDef { Id = "cool", Name = "Радиаторы", Desc = "+10% остывания", Cost = new[] { 30, 60, 110 } },
            new BomberUpDef { Id = "dmg", Name = "Калибровка стволов", Desc = "+6% урона всем оружием", Cost = new[] { 50, 100, 160 } },
            new BomberUpDef { Id = "scrap", Name = "Тайник со скрапом", Desc = "+25 скрапа в начале забега", Cost = new[] { 25, 50, 90 } },
            new BomberUpDef { Id = "emp", Name = "Запас ЭМИ", Desc = "+1 ЭМИ-граната в начале", Cost = new[] { 30, 60 } },
            new BomberUpDef { Id = "slot", Name = "Доп. отсек модулей", Desc = "+1 слот модуля (5 вместо 4)", Cost = new[] { 120 } },
            new BomberUpDef { Id = "comp", Name = "Док компаньонов", Desc = "+1 слот компаньона (2 вместо 1)", Cost = new[] { 100 } },
            new BomberUpDef { Id = "luck", Name = "Удачливый сканер", Desc = "Сундуки, алтари, элиты и боссы дают 4 варианта", Cost = new[] { 90 } },
        };

        public static readonly BomberNpcDef[] Npcs =
        {
            new BomberNpcDef { Id = "dispatch", Name = "Диспетчер Кай", Color = C("#7fe3ff"), Home = true, Bonus = "Всегда в хабе", Line = "Связь есть, дрон. Станцию захватил ОРАКУЛ — пробейся к его ядру и отправь последний сигнал." },
            new BomberNpcDef { Id = "mechanic", Name = "Механик Ори", Color = C("#ffb347"), Bonus = "Улучшения на верстаке на 20% дешевле", Line = "Я снова у станка! Тащи железо — подкручу." },
            new BomberNpcDef { Id = "quarter", Name = "Интендант Вега", Color = C("#ffd65c"), Bonus = "+15 скрапа в начале каждого забега", Line = "Запасы — моё всё. Держи на дорогу." },
            new BomberNpcDef { Id = "doctor", Name = "Доктор Лин", Color = C("#6dffb0"), Bonus = "+1 к максимальному здоровью", Line = "Корпус в норме. Почти. Не лезь под турели." },
            new BomberNpcDef { Id = "archivist", Name = "Архивист Сол", Color = C("#b48cff"), Bonus = "+10% Данных за забег", Line = "Каждый байт с этой станции бесценен." },
            new BomberNpcDef { Id = "gunsmith", Name = "Оружейница Тэй", Color = C("#ff8a5c"), Bonus = "В магазинах на 1 товар больше", Line = "Я знаю, где ОРАКУЛ прячет хорошие стволы." },
            new BomberNpcDef { Id = "forge", Name = "Кузня", Color = C("#ff9f43"), Char = true, Bonus = "Открыт играбельный дрон Кузня", Line = "Свободна! Дай мне гаечный ключ и пару врагов." },
            new BomberNpcDef { Id = "chrono", Name = "Хронос", Color = C("#5ce1e6"), Char = true, Bonus = "Открыт играбельный дрон Хронос", Line = "Я видел, как ты придёшь. Секунд за сорок до этого." },
            new BomberNpcDef { Id = "leech", Name = "Пиявка", Color = C("#ff4d8d"), Char = true, Bonus = "Открыт играбельный дрон Пиявка", Line = "Их металл — мой металл." },
        };
        public static BomberNpcDef Npc(string id) { foreach (var n in Npcs) if (n.Id == id) return n; return null; }

        public static readonly BomberAchDef[] Achs =
        {
            new BomberAchDef { Id = "boss_loader", Name = "Тяжёлая работа", Desc = "Победить Грузчика МК-II", Reward = "Рельсотрон" },
            new BomberAchDef { Id = "boss_hive", Name = "Дезинсекция", Desc = "Победить Матку роя", Reward = "Ракетный рой" },
            new BomberAchDef { Id = "boss_smelter", Name = "Холодный приём", Desc = "Победить Плавильщика", Reward = "Огнемёт" },
            new BomberAchDef { Id = "boss_warden", Name = "Побег", Desc = "Победить Надзирателя", Reward = "Дуговой разрядник" },
            new BomberAchDef { Id = "sector2", Name = "В пекло", Desc = "Дойти до сектора 2", Reward = "Крио-пушка" },
            new BomberAchDef { Id = "sector3", Name = "К ядру", Desc = "Дойти до сектора 3", Reward = "Персонаж Бастион" },
            new BomberAchDef { Id = "win", Name = "Последний сигнал", Desc = "Победить ОРАКУЛА", Reward = "Лазерный резак" },
            new BomberAchDef { Id = "kills300", Name = "Сборщик металлолома", Desc = "Уничтожить 300 машин (всего)", Reward = "Пиломёт" },
            new BomberAchDef { Id = "over50", Name = "Расплав", Desc = "Перегреться 50 раз (всего)", Reward = "Миниган" },
            new BomberAchDef { Id = "nodmg10", Name = "Неприкасаемый", Desc = "Зачистить 10 комнат без урона (всего)", Reward = "Персонаж Стриж" },
            new BomberAchDef { Id = "rooms12", Name = "Держать линию", Desc = "Зачистить 12 комнат за один забег", Reward = "Компаньон Пикировщик" },
            new BomberAchDef { Id = "flawless", Name = "Без царапины", Desc = "Победить босса, не получив урона", Reward = "+40 Данных", Data = 40 },
            new BomberAchDef { Id = "fusion", Name = "Кузнец", Desc = "Слить два оружия на верстаке", Reward = "+50 Данных", Data = 50 },
            new BomberAchDef { Id = "combo25", Name = "В ударе", Desc = "Набрать комбо ×25", Reward = "+40 Данных", Data = 40 },
            new BomberAchDef { Id = "rescue3", Name = "Спасатель", Desc = "Спасти 3 выживших", Reward = "+60 Данных", Data = 60 },
        };

        /// <summary>How a base weapon is unlocked: Data price in the armory, or an achievement. Neither = open from the start.</summary>
        public static readonly Dictionary<string, (int cost, string ach)> WeaponUnlock = new Dictionary<string, (int, string)>
        {
            ["rifle"] = (20, null), ["nail"] = (30, null), ["plasma"] = (45, null),
            ["rail"] = (0, "boss_loader"), ["swarm"] = (0, "boss_hive"), ["flamer"] = (0, "boss_smelter"), ["arc"] = (0, "boss_warden"),
            ["cryo"] = (0, "sector2"), ["beam"] = (0, "win"), ["disc"] = (0, "kills300"), ["minigun"] = (0, "over50"),
        };

        /// <summary>Hub terminals (portrait hub room).</summary>
        public static readonly BomberStationDef[] Stations =
        {
            new BomberStationDef { Id = "chars", Name = "Капсула персонажей", X = 120, Y = 250, Color = C("#7fe3ff") },
            new BomberStationDef { Id = "armory", Name = "Арсенал", X = 424, Y = 250, Color = C("#ffb347") },
            new BomberStationDef { Id = "workshop", Name = "Мастерская", X = 120, Y = 500, Color = C("#6dffb0") },
            new BomberStationDef { Id = "records", Name = "Достижения", X = 424, Y = 500, Color = C("#ffd65c") },
            new BomberStationDef { Id = "training", Name = "Тренировка", X = 120, Y = 740, Color = C("#b48cff") },
        };
        public static BomberStationDef Station(string id) { foreach (var s in Stations) if (s.Id == id) return s; return null; }
    }
}
