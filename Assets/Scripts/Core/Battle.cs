using System;

namespace BlockDrop.Core
{
    /// <summary>What one clear did to the bot.</summary>
    public readonly struct BattleHit
    {
        public readonly int Damage;
        public readonly int Coins;
        public readonly bool Defeated;
        /// <summary>The bot that took the hit, counted from 1.</summary>
        public readonly int BotLevel;

        public BattleHit(int damage, int coins, bool defeated, int botLevel)
        {
            Damage = damage;
            Coins = coins;
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

        static readonly int[] LineDamage = { 0, 10, 25, 45, 80 };

        public int Level { get; private set; } = 1;
        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public int BotsDefeated { get; private set; }
        public int CoinsEarned { get; private set; }

        public Battle()
        {
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
            int damage = DamageFor(rows, multiplier);
            int coins = CoinsFor(rows, combo);
            bool defeated = false;

            Hp -= damage;
            if (damage > 0 && Hp <= 0)
            {
                // A new bot always starts fresh: leftover damage does not carry over.
                defeated = true;
                BotsDefeated++;
                coins += DefeatCoinsPerLevel * level;
                Level++;
                MaxHp = MaxHpFor(Level);
                Hp = MaxHp;
            }

            CoinsEarned += coins;
            return new BattleHit(damage, coins, defeated, level);
        }
    }
}
