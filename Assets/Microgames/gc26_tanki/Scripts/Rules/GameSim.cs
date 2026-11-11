using System;
using System.Collections.Generic;

namespace GC26.Tanki.Rules
{
    /// <summary>
    /// The whole game with no Unity dependency. The view calls Tick with a fixed step,
    /// reads the public state and drains Events. Coordinates are in cells: cell (x, y)
    /// has its centre at (x + 0.5, y + 0.5) and y grows towards the bottom of the map.
    /// </summary>
    public sealed class GameSim
    {
        public const float FixedStep = 1f / 60f;

        public readonly Tile[,] Tiles = new Tile[Levels.Width, Levels.Height];
        public readonly List<Tank> Enemies = new List<Tank>();
        public readonly List<Bullet> Bullets = new List<Bullet>();
        public readonly List<GameEvent> Events = new List<GameEvent>();

        public Tank Player { get; private set; }
        public Bonus Bonus { get; private set; }
        public GamePhase Phase { get; private set; }
        public int LevelIndex { get; private set; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public GameMode Mode { get; private set; }
        public PlayerStats Stats { get; private set; } = new PlayerStats();

        /// <summary>The star bonus's temporary gun level, 0 to 3, gone when its timer runs out or the tank is lost.</summary>
        public int StarLevel { get; private set; }
        public float StarTimer { get; private set; }

        /// <summary>Kills towards the reactor strike, and how many it needs.</summary>
        public int SuperCharge { get; private set; }
        public int SuperNeed { get; private set; }
        public bool SuperReady => SuperCharge >= SuperNeed;

        /// <summary>Parts earned since the game last collected them with TakeParts.</summary>
        public int PartsEarned { get; private set; }

        /// <summary>The survival wave being fought, from 1, and the upgrades offered after it.</summary>
        public int Wave { get; private set; }
        public readonly List<RunUpgrade> Choices = new List<RunUpgrade>();

        public int MaxHp => Tuning.BaseHp + Stats[Stat.Armor];
        public bool IsBossLevel => Mode == GameMode.Campaign ? waves.IndexOf('X') >= 0 : Wave % Tuning.BossEvery == 0;
        public Tank Boss => Enemies.Find(e => e.Kind == EnemyKind.Boss);
        public string LevelName => Mode == GameMode.Survival ? "Выживание, волна " + Wave : (LevelIndex + 1) + ". " + Levels.All[LevelIndex].Name;
        public int BaseX { get; private set; }
        public int BaseY { get; private set; }
        public float FreezeTimer { get; private set; }
        public float FortTimer { get; private set; }
        public int EnemiesLeft => waveCursor < waves.Length ? waves.Length - waveCursor + Enemies.Count : Enemies.Count;
        public Difficulty Difficulty { get; private set; } = Difficulty.Normal;
        public DifficultySettings Settings { get; private set; } = DifficultySettings.For(Difficulty.Normal);

        /// <summary>The chance, from 0 to 1, that an enemy's decision is a smart one.</summary>
        public float Smartness { get; set; }

        readonly int[,] crateHp = new int[Levels.Width, Levels.Height];
        readonly List<(int x, int y)> enemySpawns = new List<(int x, int y)>();
        readonly List<(int x, int y)> fortCells = new List<(int x, int y)>();
        readonly Random rng;

        // Route maps: each cell holds the cost of getting from there to the target.
        readonly int[,] baseRoute = new int[Levels.Width, Levels.Height];
        readonly int[,] breakerRoute = new int[Levels.Width, Levels.Height];
        readonly int[,] playerRoute = new int[Levels.Width, Levels.Height];
        readonly Queue<(int x, int y)> routeQueue = new Queue<(int x, int y)>();
        bool baseRoutesStale = true;
        bool playerRouteStale = true;
        int playerRouteX;
        int playerRouteY;

        string waves = "";
        int waveCursor;
        int spawnerIndex;
        int nextId = 1;
        float spawnTimer;
        float respawnTimer;
        float clearTimer;
        int playerSpawnX;
        int playerSpawnY;
        int superNeedCut;
        int bossesSpawned;

        public GameSim(int seed = 1)
        {
            rng = new Random(seed);
            Lives = Tuning.StartLives;
            LoadLevel(0);
        }

        public void NewGame()
        {
            StartCampaign(0, Stats);
        }

        /// <summary>A campaign run from the chosen level, with the workshop's upgrades.</summary>
        public void StartCampaign(int levelIndex, PlayerStats stats)
        {
            Mode = GameMode.Campaign;
            Stats = stats.Copy();
            Score = 0;
            Lives = Tuning.StartLives;
            PartsEarned = 0;
            LoadLevel(levelIndex);
        }

        public void NextLevel()
        {
            if (LevelIndex + 1 < Levels.All.Length) LoadLevel(LevelIndex + 1);
        }

        /// <summary>Endless waves on the arena, with a choice of upgrade after each one.</summary>
        public void StartSurvival(PlayerStats stats)
        {
            Mode = GameMode.Survival;
            Stats = stats.Copy();
            Score = 0;
            Lives = Tuning.StartLives;
            PartsEarned = 0;
            superNeedCut = 0;
            Wave = 1;
            LevelIndex = 0;
            Smartness = SurvivalSmartness(Wave);
            LoadMap(Levels.Survival.Rows, SurvivalWave(Wave));
            SuperNeed = Tuning.SurvivalSuperNeed;
        }

        /// <summary>Hands over the parts earned so far, so the game can store them.</summary>
        public int TakeParts()
        {
            int parts = PartsEarned;
            PartsEarned = 0;
            return parts;
        }

        float SurvivalSmartness(int wave)
        {
            return Math.Max(0f, Math.Min(1f, 0.2f + 0.05f * wave + Settings.SmartBonus));
        }

        /// <summary>The enemies of a survival wave: more and tougher each time, a boss every fifth wave.</summary>
        public string SurvivalWave(int wave)
        {
            var list = new System.Text.StringBuilder();
            int count = 5 + wave;
            float heavy = Math.Min(0.4f, 0.05f * wave);
            for (int i = 0; i < count; i++)
            {
                double roll = rng.NextDouble();
                if (roll < heavy) list.Append('H');
                else if (roll < heavy + 0.25) list.Append('G');
                else if (roll < heavy + 0.5) list.Append('S');
                else list.Append('R');
            }
            if (wave % Tuning.BossEvery == 0) list.Append('X');
            return list.ToString();
        }

        /// <summary>Applies the upgrade picked after a survival wave and starts the next wave.</summary>
        public void ChooseUpgrade(RunUpgrade upgrade)
        {
            if (Phase != GamePhase.WaveCleared) return;
            switch (upgrade)
            {
                case RunUpgrade.Armor:
                    Stats[Stat.Armor] = Math.Min(Tuning.SurvivalMaxLevel, Stats[Stat.Armor] + 1);
                    if (Player.Alive)
                    {
                        Player.MaxHp = MaxHp;
                        Player.Hp = Math.Min(Player.MaxHp, Player.Hp + 1);
                    }
                    break;
                case RunUpgrade.Gun:
                case RunUpgrade.FireRate:
                case RunUpgrade.Speed:
                    var stat = (Stat)(int)upgrade;
                    Stats[stat] = Math.Min(Tuning.SurvivalMaxLevel, Stats[stat] + 1);
                    if (Player.Alive) Player.Speed = Tuning.DriveSpeed[Stats[Stat.Speed]];
                    break;
                case RunUpgrade.Repair:
                    Lives++;
                    if (Player.Alive) Player.Hp = Player.MaxHp;
                    break;
                case RunUpgrade.SuperCharge:
                    superNeedCut = Math.Min(superNeedCut + 1, Tuning.SurvivalSuperNeed - 3);
                    SuperCharge += 2;
                    break;
                case RunUpgrade.Fort:
                    StartFort(30f);
                    break;
            }

            SuperNeed = Tuning.SurvivalSuperNeed - superNeedCut;
            Wave++;
            Smartness = SurvivalSmartness(Wave);
            waves = SurvivalWave(Wave);
            waveCursor = 0;
            spawnTimer = 1.5f;
            clearTimer = 0f;
            bossesSpawned = 0;
            Choices.Clear();
            Phase = GamePhase.Playing;
        }

        void OfferChoices()
        {
            Choices.Clear();
            var pool = new List<RunUpgrade>();
            foreach (RunUpgrade upgrade in Enum.GetValues(typeof(RunUpgrade)))
            {
                bool isStat = (int)upgrade < 4;
                if (isStat && Stats[(Stat)(int)upgrade] >= Tuning.SurvivalMaxLevel) continue;
                if (upgrade == RunUpgrade.SuperCharge && superNeedCut >= Tuning.SurvivalSuperNeed - 3) continue;
                pool.Add(upgrade);
            }
            while (Choices.Count < 3 && pool.Count > 0)
            {
                int pick = rng.Next(pool.Count);
                Choices.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        public void SetDifficulty(Difficulty difficulty)
        {
            Difficulty = difficulty;
            Settings = DifficultySettings.For(difficulty);
        }

        public void LoadLevel(int index)
        {
            LevelIndex = index;
            Smartness = Math.Max(0f, Math.Min(1f, Tuning.LevelSmartness[index] + Settings.SmartBonus));
            LoadMap(Levels.All[index].Rows, Levels.All[index].Waves);
            SuperNeed = Tuning.SuperNeed(index);
        }

        /// <summary>Loads any map. Public so tests can use small hand-made situations.</summary>
        public void LoadMap(string[] rows, string waveList)
        {
            enemySpawns.Clear();
            fortCells.Clear();
            Enemies.Clear();
            Bullets.Clear();
            Events.Clear();
            Bonus = null;
            Phase = GamePhase.Playing;
            FreezeTimer = 0f;
            FortTimer = 0f;
            waves = waveList;
            waveCursor = 0;
            spawnerIndex = 0;
            spawnTimer = 0.5f;
            clearTimer = 0f;
            respawnTimer = 0f;
            StarLevel = 0;
            StarTimer = 0f;
            SuperCharge = 0;
            SuperNeed = Tuning.SuperNeed(LevelIndex);
            bossesSpawned = 0;
            Choices.Clear();
            baseRoutesStale = true;
            playerRouteStale = true;

            for (int y = 0; y < Levels.Height; y++)
            {
                for (int x = 0; x < Levels.Width; x++)
                {
                    char c = rows[y][x];
                    Tile tile = Tile.Empty;
                    switch (c)
                    {
                        case '#': tile = Tile.Crate; break;
                        case '@': tile = Tile.Concrete; break;
                        case '%': tile = Tile.Net; break;
                        case '~': tile = Tile.Water; break;
                        case '=': tile = Tile.Oil; break;
                        case 'B': tile = Tile.Base; BaseX = x; BaseY = y; break;
                        case 'P': playerSpawnX = x; playerSpawnY = y; break;
                        case 'E': enemySpawns.Add((x, y)); break;
                    }
                    Tiles[x, y] = tile;
                    crateHp[x, y] = tile == Tile.Crate ? Tuning.CrateHp : 0;
                }
            }
            SpawnPlayer();
        }

        public int CrateHp(int x, int y) => crateHp[x, y];

        public static bool IsSolid(Tile tile)
        {
            return tile == Tile.Crate || tile == Tile.Concrete || tile == Tile.Water || tile == Tile.Base;
        }

        public void Tick(float dt, PlayerInput input)
        {
            if (Phase != GamePhase.Playing) return;

            FreezeTimer = Math.Max(0f, FreezeTimer - dt);
            UpdateStar(dt);
            UpdateFort(dt);
            UpdatePlayer(dt, input);
            foreach (Tank enemy in Enemies) UpdateEnemy(enemy, dt);
            UpdateBullets(dt);
            UpdateSpawner(dt);
            UpdateBonus(dt);
            CheckLevelEnd(dt);
        }

        // ---- Player ----

        void SpawnPlayer()
        {
            Player = new Tank
            {
                Id = nextId++,
                IsPlayer = true,
                X = playerSpawnX + 0.5f,
                Y = playerSpawnY + 0.5f,
                Dir = Dir.Up,
                Speed = Tuning.DriveSpeed[Stats[Stat.Speed]],
                Hp = MaxHp,
                MaxHp = MaxHp,
                ShieldTimer = Tuning.RespawnShield
            };
        }

        // What the player's gun does now: the workshop's level plus the star's temporary one.
        public float PlayerBulletSpeed => Tuning.GunBulletSpeed[Stats[Stat.Gun]] + (StarLevel >= 1 ? Tuning.StarBulletBoost : 0f);
        public int PlayerMaxBullets => Tuning.FireBullets[Stats[Stat.FireRate]] + (StarLevel >= 2 ? 1 : 0);
        public bool PlayerPierces => StarLevel >= 3;

        void UpdatePlayer(float dt, PlayerInput input)
        {
            if (!Player.Alive)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f && !AnyTankAt(playerSpawnX + 0.5f, playerSpawnY + 0.5f)) SpawnPlayer();
                return;
            }

            Player.ShieldTimer = Math.Max(0f, Player.ShieldTimer - dt);
            Player.HurtTimer = Math.Max(0f, Player.HurtTimer - dt);
            Player.Cooldown = Math.Max(0f, Player.Cooldown - dt);
            Drive(Player, input.Move, dt);

            if (input.Super && SuperReady) SuperStrike();

            if (input.Fire && Player.Cooldown <= 0f && BulletsOf(Player) < PlayerMaxBullets)
            {
                Fire(Player, PlayerBulletSpeed, PlayerPierces, Tuning.GunDamage[Stats[Stat.Gun]]);
                Player.Cooldown = Tuning.FireCooldown[Stats[Stat.FireRate]];
            }
        }

        void HurtPlayer()
        {
            if (Player.Shielded || Player.HurtTimer > 0f)
            {
                Events.Add(new GameEvent(GameEventKind.BulletSpark, Player.X, Player.Y));
                return;
            }
            Player.Hp--;
            if (Player.Hp <= 0)
            {
                KillPlayer();
                return;
            }
            Player.HurtTimer = Tuning.HurtTime;
            Events.Add(new GameEvent(GameEventKind.PlayerHit, Player.X, Player.Y, true));
        }

        void KillPlayer()
        {
            Player.Alive = false;
            Player.Hp = 0;
            Events.Add(new GameEvent(GameEventKind.PlayerDestroyed, Player.X, Player.Y, true));
            Lives--;
            StarLevel = 0;
            StarTimer = 0f;
            respawnTimer = Tuning.RespawnDelay;
            if (Lives <= 0) Phase = GamePhase.GameOver;
        }

        void UpdateStar(float dt)
        {
            if (StarTimer <= 0f) return;
            StarTimer -= dt;
            if (StarTimer <= 0f) StarLevel = 0;
        }

        // ---- Reactor strike ----

        /// <summary>A wide beam along the player's facing: hurts every enemy in it and clears crates on its line.</summary>
        void SuperStrike()
        {
            SuperCharge = 0;
            Dir dir = Player.Dir;
            bool horizontal = dir == Dir.Left || dir == Dir.Right;
            Events.Add(new GameEvent(GameEventKind.SuperStrike, Player.X, Player.Y, true, BonusKind.Star, dir));

            foreach (Tank enemy in Enemies)
            {
                if (!enemy.Alive || enemy.Spawning) continue;
                if (InBeam(enemy.X, enemy.Y, dir, horizontal)) DamageEnemy(enemy, Tuning.SuperDamage, false);
            }
            foreach (Bullet bullet in Bullets)
                if (!bullet.FromPlayer && InBeam(bullet.X, bullet.Y, dir, horizontal)) bullet.Alive = false;

            int x = (int)Math.Floor(Player.X);
            int y = (int)Math.Floor(Player.Y);
            while (true)
            {
                StepCell(dir, ref x, ref y);
                if (!InBounds(x, y)) break;
                if (Tiles[x, y] != Tile.Crate) continue;
                Tiles[x, y] = Tile.Empty;
                crateHp[x, y] = 0;
                baseRoutesStale = true;
                Events.Add(new GameEvent(GameEventKind.TileChanged, x, y));
            }

            Bullets.RemoveAll(b => !b.Alive);
            Enemies.RemoveAll(e => !e.Alive);
        }

        bool InBeam(float x, float y, Dir dir, bool horizontal)
        {
            float across = horizontal ? Math.Abs(y - Player.Y) : Math.Abs(x - Player.X);
            float along = horizontal ? (x - Player.X) * DirX(dir) : (y - Player.Y) * DirY(dir);
            return across <= Tuning.SuperHalfWidth && along > 0f;
        }

        /// <summary>Fills the strike's charge. For checks and tests; in play only kills charge it.</summary>
        public void FillSuper()
        {
            while (!SuperReady) AddSuperCharge();
        }

        /// <summary>Fires the strike now if it is charged, as the button would.</summary>
        public void UseSuper()
        {
            if (Phase == GamePhase.Playing && Player.Alive && SuperReady) SuperStrike();
        }

        void AddSuperCharge()
        {
            if (SuperCharge >= SuperNeed) return;
            SuperCharge++;
            if (SuperCharge == SuperNeed) Events.Add(new GameEvent(GameEventKind.SuperReady, Player.X, Player.Y, true));
        }

        // ---- Movement, shared by the player and the enemies ----

        void Drive(Tank tank, Dir? want, float dt)
        {
            if (want == null)
            {
                bool onOil = TileAt(tank.X, tank.Y) == Tile.Oil;
                if (tank.Moving && onOil && tank.SlideTimer > 0f)
                {
                    tank.SlideTimer -= dt;
                    tank.Moving = MoveForward(tank, tank.Speed * dt);
                }
                else
                {
                    tank.Moving = false;
                }
                return;
            }

            tank.SlideTimer = Tuning.OilSlide;
            Dir wanted = want.Value;

            if (wanted != tank.Dir)
            {
                bool perpendicular = (((int)wanted + (int)tank.Dir) & 1) == 1;
                if (!perpendicular)
                {
                    tank.Dir = wanted;
                }
                else if (ShouldTurnNow(tank, wanted))
                {
                    if (TrySnapToCentre(tank)) tank.Dir = wanted;
                }
            }

            tank.Moving = MoveForward(tank, tank.Speed * dt);
        }

        // Keeps a moving tank going until it reaches the junction the turn leads into,
        // so turns do not need frame-perfect timing on a touch screen.
        bool ShouldTurnNow(Tank tank, Dir wanted)
        {
            bool horizontal = tank.Dir == Dir.Left || tank.Dir == Dir.Right;
            float along = horizontal ? tank.X : tank.Y;
            float centre = (float)Math.Floor(along) + 0.5f;
            float offset = Math.Abs(along - centre);

            int cellX = (int)Math.Floor(tank.X);
            int cellY = (int)Math.Floor(tank.Y);
            StepCell(wanted, ref cellX, ref cellY);
            bool wantedOpen = InBounds(cellX, cellY) && !IsSolid(Tiles[cellX, cellY]);

            if (!tank.Moving) return true;
            if (wantedOpen && offset <= Tuning.TurnWindow) return true;

            float probeX = tank.X + DirX(tank.Dir) * 0.05f;
            float probeY = tank.Y + DirY(tank.Dir) * 0.05f;
            return !CanOccupy(tank, probeX, probeY);
        }

        bool TrySnapToCentre(Tank tank)
        {
            float x = tank.X;
            float y = tank.Y;
            if (tank.Dir == Dir.Left || tank.Dir == Dir.Right) x = (float)Math.Floor(x) + 0.5f;
            else y = (float)Math.Floor(y) + 0.5f;

            if (!CanOccupy(tank, x, y)) return false;
            tank.X = x;
            tank.Y = y;
            return true;
        }

        bool MoveForward(Tank tank, float step)
        {
            float dx = DirX(tank.Dir);
            float dy = DirY(tank.Dir);
            for (int i = 0; i < 5; i++)
            {
                float x = tank.X + dx * step;
                float y = tank.Y + dy * step;
                if (CanOccupy(tank, x, y))
                {
                    tank.X = x;
                    tank.Y = y;
                    return true;
                }
                step *= 0.5f;
            }
            return false;
        }

        bool CanOccupy(Tank tank, float x, float y)
        {
            const float h = Tuning.TankHalf;
            int minX = (int)Math.Floor(x - h);
            int maxX = (int)Math.Floor(x + h);
            int minY = (int)Math.Floor(y - h);
            int maxY = (int)Math.Floor(y + h);
            if (minX < 0 || minY < 0 || maxX >= Levels.Width || maxY >= Levels.Height) return false;

            for (int cy = minY; cy <= maxY; cy++)
                for (int cx = minX; cx <= maxX; cx++)
                    if (IsSolid(Tiles[cx, cy])) return false;

            if (tank != Player && Player.Alive && BlocksTank(tank, Player, x, y)) return false;
            foreach (Tank other in Enemies)
                if (other != tank && BlocksTank(tank, other, x, y)) return false;
            return true;
        }

        // A tank that already overlaps another may still move away from it, never closer.
        static bool BlocksTank(Tank tank, Tank other, float x, float y)
        {
            const float size = Tuning.TankHalf * 2f;
            if (Math.Abs(other.X - x) >= size || Math.Abs(other.Y - y) >= size) return false;
            float before = Sq(other.X - tank.X) + Sq(other.Y - tank.Y);
            float after = Sq(other.X - x) + Sq(other.Y - y);
            return after <= before;
        }

        bool AnyTankAt(float x, float y)
        {
            const float size = Tuning.TankHalf * 2f;
            if (Player.Alive && Math.Abs(Player.X - x) < size && Math.Abs(Player.Y - y) < size) return true;
            foreach (Tank enemy in Enemies)
                if (Math.Abs(enemy.X - x) < size && Math.Abs(enemy.Y - y) < size) return true;
            return false;
        }

        // ---- Enemies ----

        void UpdateSpawner(float dt)
        {
            if (waveCursor >= waves.Length || enemySpawns.Count == 0) return;
            spawnTimer -= dt;
            if (spawnTimer > 0f || Enemies.Count >= Settings.MaxEnemies) return;

            EnemyKind kind = ParseKind(waves[waveCursor]);
            // The boss waits for the field to clear a little, and always comes from the middle gate.
            if (kind == EnemyKind.Boss && Enemies.Count > 1) return;

            for (int i = 0; i < enemySpawns.Count; i++)
            {
                int index = kind == EnemyKind.Boss ? (enemySpawns.Count / 2 + i) % enemySpawns.Count : (spawnerIndex + i) % enemySpawns.Count;
                var spawn = enemySpawns[index];
                float x = spawn.x + 0.5f;
                float y = spawn.y + 0.5f;
                if (AnyTankAt(x, y)) continue;

                SpawnEnemy(kind, x, y);
                waveCursor++;
                spawnerIndex = (spawnerIndex + i + 1) % enemySpawns.Count;
                spawnTimer = Settings.SpawnDelay;
                return;
            }
        }

        void SpawnEnemy(EnemyKind kind, float x, float y)
        {
            if (kind == EnemyKind.Boss)
            {
                bossesSpawned++;
                int number = Mode == GameMode.Survival ? Wave / Tuning.BossEvery : LevelIndex / Tuning.BossEvery + 1;
                int hp = Tuning.BossHp(number);
                Enemies.Add(new Tank
                {
                    Id = nextId++,
                    Kind = kind,
                    X = x,
                    Y = y,
                    Dir = Dir.Down,
                    Speed = Tuning.EnemySpeed(kind) * Settings.SpeedScale,
                    BulletSpeed = Tuning.EnemyBulletSpeed(kind),
                    Role = EnemyRole.Breaker,
                    Hp = hp,
                    MaxHp = hp,
                    Carrier = true,
                    SpawnTimer = Tuning.SpawnFlash * 2f,
                    Want = Dir.Down,
                    ThinkTimer = NextFloat(0.3f, 1f),
                    FireTimer = 1.5f
                });
                Events.Add(new GameEvent(GameEventKind.BossSpawned, x, y));
                return;
            }

            Enemies.Add(new Tank
            {
                Id = nextId++,
                Kind = kind,
                X = x,
                Y = y,
                Dir = Dir.Down,
                Speed = Tuning.EnemySpeed(kind) * Settings.SpeedScale,
                BulletSpeed = Tuning.EnemyBulletSpeed(kind),
                Role = RoleFor(kind),
                Hp = Tuning.EnemyHp(kind),
                Carrier = (waveCursor + 1) % Tuning.CarrierEvery == 0,
                SpawnTimer = Tuning.SpawnFlash,
                Want = Dir.Down,
                ThinkTimer = NextFloat(0.3f, 1f),
                FireTimer = NextFloat(0.6f, 1.6f)
            });
        }

        EnemyRole RoleFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Scout: return EnemyRole.Rusher;
                case EnemyKind.Gunner: return EnemyRole.Hunter;
                case EnemyKind.Heavy: return EnemyRole.Breaker;
                default: return waveCursor % 2 == 0 ? EnemyRole.Hunter : EnemyRole.Rusher;
            }
        }

        static EnemyKind ParseKind(char c)
        {
            switch (c)
            {
                case 'S': return EnemyKind.Scout;
                case 'G': return EnemyKind.Gunner;
                case 'H': return EnemyKind.Heavy;
                case 'X': return EnemyKind.Boss;
                default: return EnemyKind.Raider;
            }
        }

        void UpdateEnemy(Tank enemy, float dt)
        {
            if (enemy.Spawning)
            {
                enemy.SpawnTimer -= dt;
                return;
            }
            if (FreezeTimer > 0f)
            {
                enemy.Moving = false;
                return;
            }

            enemy.ThinkTimer -= dt;
            enemy.LineTimer -= dt;
            enemy.HoldTimer = Math.Max(0f, enemy.HoldTimer - dt);
            bool holding = enemy.HoldTimer > 0f;
            bool blocked = !enemy.Moving && !holding;
            bool atCentre = NearCentre(enemy);

            if (Smartness > 0f && enemy.LineTimer <= 0f && atCentre)
            {
                enemy.LineTimer = Tuning.LineCheckInterval;
                if (rng.NextDouble() < Smartness) TakeLineShot(enemy);
                holding = enemy.HoldTimer > 0f;
            }

            if (!holding && ((blocked && enemy.ThinkTimer <= 0.8f) || (enemy.ThinkTimer <= 0f && atCentre)))
            {
                enemy.Want = ChooseDirection(enemy, blocked);
                // Smart tanks rethink more often, so they react to a changing map.
                enemy.ThinkTimer = NextFloat(0.9f, 2.2f) * (1f - 0.5f * Smartness);
            }

            // A tank in ambush only turns; once it faces its target it stays put and shoots.
            Drive(enemy, holding && enemy.Dir == enemy.Want ? null : enemy.Want, dt);

            bool lined = HasTargetInLine(enemy) || (Smartness > 0f && CrateAhead(enemy));
            enemy.FireTimer -= lined ? dt * 3f : dt;
            if (enemy.Kind == EnemyKind.Boss)
            {
                // The boss fires forward and to both sides at once.
                if (enemy.FireTimer <= 0f && BulletsOf(enemy) == 0)
                {
                    Dir facing = enemy.Dir;
                    Fire(enemy, enemy.BulletSpeed, false);
                    enemy.Dir = (Dir)(((int)facing + 1) % 4);
                    Fire(enemy, enemy.BulletSpeed, false);
                    enemy.Dir = (Dir)(((int)facing + 3) % 4);
                    Fire(enemy, enemy.BulletSpeed, false);
                    enemy.Dir = facing;
                    enemy.FireTimer = NextFloat(1.1f, 1.6f) * Settings.FireIntervalScale;
                }
                return;
            }

            if (enemy.FireTimer <= 0f && BulletsOf(enemy) == 0)
            {
                Fire(enemy, enemy.BulletSpeed, false);
                float rate = enemy.Kind == EnemyKind.Gunner ? 0.7f : 1f;
                enemy.FireTimer = NextFloat(0.8f, 1.9f) * rate * Settings.FireIntervalScale;
            }
        }

        // ---- Smart behaviour ----

        // If the base or the player can be hit in a straight line, faces them and fires soon.
        void TakeLineShot(Tank enemy)
        {
            Dir? line = FindLineShot(enemy);
            if (line == null) return;

            enemy.Want = line;
            enemy.FireTimer = Math.Min(enemy.FireTimer, 0.2f);
            if (enemy.Role == EnemyRole.Hunter) enemy.HoldTimer = NextFloat(0.4f, 0.9f);
        }

        /// <summary>The direction of a clear shot from this tank, leading a moving player. Public for tests.</summary>
        public Dir? FindLineShot(Tank enemy)
        {
            int startX = (int)Math.Floor(enemy.X);
            int startY = (int)Math.Floor(enemy.Y);
            Dir? playerLine = null;

            for (int d = 0; d < 4; d++)
            {
                Dir dir = (Dir)d;
                int x = startX;
                int y = startY;
                int distance = 0;
                while (true)
                {
                    StepCell(dir, ref x, ref y);
                    distance++;
                    if (!InBounds(x, y)) break;

                    Tile tile = Tiles[x, y];
                    if (tile == Tile.Base) return dir;
                    if (tile == Tile.Crate || tile == Tile.Concrete) break;
                    if (playerLine == null && PlayerWillBeAt(x, y, distance / enemy.BulletSpeed)) playerLine = dir;
                }
            }
            return playerLine;
        }

        bool PlayerWillBeAt(int x, int y, float seconds)
        {
            if (!Player.Alive) return false;
            float travel = Player.Moving ? Player.Speed * seconds : 0f;
            int futureX = (int)Math.Floor(Player.X + DirX(Player.Dir) * travel);
            int futureY = (int)Math.Floor(Player.Y + DirY(Player.Dir) * travel);
            return x == futureX && y == futureY;
        }

        bool CrateAhead(Tank enemy)
        {
            int x = (int)Math.Floor(enemy.X);
            int y = (int)Math.Floor(enemy.Y);
            StepCell(enemy.Dir, ref x, ref y);
            return InBounds(x, y) && Tiles[x, y] == Tile.Crate;
        }

        void RefreshRoutes()
        {
            if (baseRoutesStale)
            {
                BuildRoute(baseRoute, BaseX, BaseY, Tuning.CrateRouteCost);
                BuildRoute(breakerRoute, BaseX, BaseY, 1);
                baseRoutesStale = false;
                playerRouteStale = true;
            }

            if (!Player.Alive) return;
            int x = (int)Math.Floor(Player.X);
            int y = (int)Math.Floor(Player.Y);
            if (playerRouteStale || x != playerRouteX || y != playerRouteY)
            {
                BuildRoute(playerRoute, x, y, Tuning.CrateRouteCost);
                playerRouteX = x;
                playerRouteY = y;
                playerRouteStale = false;
            }
        }

        // Spreads outward from the target. Crates are passable at a cost because they can be shot away.
        void BuildRoute(int[,] route, int targetX, int targetY, int crateCost)
        {
            for (int y = 0; y < Levels.Height; y++)
                for (int x = 0; x < Levels.Width; x++)
                    route[x, y] = int.MaxValue;

            route[targetX, targetY] = 0;
            routeQueue.Clear();
            routeQueue.Enqueue((targetX, targetY));
            while (routeQueue.Count > 0)
            {
                var cell = routeQueue.Dequeue();
                bool isTarget = cell.x == targetX && cell.y == targetY;
                int enter = isTarget ? 0 : Tiles[cell.x, cell.y] == Tile.Crate ? crateCost : 1;
                int cost = route[cell.x, cell.y] + enter;
                for (int d = 0; d < 4; d++)
                {
                    int x = cell.x;
                    int y = cell.y;
                    StepCell((Dir)d, ref x, ref y);
                    if (!InBounds(x, y) || !IsRoutable(Tiles[x, y]) || route[x, y] <= cost) continue;
                    route[x, y] = cost;
                    routeQueue.Enqueue((x, y));
                }
            }
        }

        static bool IsRoutable(Tile tile)
        {
            return !IsSolid(tile) || tile == Tile.Crate;
        }

        /// <summary>The next step towards this enemy's target, or null without a route. Public for tests.</summary>
        public Dir? NextRouteStep(Tank enemy)
        {
            RefreshRoutes();
            int[,] route = baseRoute;
            if (enemy.Role == EnemyRole.Breaker) route = breakerRoute;
            else if (enemy.Role == EnemyRole.Hunter && Player.Alive) route = playerRoute;

            int startX = (int)Math.Floor(enemy.X);
            int startY = (int)Math.Floor(enemy.Y);
            int best = route[startX, startY];
            Dir? step = null;
            int first = rng.Next(4);
            for (int i = 0; i < 4; i++)
            {
                Dir dir = (Dir)((first + i) % 4);
                int x = startX;
                int y = startY;
                StepCell(dir, ref x, ref y);
                if (InBounds(x, y) && route[x, y] < best)
                {
                    best = route[x, y];
                    step = dir;
                }
            }
            return step;
        }

        static bool NearCentre(Tank tank)
        {
            bool horizontal = tank.Dir == Dir.Left || tank.Dir == Dir.Right;
            float along = horizontal ? tank.X : tank.Y;
            return Math.Abs(along - ((float)Math.Floor(along) + 0.5f)) < 0.1f;
        }

        Dir ChooseDirection(Tank enemy, bool blocked)
        {
            if (rng.NextDouble() < Smartness)
            {
                // Stuck behind another tank: step aside instead of pushing along the route forever.
                if (blocked && !CrateAhead(enemy) && rng.NextDouble() < 0.5) return RandomOpenDirection(enemy);
                Dir? step = NextRouteStep(enemy);
                if (step != null) return step.Value;
            }

            if (rng.NextDouble() < 0.65)
            {
                bool huntPlayer = Player.Alive && rng.NextDouble() < 0.4;
                float targetX = huntPlayer ? Player.X : BaseX + 0.5f;
                float targetY = huntPlayer ? Player.Y : BaseY + 0.5f;
                float dx = targetX - enemy.X;
                float dy = targetY - enemy.Y;

                bool preferX = Math.Abs(dx) >= Math.Abs(dy);
                if (rng.NextDouble() < 0.3) preferX = !preferX;

                Dir dir = preferX ? (dx < 0f ? Dir.Left : Dir.Right) : (dy < 0f ? Dir.Up : Dir.Down);
                if (IsShootThroughOrOpen(enemy, dir)) return dir;
            }
            return RandomOpenDirection(enemy);
        }

        // Crates count as a valid way forward: the enemy drives up to them and shoots.
        bool IsShootThroughOrOpen(Tank enemy, Dir dir)
        {
            int x = (int)Math.Floor(enemy.X);
            int y = (int)Math.Floor(enemy.Y);
            StepCell(dir, ref x, ref y);
            if (!InBounds(x, y)) return false;
            Tile tile = Tiles[x, y];
            return !IsSolid(tile) || tile == Tile.Crate || tile == Tile.Base;
        }

        Dir RandomOpenDirection(Tank enemy)
        {
            int start = rng.Next(4);
            for (int i = 0; i < 4; i++)
            {
                Dir dir = (Dir)((start + i) % 4);
                int x = (int)Math.Floor(enemy.X);
                int y = (int)Math.Floor(enemy.Y);
                StepCell(dir, ref x, ref y);
                if (InBounds(x, y) && !IsSolid(Tiles[x, y])) return dir;
            }
            return (Dir)start;
        }

        bool HasTargetInLine(Tank enemy)
        {
            int x = (int)Math.Floor(enemy.X);
            int y = (int)Math.Floor(enemy.Y);
            int playerX = Player.Alive ? (int)Math.Floor(Player.X) : -1;
            int playerY = Player.Alive ? (int)Math.Floor(Player.Y) : -1;
            while (true)
            {
                StepCell(enemy.Dir, ref x, ref y);
                if (!InBounds(x, y)) return false;
                if (x == playerX && y == playerY) return true;
                Tile tile = Tiles[x, y];
                if (tile == Tile.Base) return true;
                if (tile == Tile.Crate || tile == Tile.Concrete) return false;
            }
        }

        void DamageEnemy(Tank enemy, int damage, bool chargesSuper = true)
        {
            if (enemy.Carrier)
            {
                enemy.Carrier = false;
                SpawnBonus();
            }
            enemy.Hp -= damage;
            if (enemy.Hp > 0)
            {
                Events.Add(new GameEvent(GameEventKind.TankHit, enemy.X, enemy.Y));
                return;
            }
            DestroyEnemy(enemy, chargesSuper);
        }

        // Kills made by the strike itself do not charge the next one.
        void DestroyEnemy(Tank enemy, bool chargesSuper = true)
        {
            enemy.Alive = false;
            enemy.Hp = 0;
            Score += Tuning.EnemyScore(enemy.Kind);
            PartsEarned += Tuning.EnemyParts(enemy.Kind);
            if (chargesSuper) AddSuperCharge();
            Events.Add(new GameEvent(GameEventKind.TankDestroyed, enemy.X, enemy.Y, false, BonusKind.Star, enemy.Dir));
        }

        // ---- Bullets ----

        int BulletsOf(Tank tank)
        {
            int count = 0;
            foreach (Bullet bullet in Bullets)
                if (bullet.OwnerId == tank.Id) count++;
            return count;
        }

        void Fire(Tank tank, float speed, bool pierce, int damage = 1)
        {
            Bullets.Add(new Bullet
            {
                OwnerId = tank.Id,
                FromPlayer = tank.IsPlayer,
                Pierce = pierce,
                Damage = damage,
                X = tank.X + DirX(tank.Dir) * 0.5f,
                Y = tank.Y + DirY(tank.Dir) * 0.5f,
                Dir = tank.Dir,
                Speed = speed
            });
            Events.Add(new GameEvent(GameEventKind.Shot, tank.X, tank.Y, tank.IsPlayer));
        }

        void UpdateBullets(float dt)
        {
            const float maxStep = 0.1f;
            foreach (Bullet bullet in Bullets)
            {
                float remaining = bullet.Speed * dt;
                while (bullet.Alive && remaining > 0f && Phase == GamePhase.Playing)
                {
                    float step = Math.Min(maxStep, remaining);
                    remaining -= step;
                    bullet.X += DirX(bullet.Dir) * step;
                    bullet.Y += DirY(bullet.Dir) * step;
                    StepBullet(bullet);
                }
            }

            ResolveBulletDuels();
            Bullets.RemoveAll(b => !b.Alive);
            Enemies.RemoveAll(e => !e.Alive);
        }

        void StepBullet(Bullet bullet)
        {
            int x = (int)Math.Floor(bullet.X);
            int y = (int)Math.Floor(bullet.Y);
            if (!InBounds(x, y))
            {
                bullet.Alive = false;
                Events.Add(new GameEvent(GameEventKind.BulletSpark, bullet.X, bullet.Y));
                return;
            }

            if (HitTile(bullet, x, y)) return;

            const float h = Tuning.TankHalf;
            if (bullet.FromPlayer)
            {
                foreach (Tank enemy in Enemies)
                {
                    if (!enemy.Alive || enemy.Spawning) continue;
                    if (Math.Abs(enemy.X - bullet.X) < h && Math.Abs(enemy.Y - bullet.Y) < h)
                    {
                        bullet.Alive = false;
                        DamageEnemy(enemy, bullet.Damage);
                        return;
                    }
                }
            }
            else if (Player.Alive && Math.Abs(Player.X - bullet.X) < h && Math.Abs(Player.Y - bullet.Y) < h)
            {
                bullet.Alive = false;
                HurtPlayer();
            }
        }

        bool HitTile(Bullet bullet, int x, int y)
        {
            switch (Tiles[x, y])
            {
                case Tile.Crate:
                    bullet.Alive = false;
                    crateHp[x, y] = bullet.Pierce ? 0 : crateHp[x, y] - 1;
                    if (crateHp[x, y] <= 0)
                    {
                        Tiles[x, y] = Tile.Empty;
                        baseRoutesStale = true;
                    }
                    Events.Add(new GameEvent(GameEventKind.TileChanged, x, y));
                    Events.Add(new GameEvent(GameEventKind.BulletSpark, bullet.X, bullet.Y));
                    return true;

                case Tile.Concrete:
                    bullet.Alive = false;
                    if (bullet.Pierce)
                    {
                        Tiles[x, y] = Tile.Empty;
                        baseRoutesStale = true;
                        Events.Add(new GameEvent(GameEventKind.TileChanged, x, y));
                    }
                    Events.Add(new GameEvent(GameEventKind.BulletSpark, bullet.X, bullet.Y));
                    return true;

                case Tile.Base:
                    bullet.Alive = false;
                    Tiles[x, y] = Tile.Empty;
                    Phase = GamePhase.GameOver;
                    Events.Add(new GameEvent(GameEventKind.TileChanged, x, y));
                    Events.Add(new GameEvent(GameEventKind.BaseDestroyed, x + 0.5f, y + 0.5f));
                    return true;

                default:
                    return false;
            }
        }

        void ResolveBulletDuels()
        {
            for (int i = 0; i < Bullets.Count; i++)
            {
                Bullet a = Bullets[i];
                if (!a.Alive || !a.FromPlayer) continue;
                for (int j = 0; j < Bullets.Count; j++)
                {
                    Bullet b = Bullets[j];
                    if (!b.Alive || b.FromPlayer) continue;
                    if (Math.Abs(a.X - b.X) < 0.3f && Math.Abs(a.Y - b.Y) < 0.3f)
                    {
                        a.Alive = false;
                        b.Alive = false;
                        Events.Add(new GameEvent(GameEventKind.BulletSpark, a.X, a.Y));
                        break;
                    }
                }
            }
        }

        // ---- Bonuses ----

        void SpawnBonus()
        {
            var free = new List<(int x, int y)>();
            for (int y = 0; y < Levels.Height; y++)
                for (int x = 1; x < Levels.Width - 1; x++)
                    if (Tiles[x, y] == Tile.Empty) free.Add((x, y));
            if (free.Count == 0) return;

            var cell = free[rng.Next(free.Count)];
            Bonus = new Bonus
            {
                Kind = (BonusKind)rng.Next(Enum.GetValues(typeof(BonusKind)).Length),
                X = cell.x,
                Y = cell.y,
                Timer = Tuning.BonusLifetime
            };
            Events.Add(new GameEvent(GameEventKind.BonusSpawned, cell.x + 0.5f, cell.y + 0.5f));
        }

        /// <summary>Puts a chosen bonus on the map. Used by checks; in play bonuses come from carrier tanks.</summary>
        public void PlaceBonus(BonusKind kind, int x, int y)
        {
            Bonus = new Bonus { Kind = kind, X = x, Y = y, Timer = Tuning.BonusLifetime };
        }

        void UpdateBonus(float dt)
        {
            if (Bonus == null) return;
            Bonus.Timer -= dt;
            if (Bonus.Timer <= 0f)
            {
                Bonus = null;
                return;
            }
            if (!Player.Alive) return;

            float dx = Player.X - (Bonus.X + 0.5f);
            float dy = Player.Y - (Bonus.Y + 0.5f);
            if (Math.Abs(dx) < 0.7f && Math.Abs(dy) < 0.7f)
            {
                Events.Add(new GameEvent(GameEventKind.BonusTaken, Bonus.X + 0.5f, Bonus.Y + 0.5f, true, Bonus.Kind));
                ApplyBonus(Bonus.Kind);
                Bonus = null;
            }
        }

        public void ApplyBonus(BonusKind kind)
        {
            Score += Tuning.BonusScore;
            switch (kind)
            {
                case BonusKind.Star:
                    StarLevel = Math.Min(Tuning.MaxUpgrade, StarLevel + 1);
                    StarTimer = Tuning.StarTime;
                    break;
                case BonusKind.Bomb:
                    foreach (Tank enemy in Enemies)
                    {
                        if (enemy.Spawning) continue;
                        if (enemy.Kind == EnemyKind.Boss) DamageEnemy(enemy, Tuning.BossBombDamage);
                        else DestroyEnemy(enemy);
                    }
                    Enemies.RemoveAll(e => !e.Alive);
                    Events.Add(new GameEvent(GameEventKind.BombBlast, Player.X, Player.Y));
                    break;
                case BonusKind.Shield:
                    Player.ShieldTimer = Tuning.ShieldBonusTime;
                    break;
                case BonusKind.Fort:
                    StartFort(Tuning.FortTime);
                    break;
                case BonusKind.Freeze:
                    FreezeTimer = Tuning.FreezeTime;
                    break;
                case BonusKind.Life:
                    Lives++;
                    break;
            }
        }

        void StartFort(float seconds)
        {
            FortTimer = Math.Max(FortTimer, seconds);
            if (fortCells.Count > 0) return;
            baseRoutesStale = true;
            for (int y = BaseY - 1; y <= BaseY + 1; y++)
            {
                for (int x = BaseX - 1; x <= BaseX + 1; x++)
                {
                    if (!InBounds(x, y) || Tiles[x, y] == Tile.Base) continue;
                    if (Tiles[x, y] != Tile.Empty && Tiles[x, y] != Tile.Crate && Tiles[x, y] != Tile.Concrete) continue;
                    if (AnyTankAt(x + 0.5f, y + 0.5f)) continue;
                    Tiles[x, y] = Tile.Concrete;
                    crateHp[x, y] = 0;
                    fortCells.Add((x, y));
                    Events.Add(new GameEvent(GameEventKind.TileChanged, x, y));
                }
            }
        }

        void UpdateFort(float dt)
        {
            if (FortTimer <= 0f) return;
            FortTimer -= dt;
            if (FortTimer > 0f) return;

            baseRoutesStale = true;
            foreach (var cell in fortCells)
            {
                if (AnyTankAt(cell.x + 0.5f, cell.y + 0.5f)) continue;
                Tiles[cell.x, cell.y] = Tile.Crate;
                crateHp[cell.x, cell.y] = Tuning.CrateHp;
                Events.Add(new GameEvent(GameEventKind.TileChanged, cell.x, cell.y));
            }
            fortCells.Clear();
        }

        // ---- Level flow ----

        void CheckLevelEnd(float dt)
        {
            if (Phase != GamePhase.Playing || EnemiesLeft > 0) return;
            clearTimer += dt;
            if (clearTimer < Tuning.ClearDelay) return;

            if (Mode == GameMode.Survival)
            {
                PartsEarned += Tuning.WaveParts(Wave);
                Bullets.Clear();
                OfferChoices();
                Phase = GamePhase.WaveCleared;
                return;
            }
            PartsEarned += Tuning.LevelParts(LevelIndex);
            Phase = LevelIndex + 1 < Levels.All.Length ? GamePhase.LevelCleared : GamePhase.Victory;
        }

        // ---- Helpers ----

        Tile TileAt(float x, float y)
        {
            int cx = (int)Math.Floor(x);
            int cy = (int)Math.Floor(y);
            return InBounds(cx, cy) ? Tiles[cx, cy] : Tile.Concrete;
        }

        static bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Levels.Width && y < Levels.Height;
        }

        public static float DirX(Dir dir)
        {
            return dir == Dir.Right ? 1f : dir == Dir.Left ? -1f : 0f;
        }

        public static float DirY(Dir dir)
        {
            return dir == Dir.Down ? 1f : dir == Dir.Up ? -1f : 0f;
        }

        static void StepCell(Dir dir, ref int x, ref int y)
        {
            x += (int)DirX(dir);
            y += (int)DirY(dir);
        }

        float NextFloat(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        static float Sq(float value) => value * value;
    }
}
