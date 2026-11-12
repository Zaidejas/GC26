using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Spinv
{
    // Метки аркадных волн: на волне может быть несколько сразу
    [System.Flags]
    public enum WaveModifier
    {
        None = 0,
        Fast = 1,       // пришельцы быстрее
        Elite = 2,      // все пришельцы усиленные
        Dark = 4,       // игрок моргает, как на Боссе 2
        Rich = 8,       // монеты выпадают втрое чаще
        NoBunkers = 16, // бункеров нет на эту волну
    }

    // Запускает волны по очереди. Классика: заданные волны, после последней - победа.
    // Аркада: бесконечные циклы «4 волны + босс», каждый цикл сложнее, у волн бывают метки
    public class WaveManager : MonoBehaviour
    {
        public WaveData[] waves;
        public InvaderGrid grid;
        [Tooltip("Пауза с надписью «WAVE N» перед появлением пришельцев")]
        public float delayBeforeWave = 2f;
        public float bossSpawnY = 7.5f;

        [Header("Хилка")]
        public HealthPickup healPrefab;
        [Range(0f, 1f)]
        [Tooltip("Шанс, что в волне выпадет хилка. Больше одной за волну не бывает")]
        public float healChance = 0.6f;
        public float healDelayMin = 3f;
        public float healDelayMax = 15f;
        public float healSpawnY = 13.5f;
        public float healEdgePadding = 1f;
        [Tooltip("Сколько хилок гарантированно падает за бой с боссом. Падают равномерно по его ХП: при 3 - на 75%, 50% и 25%")]
        public int bossHeals = 3;

        [Header("Сценка")]
        [Tooltip("Сценка «наступает ночь» перед последней волной. Без неё волна начинается как обычно. В аркаде её нет")]
        public NightCutscene nightCutscene;
        [Tooltip("Часы в HUD: между волнами классики новая волна ждёт, пока они повернутся")]
        public DayClock dayClock;
        [Tooltip("Пауза между концом волны и поворотом часов")]
        public float pauseBeforeClock = 0.3f;

        [Header("Аркада")]
        [Tooltip("Моргание игрока: метка DARK и сонливость")]
        public BlinkEffect blink;
        [Tooltip("Бункеры: на метке NO BUNKERS прячутся на волну")]
        public GameObject bunkers;
        [Tooltip("Обычных волн в цикле перед боссом")]
        public int wavesPerLoop = 4;
        [Tooltip("На сколько каждый цикл ускоряет пришельцев и их стрельбу")]
        public float loopDifficulty = 0.15f;
        [Tooltip("На сколько с каждым циклом у боссов быстрее снаряды и рывки")]
        public float bossAttackSpeedPerLoop = 0.1f;
        [Tooltip("На сколько с каждым циклом боссы чаще атакуют (короче паузы между атаками и залпами)")]
        public float bossAttackRatePerLoop = 0.1f;
        [Tooltip("На сколько с каждым циклом растёт шанс монетки с пришельцев")]
        public float coinChancePerLoop = 0.1f;
        [Tooltip("Множитель очков растёт с каждым циклом: x1, x1.5, x2...")]
        public float scorePerLoop = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("Шанс сердечка на обычной аркадной волне (одно на волну)")]
        public float healChanceArcade = 1f;
        [Range(0f, 1f)]
        [Tooltip("Шанс метки на обычной волне; сама метка - любая из пяти поровну")]
        public float modifierChance = 1f;
        [Tooltip("Сонливость: раз в столько волн игрок может начать моргать")]
        public int sleepEvery = 4;
        [Range(0f, 1f)] public float sleepChance = 0.4f;
        public float fastSpeed = 1.3f;
        [Tooltip("FAST: на сколько пришельцев меньше в строю (убираются снизу)")]
        public int fastFewerInvaders = 5;
        [Tooltip("ELITE: во сколько раз чаще стреляет весь строй (вес 1.3 у элитных сам по себе этого не даёт, когда элитные все)")]
        public float eliteFireRate = 1.5f;
        [Tooltip("RICH: во сколько раз чаще выпадают монетки")]
        public float richCoinRate = 3f;

        [Header("Проверка в редакторе")]
        [Tooltip("F1, F2... - сразу бой с этим боссом (новые боссы аркады)")]
        public Boss[] testBosses;

        Coroutine healRoutine;
        [Tooltip("Фигура строя аркадной волны 2 цикла ('x' - пришелец, '.' - пусто). Пустая - обычный прямоугольник")]
        public string[] wave2Shape =
        {
            "..x.x..",
            ".xxxxx.",
            "xx.x.xx",
            "xxxxxxx",
            "x.x.x.x",
            "x.....x",
        };
        [Tooltip("Фигура строя аркадной волны 3 цикла")]
        public string[] wave3Shape =
        {
            ".xxxxx.",
            "xxxxxxx",
            "x..x..x",
            "xxxxxxx",
            ".xx.xx.",
            ".x.x.x.",
        };

        [Tooltip("Боссы аркады: в конце каждого цикла случайный из них, шансы равные. Пусто - боссы классики по кругу")]
        public Boss[] arcadeBosses;

        readonly List<WaveData> regularWaves = new List<WaveData>();
        readonly List<WaveData> bossWaves = new List<WaveData>();
        WaveData runtimeWave; // копия шаблона для аркадной волны, удаляется со следующей
        int darkStreak; // сколько волн DARK подряд пережито

        public bool IsArcade { get; private set; }
        // Обучение: волны не идут, строй и хилки выпускает SpinvTutorial
        public bool IsTutorial { get; private set; }
        // Обучение аркады: волны тоже не идут, но надписи, счётчик и метки - как в аркаде
        public bool IsArcadeTutorial { get; private set; }
        // Надписи, номера волн и циклы считаются по-аркадному
        public bool ArcadeStyle => IsArcade || IsArcadeTutorial;
        public int CurrentWave { get; private set; }
        public int TotalWaves => waves.Length;
        // Номер цикла в аркаде, с 1
        public int Loop => ArcadeStyle ?(Mathf.Max(1, CurrentWave) - 1) / (wavesPerLoop + 1) + 1 : 1;
        public WaveModifier Modifiers { get; private set; }

        // (номер волны, всего волн)
        public event System.Action<int, int> WaveStarted;
        public event System.Action<Boss> BossSpawned;

        public bool IsBossWave(int waveNumber) => ArcadeStyle
            ? (waveNumber - 1) % (wavesPerLoop + 1) == wavesPerLoop
            : waves[waveNumber - 1].bossPrefab != null;

        void Awake()
        {
            // До Start остальных: часы и UI сразу знают режим
            var flow = FindAnyObjectByType<SpinvMicrogame>();
            IsTutorial = flow != null && flow.TutorialRun;
            IsArcadeTutorial = flow != null && flow.ArcadeTutorialRun;
            IsArcade = !IsTutorial && ArcadeMode.IsSelected;            foreach (WaveData wave in waves)
                (wave.bossPrefab != null ? bossWaves : regularWaves).Add(wave);
        }

        void Start()
        {
            if (IsTutorial) return;
            grid.Cleared += OnWaveCleared;
            grid.ReachedBottom += OnReachedBottom;
            StartCoroutine(RunWave(0));
        }

    #if UNITY_EDITOR
        // Только в редакторе: F1/F2 - бой с новым боссом (Bomber / Phantom),
        // F7 - сразу волна 4 (предпоследняя), F8 - сразу сценка «наступает ночь»
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (SpinvGameManager.Instance != null && SpinvGameManager.Instance.IsGameOver) return;

            int test = keyboard.f1Key.wasPressedThisFrame ? 0 : keyboard.f2Key.wasPressedThisFrame ? 1 : -1;
            if (test >= 0 && testBosses != null && test < testBosses.Length && testBosses[test] != null)
            {
                ClearForDebug();
                Modifiers = WaveModifier.None;
                Boss boss = SpawnBoss(testBosses[test]);
                if (healPrefab != null) healRoutine = StartCoroutine(DropBossHeals(boss));
            }
            else if (keyboard.f4Key.wasPressedThisFrame && bossWaves.Count > 0 && !IsTutorial)
            {
                // F4 - сразу бой с Боссом 1 (первый босс классики)
                ClearForDebug();
                Modifiers = WaveModifier.None;
                Boss boss = SpawnBoss(bossWaves[0].bossPrefab);
                if (healPrefab != null) healRoutine = StartCoroutine(DropBossHeals(boss));
            }
            else if (keyboard.f3Key.wasPressedThisFrame && !IsArcade && !IsTutorial)
            {
                // ClearForDebug останавливает все корутины - поэтому до запуска нашей, а не внутри неё
                Boss current = FindAnyObjectByType<Boss>();
                bool fighting = CurrentWave >= TotalWaves && current != null;
                if (!fighting) ClearForDebug();
                StartCoroutine(DebugKillFinalBoss(!fighting));
            }
            else if (keyboard.f7Key.wasPressedThisFrame)
            {
                ClearForDebug();
                StartCoroutine(RunWave(TotalWaves - 2));
            }
            else if (keyboard.f8Key.wasPressedThisFrame && nightCutscene != null && !IsArcade)
            {
                ClearForDebug();
                CurrentWave = TotalWaves - 1;
                WaveStarted?.Invoke(CurrentWave, TotalWaves);
                StartCoroutine(CutsceneThenWave(CurrentWave));
            }
        }

        // F3 (только редактор): смерть Босса 2 - анимация смерти, тишина вместо фона и окно победы с музыкой.
        // Если сейчас не бой с ним - сразу запускается последняя волна (без сценки ночи), и босс тут же умирает
        IEnumerator DebugKillFinalBoss(bool startFinalWave)
        {
            if (startFinalWave)
            {
                yield return RunWave(TotalWaves - 1, true);
                yield return null; // босс появился - даём ему кадр на Start
            }
            Boss boss = FindAnyObjectByType<Boss>();
            if (boss != null) boss.DebugKill();
        }

        void ClearForDebug()
        {
            StopAllCoroutines();
            EndModifiers();
            grid.ClearForDebug();
            foreach (Boss boss in FindObjectsByType<Boss>())
            {
                boss.Died -= OnWaveCleared; // убранный босс не должен засчитаться как победа
                Destroy(boss.gameObject);
            }
            foreach (BossMine mine in FindObjectsByType<BossMine>())
                Destroy(mine.gameObject);
            foreach (PhantomDecoy decoy in FindObjectsByType<PhantomDecoy>())
                Destroy(decoy.gameObject);
        }
    #endif

        void OnDestroy()
        {
            if (runtimeWave != null) Destroy(runtimeWave);
            if (grid == null) return;
            grid.Cleared -= OnWaveCleared;
            grid.ReachedBottom -= OnReachedBottom;
        }

        // immediate: без паузы с надписью - когда волну запускает сценка за закрытыми веками
        IEnumerator RunWave(int index, bool immediate = false)
        {
            // Ждём один кадр, чтобы UI успел подписаться на события в своём Start()
            yield return null;

            CurrentWave = index + 1;
            WaveData data = IsArcade ? MakeArcadeWave(CurrentWave) : waves[index];
            if (IsArcade && SpinvGameManager.Instance != null)
                SpinvGameManager.Instance.scoreMultiplier = 1f + scorePerLoop * (Loop - 1);
            WaveStarted?.Invoke(CurrentWave, TotalWaves);

            if (!immediate)
                yield return new WaitForSeconds(delayBeforeWave);
            BeginModifiers();

            if (IsArcade && Loop >= 3) Achievements.Award(AchievementId.Loop3);
            if (IsArcade && Loop >= 5) Achievements.Award(AchievementId.NightOwl);

            if (data.bossPrefab != null)
            {
                // Бой с боссом: несколько хилок гарантированно, по мере того как босс теряет ХП
                Boss boss = SpawnBoss(data.bossPrefab);
                if (healPrefab != null) healRoutine = StartCoroutine(DropBossHeals(boss));
            }
            else
            {
                grid.Build(data);
                if (healPrefab != null && Random.value < (IsArcade ? healChanceArcade : healChance))
                    healRoutine = StartCoroutine(DropHeal());
            }
        }

        // Аркадная волна номер n: позиция в цикле выбирает шаблон (последняя - босс по очереди),
        // номер цикла усложняет, случайно выпадают метки
        WaveData MakeArcadeWave(int number)
        {
            int loop = (number - 1) / (wavesPerLoop + 1);
            int position = (number - 1) % (wavesPerLoop + 1);
            Modifiers = WaveModifier.None;

            // Сонливость: раз в sleepEvery волн, с шансом sleepChance
            if (sleepEvery > 0 && number % sleepEvery == 0 && Random.value < sleepChance)
                Modifiers |= WaveModifier.Dark;

            if (position == wavesPerLoop)
            {
                if (arcadeBosses != null && arcadeBosses.Length > 0)
                {
                    if (runtimeWave != null) Destroy(runtimeWave);
                    runtimeWave = ScriptableObject.CreateInstance<WaveData>();
                    // Босс цикла - случайный, все с равным шансом
                    runtimeWave.bossPrefab = arcadeBosses[Random.Range(0, arcadeBosses.Length)];
                    return runtimeWave;
                }
                if (bossWaves.Count > 0)
                    return bossWaves[loop % bossWaves.Count];
            }

            if (Random.value < modifierChance)
            {
                WaveModifier[] all = { WaveModifier.Fast, WaveModifier.Elite, WaveModifier.Dark, WaveModifier.Rich, WaveModifier.NoBunkers };
                Modifiers |= all[Random.Range(0, all.Length)];
            }

            if (runtimeWave != null) Destroy(runtimeWave);
            runtimeWave = Instantiate(regularWaves[Mathf.Min(position, regularWaves.Count - 1)]);
            // Волна 2 цикла - строй-пришелец, волна 3 - строй-череп
            if (position == 1 && wave2Shape != null && wave2Shape.Length > 0) runtimeWave.shape = wave2Shape;
            if (position == 2 && wave3Shape != null && wave3Shape.Length > 0) runtimeWave.shape = wave3Shape;
            float hard = 1f + loopDifficulty * loop;
            runtimeWave.speed *= hard;
            runtimeWave.missileInterval /= hard;
            runtimeWave.eliteCountMin += loop;
            runtimeWave.eliteCountMax += loop;
            runtimeWave.coinDropChance = Mathf.Min(1f, runtimeWave.coinDropChance + coinChancePerLoop * loop);

            if ((Modifiers & WaveModifier.Fast) != 0)
            {
                runtimeWave.speed *= fastSpeed;
                runtimeWave.shape = RemoveFromBottom(runtimeWave, fastFewerInvaders);
            }
            if ((Modifiers & WaveModifier.Elite) != 0)
            {
                runtimeWave.allElite = true;
                runtimeWave.missileInterval /= eliteFireRate;
            }
            if ((Modifiers & WaveModifier.Rich) != 0) runtimeWave.coinDropChance = Mathf.Min(1f, runtimeWave.coinDropChance * richCoinRate);
            return runtimeWave;
        }

        // Строй волны без count пришельцев снизу: с нижнего ряда, попеременно с левого и правого края,
        // потом со следующего ряда. Опустевшие нижние ряды отрезаются (элитные встают в нижние настоящие ряды)
        static string[] RemoveFromBottom(WaveData data, int count)
        {
            int rows = data.Rows, columns = data.Columns;
            var cells = new char[rows][];
            for (int r = 0; r < rows; r++)
            {
                cells[r] = new char[columns];
                for (int c = 0; c < columns; c++)
                    cells[r][c] = data.Filled(r, c) ? 'x' : '.';
            }

            for (int r = rows - 1; r >= 0 && count > 0; r--)
            {
                int left = 0, right = columns - 1;
                bool fromLeft = true;
                while (left <= right && count > 0)
                {
                    int c = fromLeft ? left++ : right--;
                    fromLeft = !fromLeft;
                    if (cells[r][c] == 'x')
                    {
                        cells[r][c] = '.';
                        count--;
                    }
                }
            }

            int keep = rows;
            while (keep > 1 && System.Array.IndexOf(cells[keep - 1], 'x') < 0) keep--;
            var shape = new string[keep];
            for (int r = 0; r < keep; r++) shape[r] = new string(cells[r]);
            return shape;
        }

        // Метки, которые действуют на поле, а не на строй: моргание и бункеры
        void BeginModifiers()
        {
            if (blink != null) blink.SetSleepy((Modifiers & WaveModifier.Dark) != 0);
            if (bunkers != null) bunkers.SetActive((Modifiers & WaveModifier.NoBunkers) == 0);
        }

        void EndModifiers()
        {
            if (blink != null) blink.SetSleepy(false);
            if (bunkers != null) bunkers.SetActive(true);
        }

        // Обучение аркады: надпись волны с меткой и сама механика метки (моргание, бункеры).
        // Строй выпускает SpinvTutorial
        public void BeginTutorialWave(int number, WaveModifier modifiers)
        {
            EndModifiers();
            CurrentWave = number;
            Modifiers = modifiers;
            WaveStarted?.Invoke(number, TotalWaves);
            BeginModifiers();
        }

        public void EndTutorialWave()
        {
            EndModifiers();
            Modifiers = WaveModifier.None;
        }

        // Надпись перед волной. Аркада: «LOOP 2» в начале цикла, метки строкой ниже
        public string BannerText(int wave)
        {
            if (!ArcadeStyle)
            {
                if (IsBossWave(wave))
                    return wave == TotalWaves ? "FINAL BOSS" : "BOSS";
                return wave == TotalWaves ? "FINAL WAVE" : $"WAVE {wave}";
            }

            string text = IsBossWave(wave) ? "BOSS" : $"WAVE {wave}";
            if ((wave - 1) % (wavesPerLoop + 1) == 0 && wave > 1)
                text = $"<size=60%>LOOP {Loop}</size>\n" + text;
            if (Modifiers != WaveModifier.None)
                text += "\n<size=55%><color=#FFD45A>" + ModifierNames(Modifiers) + "</color></size>";
            return text;
        }

        static string ModifierNames(WaveModifier modifiers)
        {
            var names = new List<string>();
            if ((modifiers & WaveModifier.Fast) != 0) names.Add("FAST");
            if ((modifiers & WaveModifier.Elite) != 0) names.Add("ELITE");
            if ((modifiers & WaveModifier.Dark) != 0) names.Add("DARK");
            if ((modifiers & WaveModifier.Rich) != 0) names.Add("RICH");
            if ((modifiers & WaveModifier.NoBunkers) != 0) names.Add("NO BUNKERS");
            return string.Join(" + ", names);
        }

        Boss SpawnBoss(Boss prefab)
        {
            Boss boss = Instantiate(prefab, new Vector3(0f, bossSpawnY, 0f), Quaternion.identity);
            // В аркаде с каждым циклом босс атакует быстрее и чаще (ХП прежнее)
            if (IsArcade && Loop > 1)
            {
                float speed = 1f + bossAttackSpeedPerLoop * (Loop - 1);
                float rate = 1f + bossAttackRatePerLoop * (Loop - 1);
                boss.missileSpeed *= speed;
                boss.spiralMissileSpeed *= speed;
                boss.minionFallSpeed *= speed;
                boss.dashSpeed *= speed;
                boss.bombSpeed *= speed;
                boss.bombGravity *= speed;
                boss.attackPause /= rate;
                boss.fanInterval /= rate;
                boss.burstInterval /= rate;
                boss.wallInterval /= rate;
            }
            boss.Init(grid.player);
            boss.Died += OnWaveCleared;
            BossSpawned?.Invoke(boss);
            return boss;
        }

        // Обычная волна: хилка падает в случайный момент
        IEnumerator DropHeal()
        {
            yield return new WaitForSeconds(Random.Range(healDelayMin, healDelayMax));
            SpawnHeal();
        }

        // Босс: хилка номер k падает, когда у босса остаётся 1 - k/(n+1) ХП (при n = 3: 75%, 50%, 25%)
        IEnumerator DropBossHeals(Boss boss)
        {
            for (int k = 1; k <= bossHeals; k++)
            {
                float threshold = 1f - (float)k / (bossHeals + 1);
                while (boss != null && boss.HealthFraction > threshold)
                    yield return null;
                if (boss == null || boss.HealthFraction <= 0f) yield break; // босс убит - бой окончен
                SpawnHeal();
            }
        }

        // Хилка из случайного места сверху; atX - над этой точкой (обучение)
        public void SpawnHeal(float? atX = null)
        {
            Camera cam = Camera.main;
            float minX = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x + healEdgePadding;
            float maxX = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x - healEdgePadding;
            float x = atX.HasValue ? Mathf.Clamp(atX.Value, minX, maxX) : Random.Range(minX, maxX);
            Instantiate(healPrefab, new Vector3(x, healSpawnY, 0f), Quaternion.identity);
        }

        void OnWaveCleared()
        {
            // Волна кончилась раньше таймера: хилка этой волны уже не выпадет
            if (healRoutine != null)
                StopCoroutine(healRoutine);

            // INSOMNIA: три волны DARK подряд пережиты
            if (IsArcade)
            {
                darkStreak = (Modifiers & WaveModifier.Dark) != 0 ? darkStreak + 1 : 0;
                if (darkStreak >= 3) Achievements.Award(AchievementId.Insomnia);
            }
            EndModifiers();

            if (IsArcade)
                StartCoroutine(RunWave(CurrentWave));
            else if (CurrentWave >= TotalWaves)
                SpinvGameManager.Instance.Victory();
            else if (nightCutscene != null && CurrentWave == TotalWaves - 1)
                StartCoroutine(CutsceneThenWave(CurrentWave));
            else
                StartCoroutine(ClockThenWave(CurrentWave));
        }

        // Классика: после волны короткая пауза, сценка с крупными часами (как перед ночью, но без моргания),
        // и только потом надпись и новая волна
        IEnumerator ClockThenWave(int index)
        {
            if (dayClock != null && dayClock.isActiveAndEnabled)
            {
                yield return new WaitForSeconds(pauseBeforeClock);
                if (nightCutscene != null)
                    yield return nightCutscene.PlayClockTurn(dayClock.AngleForWave(index + 1));
                else
                    yield return dayClock.TurnForWave(index + 1);
            }
            yield return RunWave(index);
        }

        // Перед последней волной - сценка «наступает ночь»; волна стартует за закрытыми веками
        IEnumerator CutsceneThenWave(int index)
        {
            bool started = false;
            yield return nightCutscene.Play(() =>
            {
                started = true;
                StartCoroutine(RunWave(index, true));
            });
            if (!started)
                StartCoroutine(RunWave(index, true));
        }

        void OnReachedBottom()
        {
            SpinvGameManager.Instance.InvadedGameOver();
        }
    }
}
