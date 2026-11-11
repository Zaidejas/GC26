using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// The coin shop, opened from the main menu: boosters for the next battle and block skins.
    /// The rules live in TetrisShop; this part draws the list, takes keys and clicks, and saves.
    /// </summary>
    public sealed partial class TetrisGame
    {
        const string ShopName = "Shop";
        const float ShopMessageLife = 1.6f;

        TetrisShop shop;
        bool inShop;
        int shopIndex;
        readonly float[] shopGlow = new float[TetrisShop.Items.Length + 1];
        string shopMessage = "";
        Color shopMessageColor;
        float shopMessageAge = ShopMessageLife;

        int ShopRows { get { return TetrisShop.Items.Length + 1; } }

        void LoadShop()
        {
            shop = TetrisShop.Parse(PlayerPrefs.GetString(recordsPrefix + ShopName, ""));
            view.SetSkin(shop.Skin);
        }

        void SaveShop()
        {
            if (autoplay || noRecords)
                return;
            PlayerPrefs.SetString(recordsPrefix + ShopName, shop.Serialize());
            PlayerPrefs.SetInt(recordsPrefix + CoinsName, coinsTotal);
            PlayerPrefs.Save();
        }

        void OpenShop()
        {
            Debug.Log("[BlockDrop] Shop opened, coins=" + coinsTotal + " state=" + shop.Serialize());
            inShop = true;
            shopIndex = 0;
            shopMessageAge = ShopMessageLife;
            for (int i = 0; i < shopGlow.Length; i++)
                shopGlow[i] = i == 0 ? 1f : 0f;
            gameAudio.Play("hold");
        }

        void CloseShop()
        {
            Debug.Log("[BlockDrop] Shop closed");
            inShop = false;
            gameAudio.Play("hold", 0.6f);
        }

        void UpdateShop()
        {
            menuClock += Time.deltaTime;
            shopMessageAge += Time.deltaTime;
            for (int i = 0; i < shopGlow.Length; i++)
                shopGlow[i] = Mathf.MoveTowards(shopGlow[i], i == shopIndex ? 1f : 0f, Time.deltaTime * 7f);

            if (Pressed(Key.Escape) || Pressed(Key.Backspace))
            {
                CloseShop();
                return;
            }
            if (Pressed(Key.L))
                SetLanguage(!english);
            if (!HandleButtons(ref shopIndex, ShopRows, HudLayout.ShopTopY, HudLayout.ShopStepY, HudLayout.ShopItemWidth, HudLayout.ShopItemHeight))
                return;

            if (shopIndex == TetrisShop.Items.Length)
            {
                CloseShop();
                return;
            }
            BuyItem(TetrisShop.Items[shopIndex]);
        }

        void BuyItem(ShopItem item)
        {
            BuyResult result = shop.Buy(item, ref coinsTotal);
            Debug.Log("[BlockDrop] Shop: " + ItemName(item) + " -> " + result + ", coins=" + coinsTotal + " state=" + shop.Serialize());
            Color good = new Color(0.55f, 0.9f, 0.5f), bad = new Color(1f, 0.5f, 0.42f);
            switch (result)
            {
                case BuyResult.Bought:
                    gameAudio.Play("coin");
                    ShopSay(item.Kind == ShopItemKind.Booster
                        ? T("Куплено! Сработает в следующем бою", "Bought! It works in your next battle")
                        : T("Куплено и надето!", "Bought and put on!"), good);
                    break;
                case BuyResult.Equipped:
                    gameAudio.Play("hold");
                    ShopSay(T("Скин надет", "Skin put on"), good);
                    break;
                case BuyResult.AlreadyEquipped:
                    ShopSay(T("Этот скин уже надет", "You already wear this skin"), Muted);
                    break;
                case BuyResult.NotEnoughCoins:
                    gameAudio.Play("move", 0.7f, 0.6f);
                    ShopSay(T("Не хватает монет — заработай их в бою", "Not enough coins — earn some in a battle"), bad);
                    break;
                case BuyResult.Full:
                    gameAudio.Play("move", 0.7f, 0.6f);
                    ShopSay(T("Больше " + TetrisShop.MaxBoosters + " не унести", "You can't carry more than " + TetrisShop.MaxBoosters), bad);
                    break;
            }
            if (result == BuyResult.Bought || result == BuyResult.Equipped)
            {
                view.SetSkin(shop.Skin);
                SaveShop();
            }
        }

        void ShopSay(string text, Color color)
        {
            shopMessage = text;
            shopMessageColor = color;
            shopMessageAge = 0f;
        }

        /// <summary>At the start of a battle, one of each booster the player has is used up.</summary>
        BattleBoosts TakeBattleBoosts()
        {
            if (autoplay || shop == null)
                return default;
            BattleBoosts boosts = shop.TakeBoosters();
            if (!boosts.Any)
                return boosts;
            SaveShop();
            Debug.Log("[BlockDrop] Boosts: bomb=" + boosts.StartBomb + " heavy=" + boosts.HeavyHit + " magnet=" + boosts.CoinMagnet + " left=" + shop.Serialize());
            string text = "";
            if (boosts.StartBomb)
                text += T("бомба", "bomb");
            if (boosts.HeavyHit)
                text += (text.Length > 0 ? " · " : "") + T("урон ×1.5", "damage ×1.5");
            if (boosts.CoinMagnet)
                text += (text.Length > 0 ? " · " : "") + T("монеты ×2", "coins ×2");
            AddPopup(PopupSlot.Level, T("БУСТЫ: ", "BOOSTS: ") + text, 6.6f, 0.8f, BannerLife + 1f, 0.4f, new Color(1f, 0.8f, 0.25f));
            return boosts;
        }

        // --- texts ----------------------------------------------------------------------------

        string ItemName(ShopItem item)
        {
            if (item.Kind == ShopItemKind.Skin)
                return T("Скин: ", "Skin: ") + SkinName(item.Skin);
            switch (item.Booster)
            {
                case BoosterKind.StartBomb: return T("Стартовая бомба", "Starting bomb");
                case BoosterKind.HeavyHit: return T("Тяжёлый удар", "Heavy hit");
                default: return T("Магнит для монет", "Coin magnet");
            }
        }

        string ItemHint(ShopItem item)
        {
            if (item.Kind == ShopItemKind.Skin)
                return T("цвета всех блоков", "the colours of every block");
            switch (item.Booster)
            {
                case BoosterKind.StartBomb: return T("первая фигура следующего боя — бомба", "your next battle starts with a bomb");
                case BoosterKind.HeavyHit: return T("+50% урона по боту в следующем бою", "+50% damage to the bot in your next battle");
                default: return T("×2 монеты в следующем бою", "×2 coins in your next battle");
            }
        }

        string SkinName(int skin)
        {
            switch (skin)
            {
                case 0: return T("Классика", "Classic");
                case 1: return T("Пастель", "Pastel");
                case 2: return T("Неон", "Neon");
                default: return T("Золото", "Gold");
            }
        }

        /// <summary>The right-hand side of a row: price, how many the player has, or the skin's state.</summary>
        string ItemStatus(ShopItem item, out Color color)
        {
            Color gold = new Color(1f, 0.8f, 0.25f);
            if (item.Kind == ShopItemKind.Skin && shop.OwnsSkin(item.Skin))
            {
                color = shop.Skin == item.Skin ? new Color(0.55f, 0.9f, 0.5f) : Muted;
                return shop.Skin == item.Skin ? T("надет", "worn") : T("надеть", "wear");
            }
            color = coinsTotal >= item.Price ? gold : new Color(0.75f, 0.55f, 0.35f);
            string price = item.Price + T(" мон.", " coins");
            if (item.Kind == ShopItemKind.Booster && shop.Count(item.Booster) > 0)
                price += "\n" + T("есть: ", "have: ") + shop.Count(item.Booster);
            return price;
        }

        // --- drawing --------------------------------------------------------------------------

        void DrawShop(float unit)
        {
            FillWorld(0f, 0f, 200f, 200f, new Color(0.07f, 0.055f, 0.1f, 1f));
            DrawMenuBackground();

            Color accent = view.AccentColor;
            Label(T("МАГАЗИН", "SHOP"), 0f, HudLayout.ShopTitleY, unit, 1.25f, TextAnchor.MiddleCenter, accent);
            Label(T("Монеты: ", "Coins: ") + coinsTotal.ToString("N0"), 0f, HudLayout.ShopCoinsY, unit, 0.6f, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.25f));

            for (int i = 0; i < TetrisShop.Items.Length; i++)
                DrawShopRow(TetrisShop.Items[i], i, unit);
            DrawButton(T("Назад", "Back"), null, ShopRowY(TetrisShop.Items.Length), HudLayout.ShopItemWidth, HudLayout.ShopItemHeight,
                shopGlow[TetrisShop.Items.Length], 0, unit);

            if (shopMessageAge < ShopMessageLife)
            {
                float alpha = Mathf.Clamp01((ShopMessageLife - shopMessageAge) / 0.4f);
                Label(shopMessage, 0f, HudLayout.ShopMessageY, unit, 0.5f, TextAnchor.MiddleCenter,
                    new Color(shopMessageColor.r, shopMessageColor.g, shopMessageColor.b, alpha));
            }
            Label(T("↑ ↓ выбрать    Enter купить    Esc назад", "↑ ↓ select    Enter buy    Esc back"), 0f, HudLayout.MenuHintY, unit, 0.42f, TextAnchor.MiddleCenter, Muted);
        }

        static float ShopRowY(int index)
        {
            return HudLayout.ShopTopY - index * HudLayout.ShopStepY;
        }

        void DrawShopRow(ShopItem item, int index, float unit)
        {
            float y = ShopRowY(index);
            float width = HudLayout.ShopItemWidth, height = HudLayout.ShopItemHeight;
            float glow = Mathf.SmoothStep(0f, 1f, shopGlow[index]);
            Color accent = view.AccentColor;

            RoundedWorld(0f, y - 0.08f, width, height, new Color(0f, 0f, 0f, 0.4f), 0.35f, 0f, unit);
            RoundedWorld(0f, y, width, height, Color.Lerp(new Color(0.13f, 0.11f, 0.2f, 1f), new Color(0.25f, 0.2f, 0.38f, 1f), glow), 0.35f, 0f, unit);
            if (glow > 0.01f)
                RoundedWorld(0f, y, width, height, new Color(accent.r, accent.g, accent.b, glow), 0.35f, 0.07f, unit);

            // On the left: three tiles in the skin's colours, or the booster's own sign.
            float left = -width / 2f + 0.55f;
            if (item.Kind == ShopItemKind.Skin)
            {
                for (int t = 0; t < 3; t++)
                    DrawTile(left + t * 0.48f, y, 0.42f, BoardView.SkinColor(item.Skin, new[] { 2, 4, 0 }[t]));
            }
            else
            {
                Color sign = item.Booster == BoosterKind.StartBomb ? SpecialIcons.ColorOf(SpecialKind.Bomb)
                    : item.Booster == BoosterKind.HeavyHit ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.8f, 0.25f);
                DrawTile(left + 0.48f, y, 0.62f, sign);
            }

            Color textColor = Color.Lerp(Muted, Bright, glow);
            float textX = HudLayout.Portrait ? -0.35f : 0f;
            Label(ItemName(item), textX, y + 0.2f, unit, HudLayout.Portrait ? 0.5f : 0.56f, TextAnchor.MiddleCenter, textColor);
            Label(ItemHint(item), textX, y - 0.36f, unit, HudLayout.Portrait ? 0.28f : 0.31f, TextAnchor.MiddleCenter, Muted);

            string status = ItemStatus(item, out Color statusColor);
            float statusSize = status.Contains("\n") ? 0.34f : 0.42f;
            Label(status, width / 2f - (HudLayout.Portrait ? 1.1f : 1.5f), y, unit, statusSize, TextAnchor.MiddleCenter, statusColor);
        }
    }
}
