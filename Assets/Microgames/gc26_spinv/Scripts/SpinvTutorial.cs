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

        [Header("Обучение аркады")]
        [Tooltip("Счёт в шапке: на шаге про циклы показываем на него")]
        public RectTransform scoreCounter;
        [Tooltip("Скорость пришельца-примера на волне с меткой")]
        public float tagMobSpeed = 1.5f;
        [Tooltip("Скорость пришельца на метке FAST (нарочно заметно быстрее)")]
        public float fastMobSpeed = 3.5f;

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

        void Awake()
        {
            // В обучении аркады корабль не стреляет с самого первого кадра (Update игрока идёт раньше нашего Start)
            var flow = FindAnyObjectByType<SpinvMicrogame>();
            if (flow != null && flow.ArcadeTutorialRun && player != null)
                player.canShoot = false;
        }

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
            // Способности доступны только на своём шаге: иначе можно потратить монеты не на ту и застрять
            abilities.onlyAllowed = -2;
            ring.gameObject.SetActive(false);
            dragHint.gameObject.SetActive(false);
            StartCoroutine(waves.IsArcadeTutorial ? RunArcade() : Run());
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

            // Пояснения ждут касания (TAP TO CONTINUE); подсказки про способности - нажатия способности

            // 1. Управление: прочитал - тап, дальше пока игрок сам не проведёт кораблём, знак показывает «зажми и веди»
            yield return Next("<color=#39FF6A>HOLD</color> and drag your finger to move the ship");
            Say("<color=#39FF6A>HOLD</color> and drag your finger to move the ship");
            showDrag = true;
            float startX = player.transform.position.x;
            while (Mathf.Abs(player.transform.position.x - startX) < 2f) yield return null;
            showDrag = false;
            yield return new WaitForSeconds(0.6f);

            // 2. Стрельба сама, сверху медленный строй, который не стреляет
            yield return Next("Shooting is <color=#39FF6A>AUTOMATIC</color>. Destroy the invaders!");
            SpawnInvaders();
            HideTip(); // табличка не мешает целиться
            while (FindAnyObjectByType<Coin>() == null)
            {
                if (NoInvaders()) SpawnInvaders();
                yield return null;
            }

            // 3. Монетка: подсвечиваем падающую, ждём, пока поймают
            Coin firstCoin = FindAnyObjectByType<Coin>();
            PointAt(firstCoin != null ? firstCoin.transform : null, 200f);
            yield return Next("This is a <color=#FFD45A>COIN</color>. Invaders drop them - catch it with the ship!");
            Say("Catch the <color=#FFD45A>COIN</color>!");
            while (game.Coins < 1)
            {
                Coin coin = FindAnyObjectByType<Coin>();
                PointAt(coin != null ? coin.transform : null, 200f);
                if (coin == null && NoInvaders()) SpawnInvaders();
                yield return null;
            }
            PointAtUI(coinCounter, 230f);
            yield return Next("Your <color=#FFD45A>COINS</color> are counted here");

            // 4. Способности покупаются за монеты: щит
            yield return BuyAbility(0, "Abilities cost <color=#FFD45A>COINS</color> - the price is on the coin. Tap the <color=#39FF6A>SHIELD</color>!");
            Say("The shield blocks enemy shots for a moment");
            while (abilities.abilities[0].IsActive) yield return null;

            // 5. Монетный дождь полосками и две оставшиеся способности
            ClearPointer();
            yield return Next("Here are more coins - catch them!");
            Say("Catch the <color=#FFD45A>COINS</color>!");
            CoinRain();
            yield return new WaitForSeconds(0.5f);
            while (FindAnyObjectByType<Coin>() != null) yield return null;
            // Бункеры сначала повреждены: игрок видит, что именно чинит ремонт
            DamageBunkers();
            yield return Next("The <color=#39FF6A>BUNKERS</color> got damaged!");
            yield return BuyAbility(1, "Tap <color=#39FF6A>REPAIR</color> - it rebuilds the bunkers");
            yield return new WaitForSeconds(0.5f);
            yield return BuyAbility(2, "Tap <color=#39FF6A>RAPID FIRE</color> - you shoot twice as fast");
            ClearPointer();
            yield return new WaitForSeconds(1f);

            // 6. Сердечко - только когда кончились щит и ускоренная стрельба
            Say("Rapid fire is on - wait until it ends");
            while (abilities.abilities[0].IsActive || abilities.abilities[2].IsActive) yield return null;
            player.Hit();
            yield return Next("Ouch! You lost a life...");
            yield return Next("A <color=#FF5A6E>HEART</color> restores a life. Catch it!");
            Say("Catch the <color=#FF5A6E>HEART</color>!");
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

        // Обучение аркады (кнопка «?»): управление не объясняем. Волны идут с номерами, как в аркаде,
        // на каждой метке - один пришелец-пример, который не стреляет, и сама механика метки.
        // Пояснения сменяются по касанию; пришелец выходит после касания, дальше - когда его собьют.
        // Волна 5 - надпись BOSS и показ четырёх боссов, волна 6 - LOOP 2
        IEnumerator RunArcade()
        {
            yield return null;
            player.canShoot = false; // здесь корабль не стреляет: пришельцы только показывают метки

            yield return Next("In <color=#FFD45A>ARCADE</color> the waves never end. Survive as long as you can!");

            // 1. FAST и счётчик волн
            waves.BeginTutorialWave(1, WaveModifier.Fast);
            PointAtUI(waveText.rectTransform, 200f);
            yield return Next("The <color=#39FF6A>WAVE</color> counter never stops");
            ClearPointer();
            yield return TagStep("Most waves get a <color=#FFD45A>TAG</color>. <color=#FFD45A>FAST</color>: invaders move faster", fastMobSpeed, false, false);

            // 2. ELITE
            waves.BeginTutorialWave(2, WaveModifier.Elite);
            yield return TagStep("<color=#FFD45A>ELITE</color>: every invader is elite and shoots more, but dies in one hit", tagMobSpeed, true, false);

            // 3. DARK: игрок моргает, пока метка действует
            waves.BeginTutorialWave(3, WaveModifier.Dark);
            yield return TagStep("<color=#FFD45A>DARK</color>: you get sleepy and blink", tagMobSpeed, false, false);

            // 4. RICH: из пришельца сыплются монетки, их можно ловить
            waves.BeginTutorialWave(4, WaveModifier.Rich);
            yield return TagStep("<color=#FFD45A>RICH</color>: coins drop 3x more often", tagMobSpeed, false, true);

            // 5. Босс: надпись, потом по очереди все боссы аркады
            waves.EndTutorialWave();
            waves.BeginTutorialWave(5, WaveModifier.None);
            yield return Next("Every <color=#FFD45A>5th</color> wave is a <color=#FF5A6E>BOSS</color> - one of four, picked at random");
            HideTip();
            if (waves.arcadeBosses != null)
                foreach (Boss prefab in waves.arcadeBosses)
                    if (prefab != null) yield return ShowBoss(prefab);

            // 6. Новый цикл и NO BUNKERS
            waves.BeginTutorialWave(6, WaveModifier.NoBunkers);
            yield return Next("After the boss a new <color=#FFD45A>LOOP</color> begins");
            PointAtUI(scoreCounter, 260f);
            yield return Next("Each loop invaders get faster and tougher, but <color=#FFD45A>POINTS</color> grow: x1, x1.5, x2...");
            ClearPointer();
            yield return TagStep("<color=#FFD45A>NO BUNKERS</color>: no cover this wave", tagMobSpeed, false, false);
            waves.EndTutorialWave();

            Say("<color=#39FF6A>TUTORIAL COMPLETE</color> - tap to return to the menu");
            yield return WaitForTap();
            FindAnyObjectByType<SpinvMicrogame>().ToMenu();
        }

        // Пояснение, которое ждёт касания: снизу приписка TAP TO CONTINUE
        // retype = false: текст уже на экране, приписка появляется сразу, без повторной печати
        IEnumerator Next(string text, bool retype = true)
        {
            string full = text + "\n<size=70%><color=#2F7A45>TAP TO CONTINUE</color></size>";
            if (retype)
            {
                Say(full);
            }
            else
            {
                if (typing != null) StopCoroutine(typing);
                tipText.text = full;
                tipText.maxVisibleCharacters = 99999;
            }
            yield return new WaitForSeconds(0.3f); // случайное двойное касание не проскакивает пункт
            yield return WaitForTap();
        }

        void HideTip()
        {
            tipVersion++;
            if (typing != null) StopCoroutine(typing);
            tipText.transform.parent.gameObject.SetActive(false);
        }

        [Header("Показ боссов (шаг 5 обучения аркады)")]
        public float bossEnterTime = 0.8f;
        [Tooltip("Сколько босс стоит до приступа ярости")]
        public float bossFitDelay = 0.3f;
        public float bossExitTime = 0.6f;

        // Босс-пример: выезжает сверху, один раз наливается красным и трясётся (BossAnger) и уезжает.
        // Не атакует и не двигается сам (компонент Boss выключен до своего Start), в него нельзя попасть
        IEnumerator ShowBoss(Boss prefab)
        {
            Vector3 stand = new Vector3(0f, waves.bossSpawnY, 0f);
            Vector3 above = stand + Vector3.up * 9f;
            Boss boss = Instantiate(prefab, above, Quaternion.identity);
            boss.enabled = false;
            foreach (Collider2D c in boss.GetComponentsInChildren<Collider2D>()) c.enabled = false;
            var anger = boss.GetComponent<BossAnger>();
            float fitLength = 1.2f;
            if (anger != null)
            {
                anger.firstFitDelay = bossEnterTime + bossFitDelay;
                anger.intervalFullHealth = anger.intervalMin = 999f; // только один приступ
                fitLength = anger.fitDuration;
            }

            yield return Slide(boss.transform, above, stand, bossEnterTime);
            yield return new WaitForSeconds(bossFitDelay + fitLength + 0.2f);
            yield return Slide(boss.transform, stand, above, bossExitTime);
            Destroy(boss.gameObject);
        }

        static IEnumerator Slide(Transform target, Vector3 from, Vector3 to, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                if (target == null) yield break;
                float k = t / time;
                target.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k)); // с замедлением в конце
                yield return null;
            }
            if (target != null) target.position = to;
        }

        [Header("Пришелец-пример (обучение аркады)")]
        [Tooltip("Сколько секунд пришелец-пример летает, прежде чем взорваться")]
        public float mobShowTime = 5f;
        [Tooltip("RICH: монетка из пришельца раз в столько секунд")]
        public float richCoinInterval = 0.8f;

        // Метка на примере: выходит один пришелец (не стреляет, корабль тоже), висит пояснение.
        // Пришелец летает mobShowTime секунд и взрывается сам, после этого касание ведёт дальше
        IEnumerator TagStep(string text, float speed, bool elite, bool rich)
        {
            if (wave == null) wave = ScriptableObject.CreateInstance<WaveData>();
            wave.rows = 1;
            wave.columns = 1;
            wave.speed = speed;
            wave.missileInterval = 9999f; // не стреляет
            wave.coinDropChance = 0f;
            wave.allElite = elite;
            grid.Build(wave);

            Say(text);
            Coroutine watch = StartCoroutine(WatchMob(rich));
            yield return new WaitForSeconds(mobShowTime);
            StopCoroutine(watch);
            Invader mob = FindAnyObjectByType<Invader>();
            if (mob != null) mob.Kill();
            yield return Next(text, false);
        }

        // Пока висит пояснение: пролетел мимо корабля - выпускаем заново сверху; на RICH сыплются монетки
        IEnumerator WatchMob(bool rich)
        {
            float coinTimer = 0.5f;
            while (true)
            {
                yield return null;
                Invader mob = FindAnyObjectByType<Invader>();
                if (mob == null) continue;
                if (mob.transform.position.y < player.transform.position.y + 1f)
                {
                    grid.Build(wave);
                    continue;
                }
                if (!rich) continue;
                coinTimer -= Time.deltaTime;
                if (coinTimer <= 0f)
                {
                    coinTimer = richCoinInterval;
                    Instantiate(coinPrefab, mob.transform.position, Quaternion.identity);
                }
            }
        }

        // Дать монет на способность, если не хватает, подсветить её ячейку и ждать нажатия
        IEnumerator BuyAbility(int index, string tip)
        {
            int cost = abilities.abilities[index].cost;
            if (game.Coins < cost) game.AddCoins(cost - game.Coins);
            lastUsed = -1;
            abilities.onlyAllowed = index;
            PointAtUI((RectTransform)slots[index].transform, 270f);
            Say(tip);
            while (lastUsed != index) yield return null;
            abilities.onlyAllowed = -2;
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
                if (pressed && !wasPressed)
                {
                    SpinvUiSound.Click(); // TAP TO CONTINUE / tap to return - щелчок, как у кнопки
                    yield break;
                }
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
            typing = StartCoroutine(Typewriter.Type(tipText, text, charTime, true)); // быстрая печать: звук без разрывов на пробелах
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
