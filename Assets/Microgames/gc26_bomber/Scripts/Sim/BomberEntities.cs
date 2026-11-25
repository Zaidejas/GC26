using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    // Simulation state, in prototype pixels with y pointing DOWN (like the HTML canvas).
    // BomberView converts to Unity units (1 cell = 32 px = 1 unit, y up).

    public class BomberBody { public float X, Y, R, Vx, Vy; }

    public sealed class BomberWeapon
    {
        public string Id; public float Cd, AltCd, Grow, Spin;
        public readonly Dictionary<string, int> Up = new Dictionary<string, int> { ["dmg"] = 0, ["rate"] = 0, ["heat"] = 0, ["spec"] = 0 };
        public int UpSum => Up["dmg"] + Up["rate"] + Up["heat"] + Up["spec"];
        public BomberWeaponDef Def => BomberItems.Weapons[Id];
    }

    public sealed class BomberComp { public string Id; public float A, Cd, X, Y; public string St = "orbit"; public BomberEnemy Tgt; }

    public struct BomberBeam { public bool On, Zap; public float Sx, Sy, Ex, Ey; }

    public sealed class BomberPlayerState : BomberBody
    {
        public int Hp, MaxHp, BaseHp, AltarLoss;
        public float MuzzleT, Aim, ITime, DashT, DashCd, Dx, Dy = -1f, AltCd, TacCd, AbCd, AbT, Bob, Shield, ShieldT, TrailT;
        public int Emp, Scrap, Cur, ModSlots = 4, CompSlots = 1, Charges = 3, LeechK;
        public string Char = "spark", Tactic = "vent"; public float BubbleT; public float Speed = 192f, DmgK = 1f, CoolK = 1f, DashK = 1f, AbilityCd = 16f, ChargeT, VortT;
        public bool Firing;
        public readonly List<BomberWeapon> Weapons = new List<BomberWeapon>();
        public readonly List<string> Mods = new List<string>();
        public readonly List<BomberComp> Comps = new List<BomberComp>();
        public BomberBeam Beam;
        public BomberWeapon Weapon => Weapons[Cur];
    }

    public sealed class BomberEnemy : BomberBody
    {
        public string Kind; public BomberEnemyDef Def;
        public float Hp, MaxHp, Tm, Face, AimA, LosT, ShootT, Flash, Stun, Slow, Burn, BurnT, Alpha = 1f, SpdMul = 1f, Orbit = 1f;
        public bool Elite, Los; public string St = "idle"; public int Cnt, Wave;
        public string Affix; public float ShieldHp, SlamT; public BomberEnemy Tgt;
        // boss only
        public BomberBossDef Boss; public int Phase, Seq, Beams = 1, Fan; public string Act = "idle"; public float SummonT = 5f, Sdir = 1f, Emit;
        public readonly List<Vector2> Targets = new List<Vector2>();
        public bool IsBoss => Boss != null;
    }

    /// <summary>Floor hazard: hot (adds heat), lava / acid (hurts after a warning).</summary>
    public sealed class BomberZone { public string Kind; public float X, Y, W, H, Tele, Life; public bool Contains(float x, float y) => x >= X && x <= X + W && y >= Y && y <= Y + H; }

    public sealed class BomberShot
    {
        public float X, Y, Vx, Vy, Dmg, R, Life, Aoe, Slow, Kb = 60f; public int Pierce, Bounce;
        public bool Enemy, Heavy, Dead, Homing, Burn, Zap, Split, Trail; public string Look; public Color32 Color;
        public HashSet<BomberEnemy> Hit;
    }

    public sealed class BomberPickup { public string Kind, Id; public float X, Y; public int Value; public bool Vacuum; }

    /// <summary>Something the player can get: a weapon, a module, a companion or a repair kit.</summary>
    public struct BomberItem
    {
        public string Type, Id;
        public string Name => Type == "weapon" ? BomberItems.Weapons[Id].Name : Type == "module" ? BomberItems.Mods[Id].Name : Type == "companion" ? BomberItems.Comps[Id].Name : "Ремкомплект";
        public string Desc => Type == "weapon" ? BomberItems.Weapons[Id].Desc : Type == "module" ? BomberItems.Mods[Id].Desc : Type == "companion" ? BomberItems.Comps[Id].Desc : "+1 ячейка здоровья.";
        public string Tag => Type == "weapon" ? "Оружие" : Type == "module" ? "Модуль · " + BomberItems.Mods[Id].Cat : Type == "companion" ? "Компаньон" : "Расходник";
    }

    public sealed class BomberProp { public string Kind, Id; public float X, Y; public BomberItem Item; public int Price; public bool Opened, Sold; }

    public sealed class BomberReward { public string Title, Sub; public readonly List<BomberItem> Options = new List<BomberItem>(); }

    public sealed class BomberEffect
    {
        public string Kind; public float X, Y, X2, Y2, R, T, Max; public Color32 Color; public string Spawn; public bool Elite;
        public float K => Max > 0f ? 1f - T / Max : 1f;
    }

    public sealed class BomberParticle { public float X, Y, Vx, Vy, Life, Max, Size; public Color32 Color; }
    public sealed class BomberText { public float X, Y, T; public string S; public Color32 Color; public bool Big; }
    public sealed class BomberBolt { public List<Vector2> Pts = new List<Vector2>(); public float T; }
    public sealed class BomberTrail { public float X, Y, T; public readonly HashSet<BomberEnemy> Hit = new HashSet<BomberEnemy>(); }

    public sealed class BomberWall { public float X, Y, W, H; public bool On = true, Cover, DoorOut, DoorIn; }
    public sealed class BomberDoor { public string Type; public float X0, X1; public bool Open; public BomberWall Blocker; }
    public sealed class BomberBarrel { public float X, Y, R = 10f, Hp = 10f; }

    public sealed class BomberRoomState
    {
        public string Type; public List<List<(string kind, bool elite)>> Waves = new List<List<(string, bool)>>();
        public int Wave = -1; public float WaveT, WaveHp, StartDelay = 0.6f; public bool Locked, Cleared, BenchFree; public List<string> Exits = new List<string>();
        public float Fade = 0.35f;
    }
}
