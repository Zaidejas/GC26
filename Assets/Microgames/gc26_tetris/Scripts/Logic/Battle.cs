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

        public BattleHit(int damage, int coins, int points, bool defeated, int botLevel)
        {
            Damage = damage;
            Coins = coins;
            Points = points;
            Defeated = defeated;
            BotLevel = botLevel;
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

        public Battle(float damageBonus = 1f, int coinFactor = 1)
        {
            DamageBonus = Math.Max(1f, damageBonus);
            CoinFactor = Math.Max(1, coinFactor);
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

        public BattleHit Hit(int rows, int combo, float multiplier)
        {
            int level = Level;
            int damage = DamageFor(rows, multiplier * DamageBonus);
            int coins = CoinsFor(rows, combo);
            int points = damage * PointsPerDamage;
            bool defeated = false;

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
            }

            coins *= CoinFactor;
            CoinsEarned += coins;
            return new BattleHit(damage, coins, points, defeated, level);
        }
    }
}
