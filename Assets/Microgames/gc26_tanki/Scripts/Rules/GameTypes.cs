namespace GC26.Tanki.Rules
{
    public enum Tile : byte { Empty, Crate, Concrete, Net, Water, Oil, Base }

    public enum Dir { Up, Right, Down, Left }

    public enum EnemyKind { Raider, Scout, Gunner, Heavy, Boss }

    public enum BonusKind { Star, Bomb, Shield, Fort, Freeze, Life }

    public enum GamePhase { Playing, LevelCleared, GameOver, Victory, WaveCleared }

    public enum GameMode { Campaign, Survival }

    /// <summary>The four things the player can improve in the workshop, and in survival after each wave.</summary>
    public enum Stat { Armor, Gun, FireRate, Speed }

    /// <summary>What survival offers after a wave: one of the stats, or one of these extras.</summary>
    public enum RunUpgrade { Armor, Gun, FireRate, Speed, Repair, SuperCharge, Fort }

    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>What a smart enemy goes after: the base, the player, or the base straight through crates.</summary>
    public enum EnemyRole { Rusher, Hunter, Breaker }

    public struct DifficultySettings
    {
        public float SpeedScale;
        public float FireIntervalScale;
        public float SpawnDelay;
        public int MaxEnemies;
        public float SmartBonus;

        public static DifficultySettings For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return new DifficultySettings { SpeedScale = 0.9f, FireIntervalScale = 1.25f, SpawnDelay = 2.8f, MaxEnemies = 3, SmartBonus = -0.3f };
                case Difficulty.Hard:
                    return new DifficultySettings { SpeedScale = 1.1f, FireIntervalScale = 0.8f, SpawnDelay = 1.7f, MaxEnemies = 5, SmartBonus = 0.3f };
                default:
                    return new DifficultySettings { SpeedScale = 1f, FireIntervalScale = 1f, SpawnDelay = 2.2f, MaxEnemies = 4, SmartBonus = 0f };
            }
        }
    }

    public enum GameEventKind
    {
        Shot,
        TileChanged,
        BulletSpark,
        TankHit,
        TankDestroyed,
        PlayerDestroyed,
        BaseDestroyed,
        BonusSpawned,
        BonusTaken,
        BombBlast,
        PlayerHit,
        SuperStrike,
        SuperReady,
        BossSpawned
    }

    public struct GameEvent
    {
        public GameEventKind Kind;
        public float X;
        public float Y;
        public bool IsPlayer;
        public BonusKind Bonus;
        public Dir Dir;

        public GameEvent(GameEventKind kind, float x, float y, bool isPlayer = false, BonusKind bonus = BonusKind.Star, Dir dir = Dir.Up)
        {
            Kind = kind;
            X = x;
            Y = y;
            IsPlayer = isPlayer;
            Bonus = bonus;
            Dir = dir;
        }
    }

    public struct PlayerInput
    {
        public Dir? Move;
        public bool Fire;
        public bool Super;
    }

    /// <summary>Upgrade levels of the player's tank, from 0. The workshop goes up to 3, survival up to 5.</summary>
    public sealed class PlayerStats
    {
        public readonly int[] Levels = new int[4];

        public int this[Stat stat]
        {
            get => Levels[(int)stat];
            set => Levels[(int)stat] = value;
        }

        public PlayerStats Copy()
        {
            var copy = new PlayerStats();
            Levels.CopyTo(copy.Levels, 0);
            return copy;
        }
    }

    public sealed class Tank
    {
        public int Id;
        public bool IsPlayer;
        public EnemyKind Kind;
        public float X;
        public float Y;
        public Dir Dir;
        public float Speed;
        public float BulletSpeed;
        public int Hp;
        public bool Alive = true;
        public bool Moving;
        public bool Carrier;
        public float SpawnTimer;
        public float ShieldTimer;
        public float Cooldown;
        public float SlideTimer;
        public Dir? Want;
        public float ThinkTimer;
        public float FireTimer;
        public EnemyRole Role;
        public float HoldTimer;
        public float LineTimer;
        public int MaxHp = 1;
        public float HurtTimer;

        public bool Spawning => SpawnTimer > 0f;
        public bool Shielded => ShieldTimer > 0f;
    }

    public sealed class Bullet
    {
        public int OwnerId;
        public bool FromPlayer;
        public bool Pierce;
        public int Damage = 1;
        public float X;
        public float Y;
        public Dir Dir;
        public float Speed;
        public bool Alive = true;
    }

    public sealed class Bonus
    {
        public BonusKind Kind;
        public int X;
        public int Y;
        public float Timer;
    }

    /// <summary>All the numbers worth tuning live here.</summary>
    public static class Tuning
    {
        public const float TankHalf = 0.42f;
        public const float TurnWindow = 0.3f;

        public const int StartLives = 3;
        public const int MaxUpgrade = 3;
        public const int CarrierEvery = 4;

        // How often enemies act smart on each level, before the difficulty's bonus.
        public static readonly float[] LevelSmartness =
        {
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.5f, 0.55f, 0.6f, 0.65f, 0.7f,
            0.7f, 0.75f, 0.8f, 0.8f, 0.85f, 0.85f, 0.9f, 0.9f, 0.95f, 0.95f
        };
        public const int CrateRouteCost = 4;
        public const float LineCheckInterval = 0.3f;

        // ---- The player's tank by upgrade level: 0 to 3 from the workshop, up to 5 in survival ----
        public const int WorkshopMaxLevel = 3;
        public const int SurvivalMaxLevel = 5;
        public static readonly int[] WorkshopCost = { 20, 45, 80 };

        public const int BaseHp = 3;
        public static readonly float[] GunBulletSpeed = { 9f, 10.5f, 12f, 12.5f, 13.5f, 14.5f };
        public static readonly int[] GunDamage = { 1, 1, 1, 2, 2, 3 };
        public static readonly float[] FireCooldown = { 0.32f, 0.27f, 0.23f, 0.19f, 0.16f, 0.13f };
        public static readonly int[] FireBullets = { 1, 1, 2, 2, 3, 3 };
        public static readonly float[] DriveSpeed = { 3.2f, 3.45f, 3.7f, 3.95f, 4.2f, 4.45f };
        public const float HurtTime = 0.6f;

        // The temporary gun from the star bonus: faster shots, then a second shot, then shots through concrete.
        public const float StarTime = 20f;
        public const float StarBulletBoost = 3f;

        // The reactor strike: charged by kills, more of them on later levels.
        public const int SurvivalSuperNeed = 6;
        public const int SuperDamage = 4;
        public const float SuperHalfWidth = 0.8f;
        public static int SuperNeed(int levelIndex) => 4 + levelIndex / 3;

        // Parts, the workshop's currency.
        public static int LevelParts(int levelIndex) => 5 + levelIndex;
        public static int WaveParts(int wave) => 3 + wave;

        // The boss on every fifth level and every fifth survival wave.
        public const int BossEvery = 5;
        public const float BossSpeed = 1.3f;
        public const float BossBulletSpeed = 8f;
        public const int BossBombDamage = 5;
        public static int BossHp(int bossNumber) => 16 + bossNumber * 4;

        public const float SpawnFlash = 0.8f;
        public const float RespawnDelay = 1.2f;
        public const float RespawnShield = 2.5f;
        public const float ShieldBonusTime = 8f;
        public const float FreezeTime = 7f;
        public const float FortTime = 12f;
        public const float BonusLifetime = 12f;
        public const float OilSlide = 0.4f;
        public const float ClearDelay = 1.2f;

        public const int CrateHp = 2;
        public const int BonusScore = 500;

        public static float EnemySpeed(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Scout: return 3.2f;
                case EnemyKind.Heavy: return 1.4f;
                case EnemyKind.Boss: return BossSpeed;
                default: return 1.9f;
            }
        }

        public static float EnemyBulletSpeed(EnemyKind kind)
        {
            if (kind == EnemyKind.Boss) return BossBulletSpeed;
            return kind == EnemyKind.Gunner ? 10f : 6.5f;
        }

        public static int EnemyHp(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy ? 3 : 1;
        }

        public static int EnemyScore(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Scout: return 200;
                case EnemyKind.Gunner: return 300;
                case EnemyKind.Heavy: return 400;
                case EnemyKind.Boss: return 3000;
                default: return 100;
            }
        }

        public static int EnemyParts(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Scout: return 2;
                case EnemyKind.Gunner: return 2;
                case EnemyKind.Heavy: return 3;
                case EnemyKind.Boss: return 20;
                default: return 1;
            }
        }
    }
}
