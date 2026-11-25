using System.Collections.Generic;

namespace GC26.Bomber
{
    /// <summary>A weapon (or the second fire mode of a fused weapon). Numbers from the prototype.</summary>
    public sealed class BomberWeaponDef
    {
        public string Id, Name, Desc, Kind = "", Alt, Spec, Look;
        public float Dmg, Rate, Heat, Speed, Spread, R = 4f, Kb = 60f, Life = 1.4f, Aoe, Slow, Grow, Range, AltCd;
        public int Pellets = 1, Pierce, Bounce, Chains;
        public bool Homing, Burn, Zap, Trail, Fused;
        public UnityEngine.Color32 Col; // own projectile colour (a = 0: default)
        public BomberWeaponDef AltW;
        public BomberWeaponDef Clone() => (BomberWeaponDef)MemberwiseClone();
    }

    public sealed class BomberModDef { public string Name, Cat, Desc; }
    public sealed class BomberCompDef { public string Name, Desc; }

    /// <summary>Weapons, modules, companions, upgrades and fusions — tables ported from the prototype.</summary>
    public static class BomberItems
    {
        public static readonly Dictionary<string, BomberWeaponDef> Weapons = new Dictionary<string, BomberWeaponDef>();
        public static readonly string[] BaseWeapons = { "blaster", "shotgun", "rifle", "nail", "plasma", "rail", "swarm", "flamer", "arc", "cryo", "beam", "disc", "minigun", "twin", "hammer", "scatter", "needle", "venom" };

        static BomberWeaponDef Add(BomberWeaponDef w) { Weapons[w.Id] = w; return w; }

        static BomberItems()
        {
            Add(new BomberWeaponDef { Id = "blaster", Name = "Бластер", Dmg = 10, Rate = 4, Heat = 4, Speed = 720, Spread = .02f, R = 4, Alt = "charge", Spec = "pierce", Desc = "Точный стартовый. Заряд: выстрел ×3." });
            Add(new BomberWeaponDef { Id = "shotgun", Name = "Дробовик «Сноп»", Dmg = 6, Rate = 1.2f, Heat = 14, Speed = 620, Spread = .42f, Pellets = 7, R = 3, Kb = 240, Life = .42f, Alt = "double", Spec = "pellets", Desc = "7 дробинок, отбрасывание. Дуплет." });
            Add(new BomberWeaponDef { Id = "rifle", Name = "Импульсная винтовка", Dmg = 7, Rate = 10, Heat = 2.5f, Speed = 800, Spread = .03f, Grow = .22f, R = 3, Alt = "grenade", Spec = "accuracy", Desc = "Разброс растёт при стрельбе. Граната." });
            Add(new BomberWeaponDef { Id = "nail", Name = "Гвоздомёт", Dmg = 5, Rate = 14, Heat = 1.5f, Speed = 760, Spread = .07f, R = 2.5f, Bounce = 1, Spec = "bounce", Desc = "Очень быстрый, гвозди рикошетят." });
            Add(new BomberWeaponDef { Id = "plasma", Name = "Плазменная пушка", Dmg = 40, Rate = 1, Heat = 22, Speed = 460, R = 7, Aoe = 64, Spec = "aoe", Desc = "Медленный шар, взрывается." });
            Add(new BomberWeaponDef { Id = "rail", Name = "Рельсотрон", Dmg = 60, Rate = .8f, Heat = 30, Speed = 2600, R = 4, Pierce = 99, Trail = true, Spec = "dmg2", Desc = "Пробивает всех врагов на линии." });
            Add(new BomberWeaponDef { Id = "swarm", Name = "Ракетный рой", Dmg = 15, Rate = .8f, Heat = 24, Speed = 360, Spread = .6f, Pellets = 4, R = 4, Aoe = 36, Homing = true, Life = 2.2f, Spec = "rockets", Look = "rocket", Desc = "4 самонаводящиеся ракеты." });
            Add(new BomberWeaponDef { Id = "flamer", Name = "Огнемёт", Dmg = 3, Rate = 20, Heat = 1.6f, Speed = 330, Spread = .22f, R = 6, Life = .36f, Pierce = 2, Burn = true, Spec = "range", Look = "flame", Desc = "Короткий конус огня, поджигает." });
            Add(new BomberWeaponDef { Id = "arc", Name = "Дуговой разрядник", Kind = "arc", Dmg = 16, Rate = 3, Heat = 7, Chains = 2, Range = 280, Spec = "chains", Desc = "Молния перескакивает до 3 врагов." });
            Add(new BomberWeaponDef { Id = "cryo", Name = "Крио-пушка", Dmg = 8, Rate = 5, Heat = 3.5f, Speed = 600, Spread = .04f, R = 4, Slow = 2, Spec = "slow", Desc = "Замедляет врагов на 50% на 2 с." });
            Add(new BomberWeaponDef { Id = "beam", Name = "Лазерный резак", Kind = "beam", Dmg = 2.4f, Rate = 20, Heat = 1, Spec = "dmg2", Desc = "Непрерывный луч, около 48 урона в секунду." });
            Add(new BomberWeaponDef { Id = "disc", Name = "Пиломёт", Dmg = 18, Rate = 2, Heat = 9, Speed = 430, R = 7, Bounce = 3, Pierce = 3, Life = 2.2f, Spec = "bounce", Look = "disc", Desc = "Пилы отскакивают от стен и режут насквозь." });
            // quest rewards
            Add(new BomberWeaponDef { Id = "twin", Name = "Близнецы", Dmg = 9, Rate = 6, Heat = 3, Speed = 760, Spread = .03f, Pellets = 2, R = 3.5f, Spec = "accuracy", Desc = "Два параллельных ствола: стабильный урон без разброса." });
            Add(new BomberWeaponDef { Id = "hammer", Name = "Гравимолот", Dmg = 34, Rate = 1.1f, Heat = 18, Speed = 420, R = 9, Aoe = 56, Spec = "aoe", Col = BomberData.C("#b48cff"), Desc = "Тяжёлый гравизаряд с большим взрывом." });
            Add(new BomberWeaponDef { Id = "scatter", Name = "Шрапнель", Dmg = 8, Rate = 1.5f, Heat = 12, Speed = 560, Spread = .3f, Pellets = 5, R = 4, Aoe = 26, Life = .5f, Kb = 120, Alt = "double", Spec = "pellets", Col = BomberData.C("#ffcf8a"), Desc = "Разрывная дробь: каждый осколок взрывается. Дуплет." });
            Add(new BomberWeaponDef { Id = "needle", Name = "Игла", Dmg = 42, Rate = 1.6f, Heat = 9, Speed = 1900, R = 3, Pierce = 1, Trail = true, Spec = "dmg2", Desc = "Снайперская винтовка, пробивает 2 цели." });
            Add(new BomberWeaponDef { Id = "venom", Name = "Кислотник", Dmg = 4, Rate = 12, Heat = 1.4f, Speed = 520, Spread = .12f, R = 4, Life = .6f, Burn = true, Slow = 1, Spec = "range", Col = BomberData.C("#9cff6d"), Desc = "Струя кислоты: поджигает и замедляет." });
            Add(new BomberWeaponDef { Id = "minigun", Name = "Миниган", Kind = "mini", Dmg = 6, Rate = 20, Heat = 1.8f, Speed = 780, Spread = .1f, R = 3, Spec = "spin", Desc = "Раскручивается до 20 выстрелов в секунду." });
            // fused (only made at the workbench)
            Add(new BomberWeaponDef { Id = "stormgun", Fused = true, Name = "Ракетный шторм", Kind = "mini", Dmg = 9, Rate = 16, Heat = 2.2f, Speed = 520, Spread = .14f, R = 3, Homing = true, Aoe = 26, Life = 1.6f, Spec = "spin", Look = "rocket",
                AltW = new BomberWeaponDef { Dmg = 22, Pellets = 8, Spread = 1.3f, Speed = 360, R = 4, Aoe = 42, Homing = true, Life = 2.2f, Heat = 30, AltCd = 1.4f, Look = "rocket" }, Desc = "Миниган мини-ракетами. Альт: залп из 8 ракет." });
            Add(new BomberWeaponDef { Id = "dragon", Fused = true, Name = "Драконье дыхание", Dmg = 7, Rate = 1.6f, Heat = 13, Speed = 600, Spread = .4f, Pellets = 9, R = 3.5f, Kb = 200, Life = .45f, Burn = true, Spec = "pellets",
                AltW = new BomberWeaponDef { Dmg = 4, Pellets = 6, Spread = .35f, Speed = 330, R = 7, Life = .5f, Pierce = 3, Burn = true, Heat = 6, AltCd = .12f, Look = "flame" }, Desc = "Зажигательная дробь. Альт: конус огня." });
            Add(new BomberWeaponDef { Id = "gauss", Fused = true, Name = "Гаусс-шторм", Dmg = 70, Rate = .9f, Heat = 28, Speed = 2600, R = 4, Pierce = 99, Trail = true, Zap = true, Spec = "dmg2",
                AltW = new BomberWeaponDef { Kind = "arc", Dmg = 22, Chains = 4, Range = 320, Heat = 10, AltCd = .35f }, Desc = "Рельса, бьющая молнией от каждого пробитого. Альт: цепь на 5 целей." });
            Add(new BomberWeaponDef { Id = "cryoplasma", Fused = true, Name = "Криоплазма", Dmg = 38, Rate = 1.4f, Heat = 18, Speed = 480, R = 8, Aoe = 80, Slow = 2.5f, Spec = "aoe",
                AltW = new BomberWeaponDef { Dmg = 9, Spread = .05f, Speed = 640, R = 4, Slow = 2.5f, Heat = 3, AltCd = .15f }, Desc = "Ледяные взрывы. Альт: крио-очередь." });
            Add(new BomberWeaponDef { Id = "grinder", Fused = true, Name = "Мясорубка", Dmg = 9, Rate = 9, Heat = 2.2f, Speed = 600, Spread = .08f, R = 5, Bounce = 2, Pierce = 1, Life = 1.4f, Spec = "bounce", Look = "disc",
                AltW = new BomberWeaponDef { Dmg = 55, Speed = 380, R = 13, Bounce = 5, Pierce = 8, Life = 3, Heat = 22, AltCd = 1.2f, Look = "disc" }, Desc = "Поток мини-пил. Альт: огромная пила." });
            Add(new BomberWeaponDef { Id = "pulsar", Fused = true, Name = "Пульсар", Dmg = 14, Rate = 7, Heat = 3, Speed = 900, Spread = .02f, R = 4, Pierce = 2, Spec = "pierce",
                AltW = new BomberWeaponDef { Dmg = 32, Pellets = 3, Spread = .35f, Speed = 440, R = 6, Aoe = 66, Life = .75f, Heat = 18, AltCd = 1.1f }, Desc = "Пробивающие импульсы. Альт: веер гранат." });
            Add(new BomberWeaponDef { Id = "tesla", Fused = true, Name = "Тесла-луч", Kind = "beam", Dmg = 3, Rate = 20, Heat = 1.1f, Zap = true, Spec = "dmg2",
                AltW = new BomberWeaponDef { Kind = "arc", Dmg = 20, Chains = 4, Range = 320, Heat = 9, AltCd = .35f }, Desc = "Луч с молниями. Альт: цепь на 5 целей." });
        }

        public static readonly (string id, string a, string b)[] Fusions =
        {
            ("stormgun", "minigun", "swarm"), ("dragon", "shotgun", "flamer"), ("gauss", "rail", "arc"),
            ("cryoplasma", "plasma", "cryo"), ("grinder", "nail", "disc"), ("pulsar", "blaster", "rifle"), ("tesla", "beam", "arc"),
        };
        public const int FuseNeed = 3, FuseCost = 60;

        public static readonly Dictionary<string, string> SpecText = new Dictionary<string, string>
        {
            ["pierce"] = "+1 пробитие", ["pellets"] = "+2 дробинки", ["rockets"] = "+1 ракета", ["accuracy"] = "−30% разброс", ["bounce"] = "+1 рикошет",
            ["aoe"] = "+20% радиус взрыва", ["dmg2"] = "+25% урона", ["range"] = "+30% дальность", ["chains"] = "+1 цель цепи", ["slow"] = "+1 с замедления", ["spin"] = "раскрутка вдвое быстрее",
        };
        /// <summary>Workbench upgrades: id, name, description, prices per level.</summary>
        public static readonly (string id, string name, string desc, int[] cost)[] Upgrades =
        {
            ("dmg", "Урон", "+20% урона", new[] { 30, 50, 80 }), ("rate", "Скорострельность", "+15% скорострельности", new[] { 30, 50, 80 }),
            ("heat", "Охлаждение", "−15% нагрева за выстрел", new[] { 25, 45, 70 }), ("spec", "Особое", null, new[] { 40, 70, 100 }),
        };

        public static readonly Dictionary<string, BomberModDef> Mods = new Dictionary<string, BomberModDef>
        {
            ["radiator"] = new BomberModDef { Name = "Радиатор", Cat = "Ядро", Desc = "Остывание +40%." },
            ["mad"] = new BomberModDef { Name = "Безумный реактор", Cat = "Ядро", Desc = "Форсаж уже с 60%, но перегрев длится 3 с." },
            ["glue"] = new BomberModDef { Name = "Термоклей", Cat = "Огонь", Desc = "Попадания поджигают: 3 урона/с, 3 с." },
            ["napalm"] = new BomberModDef { Name = "Напалм", Cat = "Огонь", Desc = "Горящие враги взрываются при смерти." },
            ["ion"] = new BomberModDef { Name = "Ионный след", Cat = "Движение", Desc = "Рывок оставляет след, который ранит врагов." },
            ["reflect"] = new BomberModDef { Name = "Отражатель", Cat = "Движение", Desc = "Рывок сквозь вражескую пулю отражает её." },
            ["splitter"] = new BomberModDef { Name = "Расщепитель", Cat = "Снаряды", Desc = "Снаряды делятся на 2 при попадании." },
            ["heavy"] = new BomberModDef { Name = "Тяжёлые пули", Cat = "Снаряды", Desc = "+25% урона, −15% скорострельности." },
            ["armor"] = new BomberModDef { Name = "Бронепластины", Cat = "Корпус", Desc = "+2 ячейки здоровья." },
            ["shieldm"] = new BomberModDef { Name = "Щит", Cat = "Корпус", Desc = "Блокирует одно попадание, восстанавливается за 6 с." },
            ["magnet"] = new BomberModDef { Name = "Магнит", Cat = "Экономика", Desc = "Радиус сбора скрапа ×2." },
            ["discount"] = new BomberModDef { Name = "Скидочный чип", Cat = "Экономика", Desc = "Цены в магазине и на верстаке −25%." },
        };
        public static readonly Dictionary<string, string> Sets = new Dictionary<string, string>
        {
            ["Ядро"] = "Бонус набора: остывание +15%", ["Огонь"] = "Бонус набора: огонь перекидывается при смерти горящего", ["Движение"] = "Бонус набора: перезарядка рывка −0.2 с",
            ["Снаряды"] = "Бонус набора: +10% урона и скорости снарядов", ["Корпус"] = "Бонус набора: +1 ячейка здоровья", ["Экономика"] = "Бонус набора: +1 скрап с каждого врага",
        };

        public static readonly Dictionary<string, BomberCompDef> Comps = new Dictionary<string, BomberCompDef>
        {
            ["gunner"] = new BomberCompDef { Name = "Боевой дрон", Desc = "Летает рядом и стреляет в ближайшего врага: 6 урона, 2 выстрела/с." },
            ["guard"] = new BomberCompDef { Name = "Сфера-страж", Desc = "Кружит вплотную и уничтожает вражеские пули, которых касается." },
            ["medic"] = new BomberCompDef { Name = "Медбот", Desc = "Чинит 1 ячейку здоровья после каждых 3 зачищенных комнат." },
            ["striker"] = new BomberCompDef { Name = "Пикировщик", Desc = "Каждые 2 с бросается на врага: 15 урона." },
        };
    }
}
