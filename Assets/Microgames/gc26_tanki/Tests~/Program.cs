using System;
using System.Collections.Generic;
using GC26.Tanki.Rules;

// Runs without Unity: dotnet run --project Tests~
namespace GC26.Tanki.Tests
{
static class Program
{
    static int failures;
    const float Dt = GameSim.FixedStep;

    static int Main()
    {
        Test("maps have the right size and exactly one base and player spawn", MapsAreWellFormed);
        Test("every enemy spawn can reach the player and the base", MapsAreConnected);
        Test("a crate takes two ordinary hits", CrateTakesTwoHits);
        Test("concrete stops a shot until the top upgrade", ConcreteNeedsTopUpgrade);
        Test("shots cross water and tanks cannot", WaterStopsTanksOnly);
        Test("a hit on the base ends the game", BaseHitEndsGame);
        Test("the player keeps sliding on oil and stops on sand", OilSlides);
        Test("a turn requested before a junction happens at the junction", TurnIsBuffered);
        Test("opposing shots cancel each other", ShotsCancel);
        Test("the shield absorbs a shot and no shield costs a life", ShieldAndLives);
        Test("a heavy takes three hits", HeavyTakesThreeHits);
        Test("every bonus does what it says", BonusesWork);
        Test("the fort turns back into crates when it ends", FortRestores);
        Test("all waves spawn and clearing them finishes the level", LevelClears);
        Test("a smart enemy finds the way around a wall much faster", SmartEnemyFindsTheWay);
        Test("a breaker's route goes straight through crates", BreakerGoesThroughCrates);
        Test("a hunter turns to a clear shot and stays put", HunterTakesTheShot);
        Test("a smart enemy aims ahead of a moving player", SmartEnemyLeadsTheTarget);
        Test("routes update when a crate is destroyed", RoutesFollowTheMap);
        Test("difficulty sets enemy count, speed and smartness", DifficultyChangesEnemies);
        Test("an undefended base lasts longer on easier settings", UndefendedBaseSurvival);
        Test("60 bot games on normal keep every invariant", () => BotGamesKeepInvariants(Difficulty.Normal, 60));
        Test("30 bot games on easy keep every invariant", () => BotGamesKeepInvariants(Difficulty.Easy, 30));
        Test("30 bot games on hard keep every invariant", () => BotGamesKeepInvariants(Difficulty.Hard, 30));
        Test("the star's gun lasts a while and is lost with the tank", StarIsTemporary);
        Test("workshop upgrades change armour, gun, fire rate and speed", WorkshopUpgradesWork);
        Test("a hit costs health, and a life only when health runs out", HealthBeforeLives);
        Test("kills charge the reactor strike and it fires along the barrel", SuperStrikeWorks);
        Test("later levels need more kills for the strike", SuperNeedGrows);
        Test("every fifth level ends with a boss that fires three ways", BossLevels);
        Test("kills and cleared levels earn parts", PartsAreEarned);
        Test("survival offers three upgrades after each wave", SurvivalOffersChoices);
        Test("20 bot survival runs keep every invariant", SurvivalBotRuns);
        Test("the garage tanks change health, speed and fire rate", TanksChangeNumbers);
        Test("the squall's volley fires piercing shells all four ways", VolleyWorks);
        Test("the bastion's dome shields the tank and stuns the enemies close by", DomeWorks);
        Test("the phantom dashes ahead and the enemy loses sight of it", DashAndCloakWork);
        Test("a miner lays mines, a mine hurts the player, a shot clears it", MinesWork);
        Test("a fixer mends the hurt tanks around it", FixerMends);
        Test("a rammer blows up against a crate and next to the player", RammerBlowsUp);
        Test("assault maps have one enemy reactor and gates that reach the player", AssaultMapsAreWellFormed);
        Test("turrets shoot along a clear line and fall after three hits", TurretsWork);
        Test("breaking the enemy reactor wins the assault", CoreWinsAssault);
        Test("stars count the win, no tank lost and the par time", StarsAreEarned);
        Test("bot assaults with every tank keep every invariant", AssaultBotRuns);

        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }

    static void Test(string name, Action body)
    {
        try
        {
            body();
            Console.WriteLine("ok    " + name);
        }
        catch (Exception e)
        {
            failures++;
            Console.WriteLine("FAIL  " + name + "\n      " + e.Message);
        }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // A map full of sand with a base far away, so nothing interferes with the situation.
    static string[] Blank(params (int x, int y, char c)[] cells)
    {
        var grid = new char[Levels.Height][];
        for (int y = 0; y < Levels.Height; y++)
            grid[y] = new string('.', Levels.Width).ToCharArray();
        grid[10][0] = 'B';
        grid[5][2] = 'P';
        foreach (var cell in cells) grid[cell.y][cell.x] = cell.c;
        var rows = new string[Levels.Height];
        for (int y = 0; y < Levels.Height; y++) rows[y] = new string(grid[y]);
        return rows;
    }

    static GameSim Sim(string waves, params (int x, int y, char c)[] cells)
    {
        var sim = new GameSim(7);
        // An unspawnable wave keeps the level running for tests that want no enemies.
        sim.LoadMap(Blank(cells), waves == "" ? "R" : waves);
        sim.Smartness = 0f;
        return sim;
    }

    static void Run(GameSim sim, float seconds, PlayerInput input = default)
    {
        int ticks = (int)(seconds / Dt);
        for (int i = 0; i < ticks; i++) sim.Tick(Dt, input);
    }

    static void Shoot(GameSim sim, Dir dir)
    {
        sim.Player.Dir = dir;
        sim.Tick(Dt, new PlayerInput { Fire = true });
        Run(sim, 1.5f);
    }

    static void MapsAreWellFormed()
    {
        Check(Levels.All.Length == 20, "there should be 20 levels, found " + Levels.All.Length);
        Check(Tuning.LevelSmartness.Length == Levels.All.Length, "every level needs a smartness");
        var maps = new List<LevelDef>(Levels.All) { Levels.Survival };
        for (int index = 0; index < maps.Count; index++)
        {
            LevelDef level = maps[index];
            Check(level.Rows.Length == Levels.Height, level.Name + ": wrong height");
            int bases = 0, players = 0, enemies = 0;
            foreach (string row in level.Rows)
            {
                Check(row.Length == Levels.Width, level.Name + ": wrong width in " + row);
                foreach (char c in row)
                {
                    if (c == 'B') bases++;
                    if (c == 'P') players++;
                    if (c == 'E') enemies++;
                    Check(".#@%~=BPE".IndexOf(c) >= 0, level.Name + ": unknown tile " + c);
                }
            }
            Check(bases == 1 && players == 1 && enemies > 0, level.Name + ": bad markers");
            foreach (char c in level.Waves)
                Check("RSGHXMFK".IndexOf(c) >= 0, level.Name + ": unknown wave " + c);
            if (level == Levels.Survival) continue;
            bool bossLevel = (index + 1) % Tuning.BossEvery == 0;
            Check(bossLevel ? level.Waves.IndexOf('X') == level.Waves.Length - 1 : level.Waves.IndexOf('X') < 0,
                level.Name + ": the boss belongs at the end of every fifth level only");
        }
    }

    static void MapsAreConnected()
    {
        foreach (LevelDef level in new List<LevelDef>(Levels.All) { Levels.Survival })
        {
            // Crates count as passable because they can be shot away; concrete and water cannot.
            var seen = new bool[Levels.Width, Levels.Height];
            var queue = new Queue<(int x, int y)>();
            var targets = new List<(int x, int y)>();
            for (int y = 0; y < Levels.Height; y++)
                for (int x = 0; x < Levels.Width; x++)
                {
                    char c = level.Rows[y][x];
                    if (c == 'P')
                    {
                        queue.Enqueue((x, y));
                        seen[x, y] = true;
                    }
                    if (c == 'E' || c == 'B') targets.Add((x, y));
                }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Levels.Width || ny >= Levels.Height || seen[nx, ny]) continue;
                    char c = level.Rows[ny][nx];
                    if (c == '@' || c == '~') continue;
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
            foreach (var (x, y) in targets)
                Check(seen[x, y], level.Name + ": cell " + x + "," + y + " is cut off");
        }
    }

    static void CrateTakesTwoHits()
    {
        var sim = Sim("", (5, 5, '#'));
        Shoot(sim, Dir.Right);
        Check(sim.Tiles[5, 5] == Tile.Crate && sim.CrateHp(5, 5) == 1, "first hit should only damage");
        Shoot(sim, Dir.Right);
        Check(sim.Tiles[5, 5] == Tile.Empty, "second hit should destroy");
    }

    static void ConcreteNeedsTopUpgrade()
    {
        var sim = Sim("", (5, 5, '@'));
        Shoot(sim, Dir.Right);
        Check(sim.Tiles[5, 5] == Tile.Concrete, "ordinary shot must not break concrete");
        for (int i = 0; i < Tuning.MaxUpgrade; i++) sim.ApplyBonus(BonusKind.Star);
        Shoot(sim, Dir.Right);
        Check(sim.Tiles[5, 5] == Tile.Empty, "top upgrade must break concrete");
    }

    static void WaterStopsTanksOnly()
    {
        var sim = Sim("", (4, 5, '~'), (6, 5, '#'));
        Shoot(sim, Dir.Right);
        Check(sim.CrateHp(6, 5) == 1, "shot should cross the water and hit the crate");
        Run(sim, 2f, new PlayerInput { Move = Dir.Right });
        Check(sim.Player.X < 4f, "tank must stop at the water, x=" + sim.Player.X);
        Check(sim.Player.X > 3.4f, "tank should reach the water's edge, x=" + sim.Player.X);
    }

    static void BaseHitEndsGame()
    {
        var sim = Sim("");
        sim.Player.X = 0.5f;
        sim.Player.Y = 7.5f;
        Shoot(sim, Dir.Down);
        Check(sim.Phase == GamePhase.GameOver, "own shot on the base should end the game");
        Check(sim.Tiles[0, 10] == Tile.Empty, "base should be gone");
    }

    static void OilSlides()
    {
        var oil = new List<(int, int, char)>();
        for (int x = 3; x < Levels.Width; x++) oil.Add((x, 5, '='));
        var sim = Sim("", oil.ToArray());
        Run(sim, 1f, new PlayerInput { Move = Dir.Right });
        float atRelease = sim.Player.X;
        Run(sim, 1f);
        Check(sim.Player.X > atRelease + 0.8f, "should slide on oil, moved " + (sim.Player.X - atRelease));

        var sand = Sim("");
        Run(sand, 1f, new PlayerInput { Move = Dir.Right });
        atRelease = sand.Player.X;
        Run(sand, 1f);
        Check(Math.Abs(sand.Player.X - atRelease) < 0.001f, "should stop at once on sand");
    }

    static void TurnIsBuffered()
    {
        // A wall above the corridor with one gap at x=6.
        var cells = new List<(int, int, char)>();
        for (int x = 0; x < Levels.Width; x++)
            if (x != 6) cells.Add((x, 4, '@'));
        var sim = Sim("", cells.ToArray());

        Run(sim, 0.25f, new PlayerInput { Move = Dir.Right });
        // Held "up" while the wall is above: the tank should keep rolling to the gap and turn in.
        Run(sim, 3f, new PlayerInput { Move = Dir.Up });
        Check(sim.Player.Dir == Dir.Up, "should end up facing up");
        Check(Math.Abs(sim.Player.X - 6.5f) < 0.01f, "should turn exactly at the gap, x=" + sim.Player.X);
        Check(sim.Player.Y < 4f, "should have gone through the gap, y=" + sim.Player.Y);
    }

    static void ShotsCancel()
    {
        var sim = Sim("");
        sim.Player.Dir = Dir.Right;
        sim.Tick(Dt, new PlayerInput { Fire = true });
        sim.Bullets.Add(new Bullet { OwnerId = -1, X = 8.5f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
        Run(sim, 1f);
        Check(sim.Bullets.Count == 0, "both shots should be gone");
        Check(sim.Lives == Tuning.StartLives, "nobody should be hit");
    }

    static void ShieldAndLives()
    {
        var sim = Sim("");
        Check(sim.Player.Shielded, "player starts shielded");
        sim.Bullets.Add(new Bullet { OwnerId = -1, X = 5.5f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
        Run(sim, 0.7f);
        Check(sim.Player.Alive && sim.Lives == Tuning.StartLives, "shield should absorb the shot");

        Run(sim, Tuning.RespawnShield);
        sim.Player.Hp = 1;
        sim.Bullets.Add(new Bullet { OwnerId = -1, X = 5.5f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
        Run(sim, 0.7f);
        Check(sim.Lives == Tuning.StartLives - 1, "a hit on the last health should cost a life");
        Run(sim, Tuning.RespawnDelay + 0.1f);
        Check(sim.Player.Alive && sim.Player.Shielded, "should respawn with a shield");
    }

    static void HeavyTakesThreeHits()
    {
        var sim = Sim("H", (10, 5, 'E'));
        Run(sim, 0.5f + Tuning.SpawnFlash + 0.05f);
        Check(sim.Enemies.Count == 1, "heavy should have spawned");
        Tank heavy = sim.Enemies[0];
        Check(heavy.Hp == 3, "heavy hp");
        for (int hit = 0; hit < 3; hit++)
        {
            sim.Bullets.Add(new Bullet { OwnerId = sim.Player.Id, FromPlayer = true, X = heavy.X, Y = heavy.Y, Dir = Dir.Up, Speed = 1f });
            sim.Tick(Dt, default);
        }
        Check(!heavy.Alive && sim.Enemies.Count == 0, "heavy should be dead after three hits");
        Check(sim.Score == Tuning.EnemyScore(EnemyKind.Heavy), "score for the heavy, got " + sim.Score);
    }

    static void BonusesWork()
    {
        var sim = Sim("RR", (10, 3, 'E'), (10, 7, 'E'));
        Run(sim, 0.5f + sim.Settings.SpawnDelay + Tuning.SpawnFlash + 0.2f);
        Check(sim.Enemies.Count == 2, "two enemies expected, got " + sim.Enemies.Count);

        sim.ApplyBonus(BonusKind.Freeze);
        float x = sim.Enemies[0].X;
        Run(sim, 2f);
        Check(sim.Enemies[0].X == x, "freeze should hold enemies");

        sim.ApplyBonus(BonusKind.Bomb);
        Check(sim.Enemies.Count == 0, "bomb should clear the field");

        int lives = sim.Lives;
        sim.ApplyBonus(BonusKind.Life);
        Check(sim.Lives == lives + 1, "life bonus");

        Run(sim, Tuning.RespawnShield + 0.1f);
        sim.ApplyBonus(BonusKind.Shield);
        Check(sim.Player.Shielded, "shield bonus");

        for (int i = 0; i < 6; i++) sim.ApplyBonus(BonusKind.Star);
        Check(sim.StarLevel == Tuning.MaxUpgrade, "upgrade is capped");
    }

    static void FortRestores()
    {
        var sim = Sim("R", (10, 5, 'E'));
        sim.ApplyBonus(BonusKind.Fort);
        Check(sim.Tiles[0, 9] == Tile.Concrete && sim.Tiles[1, 10] == Tile.Concrete, "fort should wall the base");
        Run(sim, 0.5f);
        Check(sim.Tiles[1, 9] == Tile.Concrete, "fort should hold");
        sim.ApplyBonus(BonusKind.Freeze);
        Run(sim, Tuning.FortTime);
        Check(sim.Tiles[0, 9] == Tile.Crate && sim.CrateHp(0, 9) == Tuning.CrateHp, "fort should revert to crates");
    }

    static void LevelClears()
    {
        var sim = Sim("RRR", (10, 3, 'E'), (10, 7, 'E'));
        int guard = 0;
        while (sim.Phase == GamePhase.Playing && guard++ < 60 * 60)
        {
            foreach (Tank enemy in sim.Enemies)
                if (!enemy.Spawning)
                    sim.Bullets.Add(new Bullet { OwnerId = sim.Player.Id, FromPlayer = true, X = enemy.X, Y = enemy.Y, Dir = Dir.Up, Speed = 1f });
            sim.Tick(Dt, default);
        }
        Check(sim.Phase == GamePhase.LevelCleared, "level should clear, phase " + sim.Phase);
        Check(sim.Score == 3 * Tuning.EnemyScore(EnemyKind.Raider), "score, got " + sim.Score);
    }

    // A wall across the map with one gap at the far side, between a lone scout and the base.
    static float SecondsToReachBase(int seed, float smartness)
    {
        var cells = new List<(int, int, char)> { (2, 3, 'E') };
        for (int x = 0; x < Levels.Width - 1; x++) cells.Add((x, 7, '@'));
        var sim = new GameSim(seed);
        sim.LoadMap(Blank(cells.ToArray()), "S");
        sim.Smartness = smartness;
        sim.Player.X = 5.5f;
        sim.Player.Y = 0.5f;

        const float limit = 90f;
        for (float t = 0f; t < limit; t += Dt)
        {
            sim.Player.ShieldTimer = 9f;
            sim.Tick(Dt, default);
            if (sim.Phase == GamePhase.GameOver) return t;
        }
        return limit;
    }

    static void SmartEnemyFindsTheWay()
    {
        float dumb = 0f, smart = 0f;
        const int runs = 20;
        for (int seed = 0; seed < runs; seed++)
        {
            dumb += SecondsToReachBase(seed, 0f) / runs;
            smart += SecondsToReachBase(seed, 1f) / runs;
        }
        Console.WriteLine("      seconds to the base: simple " + dumb.ToString("0.0") + ", smart " + smart.ToString("0.0"));
        Check(smart < 25f, "smart enemy took " + smart);
        Check(smart < dumb * 0.6f, "smart " + smart + " should clearly beat simple " + dumb);
    }

    static void BreakerGoesThroughCrates()
    {
        var cells = new List<(int, int, char)>();
        for (int x = 0; x < Levels.Width - 1; x++) cells.Add((x, 7, '#'));
        var sim = Sim("", cells.ToArray());
        var tank = new Tank { X = 0.5f, Y = 5.5f, Role = EnemyRole.Breaker };
        Check(sim.NextRouteStep(tank) == Dir.Down, "breaker should drive at the crates");
    }

    static void HunterTakesTheShot()
    {
        var sim = Sim("G", (10, 5, 'E'));
        sim.Smartness = 1f;
        Tank hunter = null;
        for (float t = 0f; t < 4f && sim.Bullets.Count == 0; t += Dt)
        {
            sim.Player.ShieldTimer = 9f;
            sim.Tick(Dt, default);
            if (sim.Enemies.Count > 0) hunter = sim.Enemies[0];
        }
        Check(hunter != null && hunter.Role == EnemyRole.Hunter, "gunner should hunt");
        Check(hunter.Dir == Dir.Left, "should face the player, faces " + hunter.Dir);
        Check(sim.Bullets.Count > 0, "should have fired");
    }

    static void SmartEnemyLeadsTheTarget()
    {
        var sim = Sim("");
        var tank = new Tank { X = 5.5f, Y = 2.5f, BulletSpeed = 6.5f };
        sim.Player.X = 2.5f;
        sim.Player.Y = 8.5f;
        sim.Player.Dir = Dir.Right;
        sim.Player.Moving = true;
        Check(sim.FindLineShot(tank) == Dir.Down, "should fire where the player is going");

        sim.Player.Moving = false;
        Check(sim.FindLineShot(tank) == null, "a parked player is not in line");
    }

    static void RoutesFollowTheMap()
    {
        var cells = new List<(int, int, char)>();
        for (int x = 0; x < Levels.Width; x++) cells.Add((x, 7, x == 5 ? '#' : '@'));
        var sim = Sim("", cells.ToArray());
        var tank = new Tank { X = 5.5f, Y = 6.5f, Role = EnemyRole.Rusher };
        Check(sim.NextRouteStep(tank) == Dir.Down, "the crate is the only way through");

        for (int hit = 0; hit < Tuning.CrateHp; hit++)
        {
            sim.Player.X = 5.5f;
            sim.Player.Y = 4.5f;
            Shoot(sim, Dir.Down);
        }
        Check(sim.Tiles[5, 7] == Tile.Empty, "crate should be gone");
        Check(sim.NextRouteStep(tank) == Dir.Down, "the way should still lead through the gap");
    }

    static void DifficultyChangesEnemies()
    {
        foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
        {
            var sim = new GameSim(3);
            sim.SetDifficulty(difficulty);
            sim.NewGame();
            int peak = 0;
            float speed = 0f;
            for (float t = 0f; t < 40f && sim.Phase == GamePhase.Playing; t += Dt)
            {
                sim.Player.ShieldTimer = 9f;
                sim.Tick(Dt, default);
                peak = Math.Max(peak, sim.Enemies.Count);
                foreach (Tank enemy in sim.Enemies)
                    if (enemy.Kind == EnemyKind.Raider) speed = enemy.Speed;
            }
            Console.WriteLine("      " + difficulty + ": up to " + peak + " enemies, raider speed " + speed.ToString("0.00"));
            Check(peak <= sim.Settings.MaxEnemies, "too many enemies on " + difficulty);
            Check(Math.Abs(speed - Tuning.EnemySpeed(EnemyKind.Raider) * sim.Settings.SpeedScale) < 0.01f, "wrong speed on " + difficulty);
        }

        var level = new GameSim(3);
        level.LoadLevel(0);
        float first = level.Smartness;
        int last = Levels.All.Length - 1;
        level.LoadLevel(last);
        Check(level.Smartness > first, "later levels should be smarter");
        level.SetDifficulty(Difficulty.Hard);
        level.LoadLevel(last);
        Check(level.Smartness == 1f, "hard's last level should be fully smart, got " + level.Smartness);
        level.SetDifficulty(Difficulty.Easy);
        level.LoadLevel(0);
        Check(level.Smartness == 0f, "easy's first level should be simple, got " + level.Smartness);
    }

    // Median seconds until the base falls while the player parks at the start and never fires.
    static float MedianBaseSurvival(Difficulty difficulty, int level)
    {
        const int runs = 21;
        const float limit = 180f;
        var times = new List<float>();
        for (int seed = 0; seed < runs; seed++)
        {
            var sim = new GameSim(500 + seed);
            sim.SetDifficulty(difficulty);
            sim.LoadLevel(level);
            float t = 0f;
            while (t < limit && sim.Phase == GamePhase.Playing)
            {
                sim.Player.ShieldTimer = 9f;
                sim.Tick(Dt, default);
                sim.Events.Clear();
                t += Dt;
            }
            times.Add(t);
        }
        times.Sort();
        return times[runs / 2];
    }

    static void UndefendedBaseSurvival()
    {
        float previousFirst = float.MaxValue;
        foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
        {
            float first = MedianBaseSurvival(difficulty, 0);
            float last = MedianBaseSurvival(difficulty, Levels.All.Length - 1);
            Console.WriteLine("      " + difficulty + ": base falls after " + first.ToString("0") + " s on level 1, " + last.ToString("0") + " s on level " + Levels.All.Length);
            Check(first <= previousFirst, difficulty + " should not be gentler than the easier setting");
            previousFirst = first;
        }
    }

    // A crude bot: drives at the nearest enemy and always fires. It proves the rules hold
    // over long play and gives a rough difficulty reading.
    static void BotGamesKeepInvariants(Difficulty difficulty, int games)
    {
        int cleared = 0, lost = 0, stalled = 0, levelsDone = 0;
        var rng = new Random(99);
        for (int game = 0; game < games; game++)
        {
            var sim = new GameSim(1000 + game);
            sim.SetDifficulty(difficulty);
            sim.NewGame();
            int ticks = 0;
            Dir? move = Dir.Right;
            while (ticks++ < 60 * 60 * 40)
            {
                if (ticks % 30 == 0) move = BotMove(sim, rng);
                sim.Tick(Dt, new PlayerInput { Move = move, Fire = true, Super = true });
                CheckInvariants(sim, game);
                sim.Events.Clear();

                if (sim.Phase == GamePhase.LevelCleared)
                {
                    levelsDone++;
                    sim.NextLevel();
                }
                else if (sim.Phase != GamePhase.Playing)
                {
                    break;
                }
            }
            if (sim.Phase == GamePhase.Victory) { cleared++; levelsDone++; }
            else if (sim.Phase == GamePhase.GameOver) lost++;
            else stalled++;
        }
        Console.WriteLine("      bot: " + cleared + " won, " + lost + " lost, " + stalled + " stalled, " + levelsDone + " levels cleared");
        Check(stalled == 0, stalled + " games never finished");
    }

    static Dir? BotMove(GameSim sim, Random rng)
    {
        if (!sim.Player.Alive) return null;
        if (sim.Enemies.Count == 0 || rng.NextDouble() < 0.3) return (Dir)rng.Next(4);
        Tank target = sim.Enemies[0];
        float dx = target.X - sim.Player.X;
        float dy = target.Y - sim.Player.Y;
        if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? Dir.Right : Dir.Left;
        return dy > 0 ? Dir.Down : Dir.Up;
    }

    static void CheckInvariants(GameSim sim, int game)
    {
        var tanks = new List<Tank>(sim.Enemies);
        if (sim.Player.Alive) tanks.Add(sim.Player);
        foreach (Tank tank in tanks)
        {
            Check(!float.IsNaN(tank.X) && !float.IsNaN(tank.Y), "NaN position in game " + game);
            const float h = Tuning.TankHalf;
            int minX = (int)Math.Floor(tank.X - h), maxX = (int)Math.Floor(tank.X + h);
            int minY = (int)Math.Floor(tank.Y - h), maxY = (int)Math.Floor(tank.Y + h);
            Check(minX >= 0 && minY >= 0 && maxX < Levels.Width && maxY < Levels.Height, "tank left the map in game " + game);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    Check(!GameSim.IsSolid(sim.Tiles[x, y]), "tank inside solid tile " + x + "," + y + " at " + tank.X + "," + tank.Y + " game " + game);
        }
        for (int i = 0; i < tanks.Count; i++)
            for (int j = i + 1; j < tanks.Count; j++)
                Check(Math.Abs(tanks[i].X - tanks[j].X) >= 0.5f || Math.Abs(tanks[i].Y - tanks[j].Y) >= 0.5f,
                    "tanks stacked in game " + game);
        Check(sim.Enemies.Count <= sim.Settings.MaxEnemies, "too many enemies");
    }

    // ---- Upgrades, strike, boss, survival ----

    static void HitEnemy(GameSim sim, Tank enemy, int damage = 1)
    {
        sim.Bullets.Add(new Bullet { OwnerId = sim.Player.Id, FromPlayer = true, X = enemy.X, Y = enemy.Y, Dir = Dir.Up, Speed = 1f, Damage = damage });
        sim.Tick(Dt, default);
    }

    // Waits until enough enemies stand on the field, frozen so they stay where they appeared.
    static void WaitForEnemies(GameSim sim, int count, float limit = 20f)
    {
        for (float t = 0f; t < limit && sim.Enemies.FindAll(e => !e.Spawning).Count < count; t += Dt)
        {
            sim.Player.ShieldTimer = 9f;
            sim.ApplyBonus(BonusKind.Freeze);
            sim.Tick(Dt, default);
        }
    }

    static void ChargeSuper(GameSim sim)
    {
        sim.FillSuper();
    }

    static void StarIsTemporary()
    {
        var sim = Sim("");
        sim.ApplyBonus(BonusKind.Star);
        Check(sim.StarLevel == 1 && sim.PlayerBulletSpeed > Tuning.GunBulletSpeed[0], "star should speed up the shots");
        Run(sim, Tuning.StarTime - 1f);
        Check(sim.StarLevel == 1, "star should still be on");
        Run(sim, 1.5f);
        Check(sim.StarLevel == 0, "star should run out after " + Tuning.StarTime + " s");

        sim.ApplyBonus(BonusKind.Star);
        sim.ApplyBonus(BonusKind.Star);
        Check(sim.PlayerMaxBullets == Tuning.FireBullets[0] + 1, "second star adds a shot");
        sim.Player.ShieldTimer = 0f;
        sim.Player.Hp = 1;
        sim.Bullets.Add(new Bullet { OwnerId = -1, X = 5.5f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
        Run(sim, 0.7f);
        Check(!sim.Player.Alive && sim.StarLevel == 0, "losing the tank should drop the star");
    }

    static void WorkshopUpgradesWork()
    {
        var stats = new PlayerStats();
        stats[Stat.Armor] = 2;
        stats[Stat.Gun] = 3;
        stats[Stat.FireRate] = 2;
        stats[Stat.Speed] = 3;
        var sim = new GameSim(5);
        sim.StartCampaign(0, stats);
        Check(sim.Player.MaxHp == Tuning.BaseHp + 2 && sim.Player.Hp == sim.Player.MaxHp, "armour adds health");
        Check(sim.Player.Speed == Tuning.DriveSpeed[3], "speed upgrade");
        Check(sim.PlayerMaxBullets == 2, "fire rate allows a second shot");
        Check(sim.PlayerBulletSpeed == Tuning.GunBulletSpeed[3], "gun speeds up shots");

        stats[Stat.Armor] = 3;
        Check(sim.MaxHp == Tuning.BaseHp + 2, "the game keeps its own copy of the stats");

        sim.LoadMap(Blank((10, 5, 'E')), "H");
        sim.Smartness = 0f;
        WaitForEnemies(sim, 1);
        Tank heavy = sim.Enemies[0];
        heavy.X = 7.5f;
        heavy.Y = 5.5f;
        sim.Player.X = 2.5f;
        sim.Player.Y = 5.5f;
        sim.Player.Dir = Dir.Right;
        sim.Tick(Dt, new PlayerInput { Fire = true });
        Check(sim.Bullets.Count == 1 && sim.Bullets[0].Damage == 2, "gun level 3 hits for 2");
        Run(sim, 1f);
        Check(heavy.Hp == 1, "the heavy should have lost 2 of 3, has " + heavy.Hp);
    }

    static void HealthBeforeLives()
    {
        var sim = Sim("");
        Run(sim, Tuning.RespawnShield + 0.1f);
        Check(sim.Player.Hp == Tuning.BaseHp, "full health at the start");
        sim.Bullets.Add(new Bullet { OwnerId = -1, X = 5.5f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
        sim.Bullets.Add(new Bullet { OwnerId = -1, X = 5.9f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
        Run(sim, 0.7f);
        Check(sim.Player.Alive && sim.Player.Hp == Tuning.BaseHp - 1, "two quick shots cost one health, hp " + sim.Player.Hp);
        Check(sim.Lives == Tuning.StartLives, "no life lost yet");

        for (int i = 0; i < Tuning.BaseHp - 1; i++)
        {
            Run(sim, Tuning.HurtTime);
            sim.Bullets.Add(new Bullet { OwnerId = -1, X = 5.5f, Y = 5.5f, Dir = Dir.Left, Speed = 6.5f });
            Run(sim, 0.7f);
        }
        Check(!sim.Player.Alive && sim.Lives == Tuning.StartLives - 1, "out of health costs a life");
        Run(sim, Tuning.RespawnDelay + 0.1f);
        Check(sim.Player.Alive && sim.Player.Hp == Tuning.BaseHp, "a new tank has full health");
    }

    static void SuperStrikeWorks()
    {
        var sim = Sim("RRRRRRRRRR", (10, 2, 'E'), (10, 8, 'E'), (0, 2, 'E'));
        int need = sim.SuperNeed;
        Check(need == Tuning.SuperNeed(0), "need on level 1");
        for (int kill = 0; kill < need; kill++)
        {
            Check(!sim.SuperReady, "not ready after " + kill + " kills");
            WaitForEnemies(sim, 1);
            HitEnemy(sim, sim.Enemies.Find(e => !e.Spawning));
        }
        Check(sim.SuperReady, "ready after " + need + " kills");
        sim.Tick(Dt, default);
        Check(sim.SuperReady, "stays ready until used");

        // Two enemies: one in the beam, one beside it; a crate on the line and one off it.
        var fresh = Sim("RR", (10, 2, 'E'), (10, 8, 'E'), (6, 5, '#'), (8, 2, '#'));
        WaitForEnemies(fresh, 2);
        Tank inBeam = fresh.Enemies[0], aside = fresh.Enemies[1];
        inBeam.X = 9.5f; inBeam.Y = 5.5f;
        aside.X = 9.5f; aside.Y = 8.5f;
        ChargeSuper(fresh);
        fresh.Player.Dir = Dir.Right;
        fresh.Events.Clear();
        fresh.Tick(Dt, new PlayerInput { Super = true });
        Check(!inBeam.Alive, "the enemy in the beam should be destroyed");
        Check(aside.Alive, "the enemy beside the beam should be untouched");
        Check(fresh.Tiles[6, 5] == Tile.Empty, "the crate on the line should be gone");
        Check(fresh.Tiles[8, 2] == Tile.Crate, "a crate off the line stays");
        Check(fresh.SuperCharge == 0, "kills by the strike do not charge the next one");
        Check(fresh.Events.Exists(e => e.Kind == GameEventKind.SuperStrike && e.Dir == Dir.Right), "strike event with its direction");
    }

    static void SuperNeedGrows()
    {
        Check(Tuning.SuperNeed(0) < Tuning.SuperNeed(Levels.All.Length - 1), "the last level should need more kills");
        var sim = new GameSim(2);
        sim.LoadLevel(10);
        Check(sim.SuperNeed == Tuning.SuperNeed(10) && sim.SuperCharge == 0, "a level starts uncharged");
    }

    static void BossLevels()
    {
        var sim = Sim("X", (5, 0, 'E'));
        sim.Player.ShieldTimer = 99f;
        Run(sim, 0.5f + Tuning.SpawnFlash * 2f + 0.1f);
        Tank boss = sim.Boss;
        Check(boss != null, "boss should spawn");
        Check(boss.Hp == Tuning.BossHp(1) && boss.MaxHp == boss.Hp, "first boss health");

        // In the open, so no shot leaves the map at once.
        boss.X = 7.5f;
        boss.Y = 8.5f;
        int before = sim.Bullets.Count;
        boss.FireTimer = 0f;
        sim.Tick(Dt, default);
        Check(sim.Bullets.Count - before == 3, "boss fires three shots, fired " + (sim.Bullets.Count - before));

        sim.ApplyBonus(BonusKind.Bomb);
        Check(boss.Alive && boss.Hp == Tuning.BossHp(1) - Tuning.BossBombDamage, "a grenade hurts the boss but does not kill it, hp " + boss.Hp);

        while (boss.Alive) HitEnemy(sim, boss, 2);
        Run(sim, Tuning.ClearDelay + 0.1f);
        Check(sim.Phase == GamePhase.LevelCleared, "beating the boss clears the level, phase " + sim.Phase);

        var real = new GameSim(4);
        real.LoadLevel(9);
        Check(real.IsBossLevel, "level 10 has a boss");
        real.LoadLevel(8);
        Check(!real.IsBossLevel, "level 9 does not");
    }

    static void PartsAreEarned()
    {
        var sim = Sim("RG", (10, 3, 'E'), (10, 7, 'E'));
        WaitForEnemies(sim, 2);
        foreach (Tank enemy in new List<Tank>(sim.Enemies)) HitEnemy(sim, enemy);
        int fromKills = Tuning.EnemyParts(EnemyKind.Raider) + Tuning.EnemyParts(EnemyKind.Gunner);
        Check(sim.PartsEarned == fromKills, "parts for kills, got " + sim.PartsEarned);
        Run(sim, Tuning.ClearDelay + 0.1f);
        Check(sim.PartsEarned == fromKills + Tuning.LevelParts(0), "parts for the level, got " + sim.PartsEarned);
        Check(sim.TakeParts() == fromKills + Tuning.LevelParts(0) && sim.PartsEarned == 0, "taking parts empties the purse");
    }

    static void ClearWave(GameSim sim)
    {
        for (int guard = 0; guard < 60 * 180 && sim.Phase == GamePhase.Playing; guard++)
        {
            sim.Player.ShieldTimer = 9f;
            foreach (Tank enemy in sim.Enemies)
                if (!enemy.Spawning)
                    sim.Bullets.Add(new Bullet { OwnerId = sim.Player.Id, FromPlayer = true, X = enemy.X, Y = enemy.Y, Dir = Dir.Up, Speed = 1f, Damage = 50 });
            sim.Tick(Dt, default);
        }
    }

    static void SurvivalOffersChoices()
    {
        var sim = new GameSim(11);
        sim.StartSurvival(new PlayerStats());
        Check(sim.Mode == GameMode.Survival && sim.Wave == 1, "survival starts on wave 1");
        ClearWave(sim);
        Check(sim.Phase == GamePhase.WaveCleared, "wave should end with a choice, phase " + sim.Phase);
        Check(sim.Choices.Count == 3 && new HashSet<RunUpgrade>(sim.Choices).Count == 3, "three different choices");

        sim.ChooseUpgrade(RunUpgrade.Armor);
        Check(sim.Phase == GamePhase.Playing && sim.Wave == 2, "the next wave starts");
        Check(sim.Stats[Stat.Armor] == 1 && sim.Player.MaxHp == Tuning.BaseHp + 1, "armour from the choice");

        for (int wave = 2; wave < Tuning.BossEvery; wave++)
        {
            ClearWave(sim);
            Check(sim.Phase == GamePhase.WaveCleared, "wave " + wave + " should end, phase " + sim.Phase);
            sim.ChooseUpgrade(RunUpgrade.Gun);
        }
        Check(sim.Wave == Tuning.BossEvery && sim.IsBossLevel, "wave 5 brings a boss");
        Check(sim.Stats[Stat.Gun] == Tuning.BossEvery - 2, "gun upgrades add up");

        // A stat at its top is no longer offered.
        var maxed = new PlayerStats();
        for (int i = 0; i < 4; i++) maxed.Levels[i] = Tuning.SurvivalMaxLevel;
        var top = new GameSim(12);
        top.StartSurvival(maxed);
        ClearWave(top);
        foreach (RunUpgrade choice in top.Choices)
            Check((int)choice >= 4, "a maxed stat was offered: " + choice);
    }

    static void SurvivalBotRuns()
    {
        var rng = new Random(5);
        int bestWave = 0;
        for (int run = 0; run < 20; run++)
        {
            var sim = new GameSim(2000 + run);
            sim.StartSurvival(new PlayerStats());
            Dir? move = Dir.Up;
            for (int ticks = 0; ticks < 60 * 60 * 20; ticks++)
            {
                if (ticks % 30 == 0) move = BotMove(sim, rng);
                sim.Tick(Dt, new PlayerInput { Move = move, Fire = true, Super = true });
                CheckInvariants(sim, run);
                sim.Events.Clear();
                if (sim.Phase == GamePhase.WaveCleared) sim.ChooseUpgrade(sim.Choices[rng.Next(sim.Choices.Count)]);
                else if (sim.Phase != GamePhase.Playing) break;
            }
            bestWave = Math.Max(bestWave, sim.Wave);
        }
        Console.WriteLine("      bot's best survival wave: " + bestWave);
        Check(bestWave >= 2, "the bot should get past the first wave at least once");
    }
    // ---- Garage tanks, new enemies, assault, stars ----

    // A blank test map with the chosen garage tank.
    static GameSim SimWith(PlayerTank tank, string waves, params (int x, int y, char c)[] cells)
    {
        var sim = new GameSim(7);
        sim.StartCampaign(0, new PlayerStats(), tank);
        sim.LoadMap(Blank(cells), waves == "" ? "R" : waves);
        sim.Smartness = 0f;
        return sim;
    }

    static Tank PlaceEnemy(GameSim sim, EnemyKind kind, float x, float y, int hp = 1)
    {
        var enemy = new Tank { Id = 500 + sim.Enemies.Count, Kind = kind, X = x, Y = y, Dir = Dir.Down, Want = Dir.Down, Speed = Tuning.EnemySpeed(kind),
            BulletSpeed = Tuning.EnemyBulletSpeed(kind), Hp = hp, MaxHp = Math.Max(hp, Tuning.EnemyHp(kind)), ThinkTimer = 99f, FireTimer = 99f, MineTimer = 99f, FixTimer = 99f };
        sim.Enemies.Add(enemy);
        return enemy;
    }

    static void TanksChangeNumbers()
    {
        var stats = new PlayerStats();
        var guard = SimWith(PlayerTank.Guard, "");
        var squall = SimWith(PlayerTank.Squall, "");
        var bastion = SimWith(PlayerTank.Bastion, "");
        var phantom = SimWith(PlayerTank.Phantom, "");
        Check(guard.MaxHp == Tuning.BaseHp && guard.Ability == Ability.Strike, "guard is the plain tank");
        Check(squall.MaxHp == Tuning.BaseHp - 1 && squall.PlayerSpeed > guard.PlayerSpeed && squall.PlayerCooldown < guard.PlayerCooldown, "squall: thinner, faster, quicker gun");
        Check(bastion.MaxHp == Tuning.BaseHp + 2 && bastion.PlayerSpeed < guard.PlayerSpeed, "bastion: tougher and slower");
        Check(bastion.Player.Hp == bastion.MaxHp, "bastion spawns with its extra health");
        Check(phantom.SuperNeed == guard.SuperNeed - 1, "phantom charges its ability faster");
        Check(squall.Ability == Ability.Volley && bastion.Ability == Ability.Dome && phantom.Ability == Ability.Dash, "abilities");
        foreach (PlayerTank tank in Enum.GetValues(typeof(PlayerTank)))
            Check(TankDef.For(tank).Name.Length > 0 && TankDef.For(tank).Model.Length > 0, "every tank has a name and a model");
    }

    static void VolleyWorks()
    {
        var sim = SimWith(PlayerTank.Squall, "", (2, 1, '#'), (6, 5, '@'));
        sim.FillSuper();
        sim.Events.Clear();
        sim.Tick(Dt, new PlayerInput { Super = true });
        Check(sim.Events.Exists(e => e.Kind == GameEventKind.Volley), "volley event");
        Check(sim.Bullets.Count == 8 && sim.Bullets.TrueForAll(b => b.Pierce && b.Volley && b.FromPlayer), "eight piercing shells");
        Check(sim.Bullets.Exists(b => b.Dir == Dir.Up) && sim.Bullets.Exists(b => b.Dir == Dir.Down)
            && sim.Bullets.Exists(b => b.Dir == Dir.Left) && sim.Bullets.Exists(b => b.Dir == Dir.Right), "all four ways");
        sim.Tick(Dt, new PlayerInput { Fire = true });
        Check(sim.Bullets.Exists(b => !b.Volley), "the gun still fires while the volley flies");
        Run(sim, 1.5f);
        Check(sim.Tiles[2, 1] == Tile.Empty && sim.Tiles[6, 5] == Tile.Empty, "the volley breaks a crate and concrete");
    }

    static void DomeWorks()
    {
        var sim = SimWith(PlayerTank.Bastion, "");
        Tank close = PlaceEnemy(sim, EnemyKind.Raider, 4.5f, 5.5f);
        Tank far = PlaceEnemy(sim, EnemyKind.Raider, 9.5f, 12.5f);
        sim.Bullets.Add(new Bullet { OwnerId = close.Id, X = 3.5f, Y = 5.5f, Dir = Dir.Left, Speed = 0.1f });
        sim.FillSuper();
        sim.Player.ShieldTimer = 0f;
        sim.Tick(Dt, new PlayerInput { Super = true });
        Check(sim.Player.ShieldTimer > Tuning.DomeShield - 0.1f, "the dome is a shield");
        Check(close.StunTimer > 0f && far.StunTimer <= 0f, "only the close enemy is stunned");
        Check(!sim.Bullets.Exists(b => !b.FromPlayer), "enemy shots inside the dome are gone");
        float x = close.X, y = close.Y;
        close.Want = Dir.Up;
        Run(sim, 1f);
        Check(close.X == x && close.Y == y, "a stunned enemy stands still");
    }

    static void DashAndCloakWork()
    {
        var sim = SimWith(PlayerTank.Phantom, "", (6, 5, '@'));
        sim.Player.Dir = Dir.Right;
        sim.FillSuper();
        sim.Tick(Dt, new PlayerInput { Super = true });
        Check(Math.Abs(sim.Player.X - 5.5f) < 0.15f, "the dash stops at the wall, got " + sim.Player.X);
        Check(sim.CloakTimer > 0f && !sim.PlayerVisible, "cloaked after the dash");
        Tank hunter = PlaceEnemy(sim, EnemyKind.Gunner, 7.5f, 9.5f);
        Check(sim.FindLineShot(hunter) == null, "a cloaked player gives no shot");
        Run(sim, Tuning.CloakTime + 0.1f);
        Check(sim.PlayerVisible, "visible again after the cloak");
        hunter.X = sim.Player.X; hunter.Y = 9.5f;
        Check(sim.FindLineShot(hunter) == Dir.Up, "seen again once the cloak ends");
    }

    static void MinesWork()
    {
        var sim = Sim("");
        Tank miner = PlaceEnemy(sim, EnemyKind.Miner, 6.5f, 9.5f, 2);
        miner.MineTimer = 0f;
        miner.Speed = 0f;
        sim.Tick(Dt, default);
        Check(sim.Mines.Count == 1 && sim.Mines[0].CellX == 6 && sim.Mines[0].CellY == 9, "a mine under the miner");
        sim.Enemies.Clear();

        // The player drives onto the armed mine.
        Run(sim, Tuning.MineArm + 0.1f);
        sim.Player.ShieldTimer = 0f;
        sim.Player.X = 6.5f; sim.Player.Y = 9.3f;
        int hp = sim.Player.Hp;
        sim.Tick(Dt, default);
        Check(sim.Mines.Count == 0 && sim.Player.Hp == hp - 1, "the mine goes off under the player");

        // A shot clears a mine.
        var shot = Sim("");
        Tank other = PlaceEnemy(shot, EnemyKind.Miner, 2.5f, 9.5f, 2);
        other.MineTimer = 0f;
        other.Speed = 0f;
        shot.Tick(Dt, default);
        shot.Enemies.Clear();
        shot.Events.Clear();
        Shoot(shot, Dir.Down);
        Check(shot.Mines.Count == 0, "a shot clears the mine");
        Check(shot.Events.Exists(e => e.Kind == GameEventKind.MineShot), "mine shot event");
    }

    static void FixerMends()
    {
        var sim = Sim("");
        Tank fixer = PlaceEnemy(sim, EnemyKind.Fixer, 6.5f, 10.5f, 2);
        Tank hurt = PlaceEnemy(sim, EnemyKind.Heavy, 7.5f, 11.5f, 1);
        Tank far = PlaceEnemy(sim, EnemyKind.Heavy, 1.5f, 15.5f, 1);
        fixer.Speed = hurt.Speed = far.Speed = 0f;
        fixer.FixTimer = 0f;
        sim.Tick(Dt, default);
        Check(hurt.Hp == 2, "the close heavy is mended, hp " + hurt.Hp);
        Check(far.Hp == 1, "the far heavy is not");
        Run(sim, Tuning.FixEvery * 2f + 0.1f);
        Check(hurt.Hp == 3, "mended up to its full health only, hp " + hurt.Hp);
    }

    static void RammerBlowsUp()
    {
        var sim = Sim("", (6, 12, '#'), (7, 12, '#'));
        Tank rammer = PlaceEnemy(sim, EnemyKind.Rammer, 6.5f, 10.5f);
        rammer.Want = Dir.Down;
        rammer.ThinkTimer = 99f;
        Run(sim, 1.5f);
        Check(!rammer.Alive, "the rammer blew up at the crate");
        Check(sim.Tiles[6, 12] == Tile.Empty, "the crate in front is gone");
        Check(sim.Bullets.Count == 0, "a rammer never fires");

        var near = Sim("");
        near.Player.ShieldTimer = 0f;
        Tank other = PlaceEnemy(near, EnemyKind.Rammer, 2.5f, 7.5f);
        other.Want = Dir.Up;
        int hp = near.Player.Hp;
        Run(near, 1.5f);
        Check(!other.Alive && near.Player.Hp == hp - 1, "it blows up next to the player and hurts it");
    }

    static void AssaultMapsAreWellFormed()
    {
        Check(Levels.Assault.Length == 6, "six assault maps");
        foreach (LevelDef level in Levels.Assault)
        {
            Check(level.Rows.Length == Levels.Height, level.Name + ": rows");
            int cores = 0, players = 0, gates = 0, bases = 0;
            foreach (string row in level.Rows)
            {
                Check(row.Length == Levels.Width, level.Name + ": width");
                foreach (char c in row)
                {
                    Check(".#@%~=CTPE".IndexOf(c) >= 0, level.Name + ": unknown tile " + c);
                    if (c == 'C') cores++;
                    if (c == 'P') players++;
                    if (c == 'E') gates++;
                    if (c == 'B') bases++;
                }
            }
            Check(cores == 1 && players == 1 && gates >= 2 && bases == 0, level.Name + ": markers");
            foreach (char c in level.Waves) Check("RSGHXMFK".IndexOf(c) >= 0, level.Name + ": unknown wave " + c);
        }
        for (int i = 0; i < Levels.Assault.Length; i++)
        {
            var sim = new GameSim(3);
            sim.StartAssault(i, new PlayerStats());
            Check(sim.Mode == GameMode.Assault && sim.CoreHp == Tuning.CoreHp(i), "assault " + i + " starts with its reactor");
            Check(sim.Tiles[sim.BaseX, sim.BaseY] == Tile.Core, "the reactor tile");
            Check(sim.TurretCount > 0 && sim.TurretsLeft == sim.TurretCount, "turrets");
            Check(sim.LevelName.StartsWith("Штурм"), "assault name");
        }
    }

    static void TurretsWork()
    {
        var sim = new GameSim(3);
        sim.StartAssault(0, new PlayerStats());
        Turret turret = sim.Turrets[0];
        sim.Enemies.Clear();
        // Stand straight below the turret with nothing between.
        sim.Player.X = turret.X + 0.5f;
        sim.Player.Y = turret.Y + 3.5f;
        sim.Player.ShieldTimer = 9f;
        for (int y = turret.Y + 1; y <= turret.Y + 3; y++) sim.Tiles[turret.X, y] = Tile.Empty;
        turret.FireTimer = 0f;
        sim.Tick(Dt, default);
        Check(sim.Bullets.Exists(b => b.OwnerId == turret.Id && b.Dir == Dir.Down), "the turret fires down the line");

        for (int hit = 0; hit < Tuning.TurretHp; hit++)
        {
            sim.Bullets.Clear();
            sim.Bullets.Add(new Bullet { OwnerId = sim.Player.Id, FromPlayer = true, X = turret.X + 0.5f, Y = turret.Y + 0.6f, Dir = Dir.Up, Speed = 1f });
            sim.Tick(Dt, default);
        }
        Check(turret.Hp <= 0 && sim.Tiles[turret.X, turret.Y] == Tile.Empty, "three hits break the turret");
        Check(sim.TurretsLeft == sim.TurretCount - 1, "one turret fewer");
    }

    static void CoreWinsAssault()
    {
        var sim = new GameSim(3);
        sim.StartAssault(0, new PlayerStats());
        int x = sim.BaseX, y = sim.BaseY;
        int hp = sim.CoreHp;
        sim.Enemies.Clear();
        sim.Bullets.Add(new Bullet { OwnerId = -5, X = x + 0.5f, Y = y + 0.98f, Dir = Dir.Up, Speed = 1f });
        sim.Tick(Dt, default);
        Check(sim.CoreHp == hp, "enemy shots do not hurt their own reactor");
        for (int guard = 0; sim.CoreHp > 0 && guard < 100; guard++)
        {
            sim.Bullets.Clear();
            sim.Bullets.Add(new Bullet { OwnerId = sim.Player.Id, FromPlayer = true, Damage = 1, X = x + 0.5f, Y = y + 0.98f, Dir = Dir.Up, Speed = 1f });
            sim.Tick(Dt, default);
        }
        Check(sim.Tiles[x, y] == Tile.Empty, "the reactor is gone");
        Run(sim, Tuning.ClearDelay + 0.2f);
        Check(sim.Phase == GamePhase.LevelCleared, "the assault is won, phase " + sim.Phase);
        Check(sim.TakeParts() >= Tuning.AssaultParts(0), "parts for the assault");
        Check(sim.StarsWon >= 1, "at least one star");
        sim.NextLevel();
        Check(sim.LevelIndex == 1 && sim.Mode == GameMode.Assault && sim.CoreHp == Tuning.CoreHp(1), "the next fortress");
    }

    static void StarsAreEarned()
    {
        // A quick clean win: three stars.
        var sim = Sim("R", (10, 2, 'E'));
        WaitForEnemies(sim, 1);
        HitEnemy(sim, sim.Enemies[0]);
        Run(sim, Tuning.ClearDelay + 0.2f);
        Check(sim.Phase != GamePhase.Playing && sim.StarsWon == 3, "three stars, got " + sim.StarsWon);

        // A tank lost: two at most.
        var lost = Sim("R", (10, 2, 'E'));
        lost.Player.ShieldTimer = 0f;
        lost.Player.Hp = 1;
        lost.Bullets.Add(new Bullet { OwnerId = -9, X = lost.Player.X, Y = lost.Player.Y, Dir = Dir.Up, Speed = 1f });
        lost.Tick(Dt, default);
        Check(lost.LivesLost == 1, "a tank lost");
        WaitForEnemies(lost, 1);
        HitEnemy(lost, lost.Enemies[0]);
        Run(lost, Tuning.ClearDelay + 0.2f);
        Check(lost.StarsWon == 2, "two stars after a loss, got " + lost.StarsWon);
    }

    static void AssaultBotRuns()
    {
        var rng = new Random(5);
        int won = 0;
        foreach (PlayerTank tank in Enum.GetValues(typeof(PlayerTank)))
        {
            for (int map = 0; map < Levels.Assault.Length; map++)
            {
                var sim = new GameSim(200 + map);
                var stats = new PlayerStats();
                for (int i = 0; i < 4; i++) stats.Levels[i] = 3;
                sim.StartAssault(map, stats, tank);
                Dir? move = Dir.Up;
                for (int tick = 0; tick < 60 * 120 && sim.Phase == GamePhase.Playing; tick++)
                {
                    if (tick % 40 == 0) move = rng.NextDouble() < 0.6 ? Dir.Up : (Dir)rng.Next(4);
                    if (tick % 300 == 0) sim.FillSuper();
                    sim.Tick(Dt, new PlayerInput { Move = move, Fire = true, Super = tick % 300 == 1 });
                    CheckInvariants(sim, map);
                    Check(sim.CoreHp >= 0 && sim.CoreHp <= sim.CoreMax, "reactor health in range");
                    sim.Events.Clear();
                }
                if (sim.Phase == GamePhase.LevelCleared || sim.Phase == GamePhase.Victory) won++;
            }
        }
        Console.WriteLine("      bot assaults won: " + won + " of " + 4 * Levels.Assault.Length);
    }
}
}
