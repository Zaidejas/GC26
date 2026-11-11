using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    // Simulation state, in prototype pixels with y pointing DOWN (like the HTML canvas).
    // BomberView converts to Unity units (1 cell = 32 px = 1 unit, y up).

    public class BomberBody { public float X, Y, R, Vx, Vy; }

    public sealed class BomberPlayerState : BomberBody
    {
        public int Hp, MaxHp;
        public float MuzzleT, Aim, ITime, DashT, DashCd, Dx, Dy = -1f, FireCd, AltCd, TacCd, AbCd, AbT, Bob;
        public int Emp, Scrap;
        public bool Firing;
    }

    public sealed class BomberEnemy : BomberBody
    {
        public string Kind; public BomberEnemyDef Def;
        public float Hp, MaxHp, Tm, Face, AimA, LosT, ShootT, Flash, Stun, Slow, Alpha = 1f, SpdMul = 1f, Orbit = 1f;
        public bool Elite, Los; public string St = "idle"; public int Cnt, Wave;
    }

    public sealed class BomberShot
    {
        public float X, Y, Vx, Vy, Dmg, R, Life; public bool Enemy, Heavy, Dead; public Color32 Color;
    }

    public sealed class BomberPickup { public string Kind; public float X, Y; public int Value; public bool Vacuum; }

    public sealed class BomberEffect
    {
        public string Kind; public float X, Y, X2, Y2, R, T, Max; public Color32 Color; public string Spawn; public bool Elite;
        public float K => Max > 0f ? 1f - T / Max : 1f;
    }

    public sealed class BomberParticle { public float X, Y, Vx, Vy, Life, Max, Size; public Color32 Color; }
    public sealed class BomberText { public float X, Y, T; public string S; public Color32 Color; public bool Big; }

    public sealed class BomberWall { public float X, Y, W, H; public bool On = true, Cover, DoorOut, DoorIn; }
    public sealed class BomberDoor { public string Type; public float X0, X1; public bool Open; public BomberWall Blocker; }
    public sealed class BomberBarrel { public float X, Y, R = 10f, Hp = 10f; }

    public sealed class BomberRoomState
    {
        public string Type; public List<List<(string kind, bool elite)>> Waves = new List<List<(string, bool)>>();
        public int Wave = -1; public float WaveT, WaveHp, StartDelay = 0.6f; public bool Locked, Cleared; public List<string> Exits = new List<string>();
        public float Fade = 0.35f;
    }
}
