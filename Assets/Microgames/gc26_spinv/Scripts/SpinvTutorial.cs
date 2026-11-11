using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GC26.Spinv
{
    // Обучение (кнопка TUTORIAL в меню): короткая партия без смерти и без сохранений.
    // Шаги ждут действия игрока: подвигать корабль, поймать монетку, купить каждую способность,
    // поймать сердечко. Подсказки печатаются в окне, жёлтое кольцо показывает, куда смотреть
    public class SpinvTutorial : MonoBehaviour
    {
        [Header("Игра")]
        public SpinvGameManager game;
        public WaveManager waves;
        public InvaderGrid grid;
        public PlayerController player;
        public AbilityManager abilities;
        public Coin coinPrefab;
        [Tooltip("Иконка монет в шапке: её показываем на шаге с монетой")]
        public RectTransform coinCounter;
        public TMP_Text waveText;

        [Header("Подсказки")]
        public GameObject root;
        public TMP_Text tipText;
        public RectTransform ring;
        [Tooltip("Знак «зажми и веди»: кружок-касание со стрелками, ездит под кораблём")]
        public RectTransform dragHint;
        public RectTransform canvasRect;
        public Camera uiCamera;
        public float charTime = 0.03f;

        [Header("Монетный дождь (шаг со способностями)")]
        public int coinRows = 4;
        public int coinsPerRow = 5;
        public float coinSpacing = 0.9f;
        public float rowSpacing = 1.3f;
        public float coinRainY = 13f;

        AbilityButton[] slots;
        WaveData wave;
        Transform worldTarget;
        RectTransform uiTarget;
        float ringSize;
        bool showDrag;
        int lastUsed = -1;
        Coroutine typing, hiding;
        int tipVersion; // меняется с каждой новой подсказкой

        void Start()
        {
            if (!game.IsTutorial)
            {
                root.SetActive(false);
                enabled = false;
                return;
            }
            waveText.text = "-";
            slots = FindObjectsByType<AbilityButton>().OrderBy(s => s.index).ToArray();
            abilities.Used += OnUsed;
            ring.gameObject.SetActive(false);
            dragHint.gameObject.SetActive(false);
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (abilities != null) abilities.Used -= OnUsed;
            if (wave != null) Destroy(wave);
        }

        void OnUsed(int index) => lastUsed = index;

        IEnumerator Run()
        {
            yield return null;

            // 1. Управление: пока игрок сам не проведёт кораблём, знак показывает «зажми и веди»
            Say("<color=#39FF6A>HOLD</color> and drag your finger to move the ship");
            showDrag = true;
            float startX = player.transform.position.x;
            while (Mathf.Abs(player.transform.position.x - startX) < 2f) yield return null;
            showDrag = false;
            yield return new WaitForSeconds(0.6f);

            // 2. Стрельба сама, сверху медленный строй, который не стреляет
            SpawnInvaders();
            Say("Shooting is <color=#39FF6A>AUTOMATIC</color>. Destroy the invaders!");
            HideTipAfter(4f); // табличка не мешает целиться
            while (FindAnyObjectByType<Coin>() == null)
            {
                if (NoInvaders()) SpawnInvaders();
                yield return null;
            }

            // 3. Монетка: подсвечиваем падающую, ждём, пока поймают
            Say("This is a <color=#FFD45A>COIN</color>. Invaders drop them - catch it with the ship!");
            while (game.Coins < 1)
            {
                Coin coin = FindAnyObjectByType<Coin>();
                PointAt(coin != null ? coin.transform : null, 200f);
                if (coin == null && NoInvaders()) SpawnInvaders();
                yield return null;
            }
            PointAtUI(coinCounter, 230f);
            Say("Your <color=#FFD45A>COINS</color> are counted here");
            yield return new WaitForSeconds(2.5f);

            // 4. Способности покупаются за монеты: щит
            yield return BuyAbility(0, "Abilities cost <color=#FFD45A>COINS</color> - the price is on the coin. Tap the <color=#39FF6A>SHIELD</color>!");
            Say("The shield blocks enemy shots for a moment");
            while (abilities.abilities[0].IsActive) yield return null;

            // 5. Монетный дождь полосками и две оставшиеся способности
            ClearPointer();
            Say("Here are more coins - catch them!");
            CoinRain();
            yield return new WaitForSeconds(0.5f);
            while (FindAnyObjectByType<Coin>() != null) yield return null;
            // Бункеры сначала повреждены: игрок видит, что именно чинит ремонт
            DamageBunkers();
            Say("The <color=#39FF6A>BUNKERS</color> got damaged!");
            yield return new WaitForSeconds(2f);
            yield return BuyAbility(1, "Tap <color=#39FF6A>REPAIR</color> - it rebuilds the bunkers");
            yield return new WaitForSeconds(0.5f);
            yield return BuyAbility(2, "Tap <color=#39FF6A>RAPID FIRE</color> - you shoot twice as fast");
            ClearPointer();
            yield return new WaitForSeconds(1f);

            // 6. Сердечко - только когда кончились щит и ускоренная стрельба
            Say("Rapid fire is on - wait until it ends");
            while (abilities.abilities[0].IsActive || abilities.abilities[2].IsActive) yield return null;
            Say("Ouch! You lost a life...");
            player.Hit();
            yield return new WaitForSeconds(1.6f);
            Say("A <color=#FF5A6E>HEART</color> restores a life. Catch it!");
            while (game.Lives < game.startLives)
            {
                HealthPickup heart = FindAnyObjectByType<HealthPickup>();
                if (heart == null) waves.SpawnHeal(player.transform.position.x);
                PointAt(heart != null ? heart.transform : null, 200f);
                yield return null;
            }
            ClearPointer();

            // 7. Конец: по касанию - в меню
            Say("<color=#39FF6A>TUTORIAL COMPLETE</color> - tap to return to the menu");
            yield return WaitForTap();
            FindAnyObjectByType<SpinvMicrogame>().ToMenu();
        }

        // Дать монет на способность, если не хватает, подсветить её ячейку и ждать нажатия
        IEnumerator BuyAbility(int index, string tip)
        {
            int cost = abilities.abilities[index].cost;
            if (game.Coins < cost) game.AddCoins(cost - game.Coins);
            lastUsed = -1;
            PointAtUI((RectTransform)slots[index].transform, 270f);
            Say(tip);
            while (lastUsed != index) yield return null;
            ClearPointer();
        }

        void SpawnInvaders()
        {
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveData>();
                wave.rows = 1;
                wave.columns = 4;
                wave.speed = 0.7f;
                wave.missileInterval = 9999f; // не стреляют
                wave.coinDropChance = 1f;     // монета из каждого
            }
            grid.Build(wave);
        }

        bool NoInvaders() => FindAnyObjectByType<Invader>() == null;

        [Header("Повреждение бункеров перед ремонтом")]
        public int holesPerBunker = 3;
        public float holeRadius = 0.32f;

        // В каждом бункере несколько круглых пробоин, как от ракет
        void DamageBunkers()
        {
            BunkerBlock[] blocks = FindObjectsByType<BunkerBlock>();
            foreach (var bunker in blocks.GroupBy(b => b.transform.parent))
            {
                BunkerBlock[] own = bunker.ToArray();
                for (int h = 0; h < holesPerBunker; h++)
                {
                    Vector3 center = own[Random.Range(0, own.Length)].transform.position;
                    foreach (BunkerBlock block in own)
                    {
                        if (block.gameObject.activeSelf && Vector2.Distance(block.transform.position, center) < holeRadius)
                            block.Break();
                    }
                }
            }
        }

        // 4 полоски по 5 монет над кораблём
        void CoinRain()
        {
            float centerX = Mathf.Clamp(player.transform.position.x, -3f, 3f);
            for (int row = 0; row < coinRows; row++)
            {
                for (int i = 0; i < coinsPerRow; i++)
                {
                    float x = centerX + (i - (coinsPerRow - 1) / 2f) * coinSpacing;
                    Instantiate(coinPrefab, new Vector3(x, coinRainY + row * rowSpacing, 0f), Quaternion.identity);
                }
            }
        }

        IEnumerator WaitForTap()
        {
            bool wasPressed = true; // палец, которым ловили сердечко, сначала надо отпустить
            while (true)
            {
                bool pressed = GameInput.PointerHeld(out _);
                if (pressed && !wasPressed) yield break;
                wasPressed = pressed;
                yield return null;
            }
        }

        void Say(string text)
        {
            tipVersion++;
            if (hiding != null) StopCoroutine(hiding);
            tipText.transform.parent.gameObject.SetActive(true);
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(Typewriter.Type(tipText, text, charTime));
        }

        // Убрать табличку через seconds, если за это время не появилась новая подсказка
        void HideTipAfter(float seconds)
        {
            hiding = StartCoroutine(HideTip(tipVersion, seconds));
        }

        IEnumerator HideTip(int version, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (version == tipVersion) tipText.transform.parent.gameObject.SetActive(false);
        }

        void PointAt(Transform target, float size)
        {
            worldTarget = target;
            uiTarget = null;
            ringSize = size;
        }

        void PointAtUI(RectTransform target, float size)
        {
            uiTarget = target;
            worldTarget = null;
            ringSize = size;
        }

        void ClearPointer()
        {
            worldTarget = null;
            uiTarget = null;
        }

        void LateUpdate()
        {
            // У элементов интерфейса точка привязки бывает на краю: берём центр прямоугольника
            Vector3? world = worldTarget != null ? worldTarget.position
                : uiTarget != null ? uiTarget.TransformPoint(uiTarget.rect.center)
                : (Vector3?)null;
            ring.gameObject.SetActive(world.HasValue);
            if (world.HasValue)
            {
                ring.anchoredPosition = ToCanvas(world.Value);
                float pulse = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f);
                ring.sizeDelta = Vector2.one * ringSize * pulse;
            }

            dragHint.gameObject.SetActive(showDrag);
            if (showDrag)
            {
                Vector2 ship = ToCanvas(player.transform.position);
                dragHint.anchoredPosition = ship + new Vector2(Mathf.Sin(Time.unscaledTime * 2.4f) * 260f, -190f);
            }
        }

        // Мировая точка (или точка интерфейса на этой же камере) -> координаты на канвасе
        Vector2 ToCanvas(Vector3 world)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local);
            return local;
        }
    }
}
