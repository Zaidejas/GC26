using System;

namespace GC26.Tetris.Logic
{
    /// <summary>What one clear did to the bot.</summary>
    public readonly struct BattleHit
    {
        public readonly int Damage;
        public readonly int Coins;
        /// <summary>Bonus score for the hit, on top of the points for the lines themselves.</summary>
        public readonly int Points;
        public readonly bool Defeated;
        /// <summary>The bot that took the hit, counted from 1.</summary>
        public readonly int BotLevel;
        /// <summary>Garbage rows of a coming attack this clear cancelled.</summary>
        public readonly int Blocked;
        /// <summary>True when the clear (or the defeat) called off the whole coming attack.</summary>
        public readonly bool AttackStopped;

        public BattleHit(int damage, int coins, int points, bool defeated, int botLevel, int blocked = 0, bool attackStopped = false)
        {
            Damage = damage;
            Coins = coins;
            Points = points;
            Defeated = defeated;
            BotLevel = botLevel;
            Blocked = blocked;
            AttackStopped = attackStopped;
        }
    }

    public enum BotAttackKind { None, Warning, Landed }

    /// <summary>What the bot did this frame: started to warn, or pushed garbage rows in.</summary>
    public readonly struct BotAttack
    {
        public readonly BotAttackKind Kind;
        public readonly int Rows;
        /// <summary>The column left open in every garbage row.</summary>
        public readonly int Hole;

        public BotAttack(BotAttackKind kind, int rows, int hole)
        {
            Kind = kind;
            Rows = rows;
            Hole = hole;
        }
    }

    /// <summary>
    /// The fight against a line of bots. Cleared lines hurt the current bot, the combo multiplies
    /// that damage and raises the coins it drops. Each defeated bot is replaced by a stronger one.
    /// </summary>
    public sealed class Battle
    {
        public const int BaseHp = 120;
        public const int HpPerLevel = 60;
        public const int DefeatCoinsPerLevel = 10;
        public const int MaxComboCoins = 5;
        public const int PointsPerDamage = 2;
        public const int DefeatPointsPerLevel = 500;

        static readonly int[] LineDamage = { 0, 10, 25, 45, 80 };

        public int Level { get; private set; } = 1;
        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public int BotsDefeated { get; private set; }
        public int CoinsEarned { get; private set; }

        /// <summary>Damage multiplier from the "heavy hit" booster (1 = none).</summary>
        public float DamageBonus { get; }

        /// <summary>Coin multiplier from the "coin magnet" booster (1 = none).</summary>
        public int CoinFactor { get; }

        /// <summary>Seconds of warning before garbage rows come in.</summary>
        public const float WarningTime = 3f;

        readonly Random random;
        float attackTimer;

        /// <summary>True while the bot is about to attack: lines cleared now cancel garbage rows.</summary>
        public bool Warning { get; private set; }
        /// <summary>Garbage rows the coming attack will push in.</summary>
        public int PendingRows { get; private set; }
        /// <summary>Seconds until the next attack lands.</summary>
        public float TimeToAttack { get { return Math.Max(0f, attackTimer); } }

        /// <summary>Stronger bots attack more often: 20 s for the first, 2 s less for each next one, never under 9 s.</summary>
        public static float AttackInterval(int level)
        {
            return Math.Max(9f, 22f - 2f * level);
        }

        /// <summary>One row for bots 1-2, two for 3-4, three for 5-6, then four.</summary>
        public static int AttackRows(int level)
        {
            return Math.Min(4, 1 + (level - 1) / 2);
        }

        public Battle(float damageBonus = 1f, int coinFactor = 1, int seed = 0)
        {
            DamageBonus = Math.Max(1f, damageBonus);
            CoinFactor = Math.Max(1, coinFactor);
            random = new Random(seed);
            attackTimer = AttackInterval(Level);
            MaxHp = MaxHpFor(Level);
            Hp = MaxHp;
        }

        public static int MaxHpFor(int level)
        {
            return BaseHp + HpPerLevel * (level - 1);
        }

        public static int DamageFor(int rows, float multiplier)
        {
            return (int)Math.Round(LineDamage[Math.Max(0, Math.Min(rows, 4))] * multiplier);
        }

        /// <summary>One coin per line, and one more for each step of the combo after the first.</summary>
        public static int CoinsFor(int rows, int combo)
        {
            if (rows <= 0)
                return 0;
            return rows + Math.Min(Math.Max(0, combo - 1), MaxComboCoins);
        }

        /// <summary>Runs the bot's attack clock. Call every frame while the game is being played.</summary>
        public BotAttack Tick(float deltaTime)
        {
            attackTimer -= deltaTime;
            if (!Warning && attackTimer <= WarningTime)
            {
                Warning = true;
                PendingRows = AttackRows(Level);
                return new BotAttack(BotAttackKind.Warning, PendingRows, -1);
            }
            if (Warning && attackTimer <= 0f)
            {
                int rows = PendingRows;
                StopAttack();
                return new BotAttack(BotAttackKind.Landed, rows, random.Next(Board.Width));
            }
            return default;
        }

        void StopAttack()
        {
            Warning = false;
            PendingRows = 0;
            attackTimer = AttackInterval(Level);
        }

        public BattleHit Hit(int rows, int combo, float multiplier)
        {
            int level = Level;
            int damage = DamageFor(rows, multiplier * DamageBonus);
            int coins = CoinsFor(rows, combo);
            int points = damage * PointsPerDamage;
            bool defeated = false;

            // Lines cleared while the bot is about to attack each cancel one garbage row.
            int blocked = 0;
            bool stopped = false;
            if (Warning && rows > 0)
            {
                blocked = Math.Min(rows, PendingRows);
                PendingRows -= blocked;
                if (PendingRows == 0)
                {
                    StopAttack();
                    stopped = true;
                }
            }

            Hp -= damage;
            if (damage > 0 && Hp <= 0)
            {
                // A new bot always starts fresh: leftover damage does not carry over.
                defeated = true;
                BotsDefeated++;
                coins += DefeatCoinsPerLevel * level;
                points += DefeatPointsPerLevel * level;
                Level++;
                MaxHp = MaxHpFor(Level);
                Hp = MaxHp;
                // A new bot starts its own clock, and the old one's attack never comes.
                stopped = stopped || Warning;
                StopAttack();
            }

            coins *= CoinFactor;
            CoinsEarned += coins;
            return new BattleHit(damage, coins, points, defeated, level, blocked, stopped);
        }
    }
}
