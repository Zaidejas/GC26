using System;
using System.Globalization;

namespace GC26.Tetris.Logic
{
    public enum ShopItemKind { Booster, Skin }

    public enum BoosterKind { StartBomb, HeavyHit, CoinMagnet }

    public enum BuyResult { Bought, Equipped, AlreadyEquipped, NotEnoughCoins, Full }

    public sealed class ShopItem
    {
        public readonly ShopItemKind Kind;
        public readonly BoosterKind Booster;
        public readonly int Skin;
        public readonly int Price;

        ShopItem(ShopItemKind kind, BoosterKind booster, int skin, int price)
        {
            Kind = kind;
            Booster = booster;
            Skin = skin;
            Price = price;
        }

        public static ShopItem ForBooster(BoosterKind booster, int price)
        {
            return new ShopItem(ShopItemKind.Booster, booster, 0, price);
        }

        public static ShopItem ForSkin(int skin, int price)
        {
            return new ShopItem(ShopItemKind.Skin, BoosterKind.StartBomb, skin, price);
        }
    }

    /// <summary>Boosters wait for the next battle and are used up there.</summary>
    public readonly struct BattleBoosts
    {
        public readonly bool StartBomb;
        public readonly bool HeavyHit;
        public readonly bool CoinMagnet;

        public BattleBoosts(bool startBomb, bool heavyHit, bool coinMagnet)
        {
            StartBomb = startBomb;
            HeavyHit = heavyHit;
            CoinMagnet = coinMagnet;
        }

        public bool Any { get { return StartBomb || HeavyHit || CoinMagnet; } }
        public float DamageBonus { get { return HeavyHit ? 1.5f : 1f; } }
        public int CoinFactor { get { return CoinMagnet ? 2 : 1; } }
    }

    /// <summary>
    /// What the player bought with coins: boosters for the next battle and block skins.
    /// Skin 0 (classic) is always owned. The state is saved as one short text line.
    /// </summary>
    public sealed class TetrisShop
    {
        public const int MaxBoosters = 9;
        public const int SkinCount = 4;
        const int BoosterKinds = 3;

        /// <summary>Everything on sale, in the order the shop shows it.</summary>
        public static readonly ShopItem[] Items =
        {
            ShopItem.ForBooster(BoosterKind.StartBomb, 25),
            ShopItem.ForBooster(BoosterKind.HeavyHit, 40),
            ShopItem.ForBooster(BoosterKind.CoinMagnet, 40),
            ShopItem.ForSkin(0, 0),
            ShopItem.ForSkin(1, 80),
            ShopItem.ForSkin(2, 120),
            ShopItem.ForSkin(3, 200),
        };

        readonly int[] boosters = new int[BoosterKinds];
        readonly bool[] skins = { true, false, false, false };

        public int Skin { get; private set; }

        public int Count(BoosterKind kind)
        {
            return boosters[(int)kind];
        }

        public bool OwnsSkin(int skin)
        {
            return skin >= 0 && skin < SkinCount && skins[skin];
        }

        /// <summary>Buys a booster, or buys and puts on a skin; a skin already owned is just put on, for free.</summary>
        public BuyResult Buy(ShopItem item, ref int coins)
        {
            if (item.Kind == ShopItemKind.Skin && OwnsSkin(item.Skin))
            {
                if (Skin == item.Skin)
                    return BuyResult.AlreadyEquipped;
                Skin = item.Skin;
                return BuyResult.Equipped;
            }
            if (item.Kind == ShopItemKind.Booster && Count(item.Booster) >= MaxBoosters)
                return BuyResult.Full;
            if (coins < item.Price)
                return BuyResult.NotEnoughCoins;

            coins -= item.Price;
            if (item.Kind == ShopItemKind.Booster)
            {
                boosters[(int)item.Booster]++;
            }
            else
            {
                skins[item.Skin] = true;
                Skin = item.Skin;
            }
            return BuyResult.Bought;
        }

        /// <summary>Takes one of each booster the player has, for the battle that is starting.</summary>
        public BattleBoosts TakeBoosters()
        {
            bool[] taken = new bool[BoosterKinds];
            for (int i = 0; i < BoosterKinds; i++)
            {
                if (boosters[i] > 0)
                {
                    boosters[i]--;
                    taken[i] = true;
                }
            }
            return new BattleBoosts(taken[0], taken[1], taken[2]);
        }

        /// <summary>"b=1,0,2;s=1,1,0,0;k=1": booster counts, owned skins, skin in use.</summary>
        public string Serialize()
        {
            string owned = "";
            for (int i = 0; i < SkinCount; i++)
                owned += (i > 0 ? "," : "") + (skins[i] ? "1" : "0");
            return "b=" + string.Join(",", Array.ConvertAll(boosters, n => n.ToString(CultureInfo.InvariantCulture))) +
                   ";s=" + owned + ";k=" + Skin.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reads what Serialize wrote. Anything damaged or impossible is ignored, never thrown.</summary>
        public static TetrisShop Parse(string text)
        {
            var shop = new TetrisShop();
            if (string.IsNullOrEmpty(text))
                return shop;
            int wanted = 0;
            foreach (string part in text.Split(';'))
            {
                string[] pair = part.Split('=');
                if (pair.Length != 2)
                    continue;
                string[] values = pair[1].Split(',');
                if (pair[0] == "b")
                {
                    for (int i = 0; i < BoosterKinds && i < values.Length; i++)
                    {
                        if (int.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                            shop.boosters[i] = Math.Max(0, Math.Min(MaxBoosters, count));
                    }
                }
                else if (pair[0] == "s")
                {
                    for (int i = 1; i < SkinCount && i < values.Length; i++)
                        shop.skins[i] = values[i] == "1";
                }
                else if (pair[0] == "k")
                {
                    int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out wanted);
                }
            }
            // A skin that is not owned can't be worn, whatever the order of the saved parts.
            shop.Skin = shop.OwnsSkin(wanted) ? wanted : 0;
            return shop;
        }
    }
}
