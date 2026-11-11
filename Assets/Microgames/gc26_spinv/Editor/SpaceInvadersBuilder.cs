using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Сборщик игры: Tools → Space Invaders → Build Game
    // Рисует спрайты, генерирует звуки, создаёт префабы, 5 волн и одну сцену gc26_spinv_Main:
    // меню и выключенный шаблон игры (SpinvMicrogame переключает их без загрузки сцен).
    // Всё лежит в папке игры Assets/Microgames/gc26_spinv; общие настройки проекта не трогаются (RULES.md).
    public static class SpaceInvadersBuilder
    {
        public const string RootDir = "Assets/Microgames/gc26_spinv";
        const string SpritesDir = RootDir + "/Sprites";
        const string GameArtDir = SpritesDir; // нарисованные и сгенерированные картинки - в одной папке
        const string AudioDir = RootDir + "/Audio";
        const string PrefabsDir = RootDir + "/Prefabs";
        const string WavesDir = RootDir + "/Waves";
        const string ScenesDir = RootDir + "/Scenes";
        public const string MainScenePath = ScenesDir + "/gc26_spinv_Main.unity";
        const float BodyScale = 1.8f; // Игрок на вертикальном экране
        const float InvaderScale = 1.3333f;
        const float PickupScale = 3f; // Монетка и хилка
        const float MissilePpu = 624f;

        // ---------- Пиксель-арт ('#' - основной цвет, '+' - второй цвет, '.' - пусто) ----------












        static readonly string[] BlockArt = { "####", "####", "####", "####" };







        // Крыша на опорах из мелких блоков (0.1 юнита): у всех бункеров одна крыша и крайние опоры,
        // а короткий средний зубец под крышей у каждого на своём месте
        static readonly string[][] BunkerShapes =
        {
            BunkerWithTooth("..###..####....###.."),
            BunkerWithTooth("..###......#######.."),
            BunkerWithTooth("..#######......###.."),
            BunkerWithTooth("..###....####..###.."),
        };

        static string[] BunkerWithTooth(string toothRow)
        {
            const string roof = "####################";
            const string eaves = "..################..";
            const string legs = "..###..........###..";
            return new[] { roof, roof, roof, eaves, eaves, toothRow, toothRow, toothRow, legs, legs, legs };
        }

        // ---------- Цвета ----------

        static readonly Color PlayerColor = new Color(0.22f, 1f, 0.42f);
        static readonly Color TextColor = new Color(0.78f, 1f, 0.84f);

        // ---------- Созданные ассеты ----------

        static Sprite laserSprite, missileSprite, coinSprite, explosionSprite, blockSprite, blockTopSprite, blockGrooveSprite;
        static Sprite boss1Sprite, boss2Sprite, boss1AngrySprite, boss2AngrySprite, heartSprite;
        static Sprite bomberSprite, bomberAngrySprite, phantomSprite, phantomAngrySprite, bombSprite, mineSprite, mineLightSprite, wispSprite;
        static Boss bomberBossPrefab, phantomBossPrefab;
        static Sprite canSprite, logoSprite;
        static Sprite btnOutlineSprite, btnFillSprite, btnGlowSprite, windowSprite;
        static Sprite shieldIconSprite, bunkerIconSprite, rapidFireIconSprite, uiSquareSprite;
        static Sprite[] squidFrames, crabFrames, octopusFrames, eliteFrames;
        static AudioClip shootClip, explosionClip, coinClip, hitClip;
        static Projectile laserPrefab, missilePrefab;
        static Coin coinPrefab;
        static GameObject explosionPrefab;
        static Invader[] invaderPrefabs;
        static Invader eliteInvaderPrefab;
        static Camera sceneCamera;
        static LaserBeam bossLaserPrefab;
        static HealthPickup healPrefab;
        static WaveData[] waves;

        [MenuItem("Tools/Space Invaders/Build Game")]
        public static void BuildFromMenu()
        {
            if (!EnsureTextMeshPro(true)) return;

            if (!EditorUtility.DisplayDialog("Space Invaders",
                    "Будут созданы (или перезаписаны) спрайты, звуки, префабы, волны и сцены SpinvMainMenu и Game.\n\nПродолжить?",
                    "Да", "Отмена"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildAll();

            EditorUtility.DisplayDialog("Space Invaders",
                "Готово!\n\nСцена SpinvMainMenu открыта - нажми Play.", "OK");
        }

        // Для запуска из командной строки: Unity -batchmode -executeMethod SpaceInvadersBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            if (!EnsureTextMeshPro(false))
            {
                Debug.LogError("TMP Essential Resources were missing. They are being imported - run the build again.");
                return;
            }
            BuildAll();
        }

        static void BuildAll()
        {
            foreach (string dir in new[] { SpritesDir, AudioDir, PrefabsDir, WavesDir, ScenesDir })
                EnsureFolder(dir);

            CreateSprites();
            CreateSounds();
            CreateWaves();

            // Одна сцена: общие камера и EventSystem, выключенный шаблон игры и меню
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            sceneCamera = null;
            CreatePrefabs();
            MakeCamera();
            MakeEventSystem();

            var before = new HashSet<GameObject>(scene.GetRootGameObjects());
            BuildGameScene();
            GameObject gameTemplate = GatherNewRoots(scene, before, "GameTemplate");

            before = new HashSet<GameObject>(scene.GetRootGameObjects());
            BuildMenuScene();
            GameObject menuRoot = GatherNewRoots(scene, before, "Menu");

            var flow = new GameObject("gc26_spinv").AddComponent<SpinvMicrogame>();
            flow.menuRoot = menuRoot;
            flow.gameTemplate = gameTemplate;
            menuRoot.GetComponentInChildren<SpinvMainMenu>(true).flow = flow;
            gameTemplate.SetActive(false);

            // CRT - проход на нашей камере, а не в общем рендерере проекта
            sceneCamera.gameObject.AddComponent<CrtCamera>().material = SpaceInvadersCrt.LoadOrCreateMaterial();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            SpaceInvadersBackground.ApplyIfMissing();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Space Invaders: build complete");
        }

        // Всё, что появилось в корне сцены после before, складывается в новый корневой объект name
        static GameObject GatherNewRoots(UnityEngine.SceneManagement.Scene scene, HashSet<GameObject> before, string name)
        {
            var root = new GameObject(name);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go != root && !before.Contains(go))
                    go.transform.SetParent(root.transform, true);
            }
            return root;
        }

        // TextMeshPro требует импортировать базовые ресурсы (шрифт, настройки)
        static bool EnsureTextMeshPro(bool interactive)
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;

            string package = FindTmpEssentials();
            if (package == null)
            {
                if (interactive)
                    EditorUtility.DisplayDialog("Space Invaders",
                        "Импортируй TextMeshPro вручную:\nWindow → TextMeshPro → Import TMP Essential Resources\n\nПотом запусти Tools → Space Invaders → Build Game ещё раз.",
                        "OK");
                return false;
            }

            AssetDatabase.ImportPackage(package, false);
            if (interactive)
                EditorUtility.DisplayDialog("Space Invaders",
                    "Импортирую ресурсы TextMeshPro.\n\nКогда импорт закончится, запусти Tools → Space Invaders → Build Game ещё раз.",
                    "OK");
            return false;
        }

        static string FindTmpEssentials()
        {
            foreach (string packageName in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName);
                if (info == null) continue;

                string path = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        // ================= Ассеты =================

        static void CreateSprites()
        {
            uiSquareSprite = MakeSprite("UISquare", BlockArt, Palette(Color.white), 100);

            LoadGameArt();
        }

        // Нарисованная графика из Sprites. PPU подобран так, чтобы размеры в игре остались прежними
        static void LoadGameArt()
        {
            squidFrames = new[] { Art("squid_1", 384f, null), Art("squid_2", 384f, null) };
            crabFrames = new[] { Art("crab_1", 390f, null), Art("crab_2", 390f, null) };
            octopusFrames = new[] { Art("octopus_1", 358f, null), Art("octopus_2", 358f, null) };
            eliteFrames = new[] { Art("elite_1", 358f, null), Art("elite_2", 358f, null) };
            boss1Sprite = Art("boss_1", 768f, null);
            boss2Sprite = Art("boss_2", 816f, null);
            // Красные копии для приступов ярости (каналы переставлены, размер тот же). Нет файла - без красноты
            boss1AngrySprite = Art("boss_1_angry", 768f, null);
            boss2AngrySprite = Art("boss_2_angry", 816f, null);
            // Боссы аркады: Bomber и Phantom, их бомба и мина
            bomberSprite = Art("boss_bomber", 800f, null);
            bomberAngrySprite = Art("boss_bomber_angry", 800f, null);
            phantomSprite = Art("boss_phantom", 800f, null);
            phantomAngrySprite = Art("boss_phantom_angry", 800f, null);
            bombSprite = Art("bomb", 300f, null);
            mineSprite = Art("mine", 300f, null);
            mineLightSprite = Art("mine_light", 300f, null);
            wispSprite = Art("wisp", 300f, null);
            laserSprite = Art("laser", 600f, null);
            missileSprite = Art("missile", MissilePpu, null); // квадрат 128 px на холсте 136, в игре повёрнут ромбом
            coinSprite = Art("coin", 640f, null);
            explosionSprite = Art("explosion", 683f, null);
            blockSprite = Art("block", 500f, null);              // 50x50 -> 0.1 юнита
            blockTopSprite = Art("block_top", 500f, null);
            blockGrooveSprite = Art("block_groove", 500f, null);
            heartSprite = Art("heart", 100f, null);
            shieldIconSprite = Art("icon_shield", 100f, null);
            bunkerIconSprite = Art("icon_repair", 100f, null);
            rapidFireIconSprite = Art("icon_rapidfire", 100f, null);
            canSprite = Art("heart_pickup", 550f, null); // хилка: сердечко размером с монетку
            logoSprite = Art("logo", 100f, null);

            // Экраны конца игры: аркадные кнопки, свечение нажатия, окно Game Over (9-slice)
            btnOutlineSprite = Art("btn_outline", 100f, uiSquareSprite);
            btnFillSprite = Art("btn_fill", 100f, uiSquareSprite);
            btnGlowSprite = Art("btn_glow", 100f, null);
            windowSprite = Art("window", 100f, uiSquareSprite, new Vector4(80f, 80f, 80f, 80f));
        }

        // border - края для 9-slice (левый, нижний, правый, верхний), по умолчанию без растягивания по краям
        static Sprite Art(string name, float pixelsPerUnit, Sprite fallback, Vector4 border = default)
        {
            string path = $"{GameArtDir}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return fallback;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : fallback;
        }

        static void CreateSounds()
        {
            shootClip = MakeSound("Shoot", 0.12f, 1200f, 300f, 0f);
            explosionClip = MakeSound("Explosion", 0.3f, 200f, 60f, 0.8f);
            coinClip = MakeSound("Coin", 0.18f, 900f, 1800f, 0f);
            hitClip = MakeSound("PlayerHit", 0.5f, 300f, 40f, 0.5f);
        }

        static void CreateWaves()
        {
            waves = new[]
            {
                MakeWave("Wave1", 4, 6, 1.5f, 1.0f, 0.20f),
                MakeWave("Wave2", 5, 6, 2.1f, 0.73f, 0.25f, 2, 5),
                MakeWave("Wave3", 6, 6, 2.7f, 0.5f, 0.30f),
                MakeWave("Wave4", 7, 6, 2.7f, 0.5f, 0.30f, 2, 5),
                MakeWave("Wave5", 7, 6, 2.7f, 0.5f, 0.35f),
            };

            // Классика: вторая волна строем-ромбом (в аркаде у второй волны своя фигура - пришелец)
            waves[1].shape = new[]
            {
                "...x...",
                "..xxx..",
                ".xx.xx.",
                "xx...xx",
                ".xx.xx.",
                "..xxx..",
                "...x...",
            };
            EditorUtility.SetDirty(waves[1]);
        }

        static WaveData MakeWave(string name, int rows, int columns, float speed, float missileInterval, float coinChance, int eliteMin = 0, int eliteMax = 0)
        {
            string path = $"{WavesDir}/{name}.asset";
            var wave = AssetDatabase.LoadAssetAtPath<WaveData>(path);
            bool isNew = wave == null;
            if (isNew)
                wave = ScriptableObject.CreateInstance<WaveData>();

            wave.eliteCountMin = eliteMin;
            wave.eliteCountMax = eliteMax;
            wave.shape = null; // фигуру задают отдельно, после создания
            wave.rows = rows;
            wave.columns = columns;
            wave.speed = speed;
            wave.missileInterval = missileInterval;
            wave.coinDropChance = coinChance;

            // Существующий файл обновляем на месте, чтобы не менять его GUID
            if (isNew)
                AssetDatabase.CreateAsset(wave, path);
            else
                EditorUtility.SetDirty(wave);
            return wave;
        }

        static void CreatePrefabs()
        {
            // Пуля игрока: летит вверх, бункеры не ломает
            var laser = NewSpriteObject("Laser", laserSprite, 1);
            AddTrigger(laser, true);
            var laserProjectile = laser.AddComponent<Projectile>();
            laserProjectile.direction = Vector2.up;
            laserProjectile.speed = 16f;
            laserProjectile.fromPlayer = true;
            laserProjectile.damagesBunkers = false;
            laserPrefab = SavePrefab(laser).GetComponent<Projectile>();

            // Ракета пришельца: летит вниз, ломает бункеры
            var missile = NewSpriteObject("Missile", missileSprite, 1);
            AddTrigger(missile, true);
            var missileProjectile = missile.AddComponent<Projectile>();
            missileProjectile.direction = Vector2.down;
            missileProjectile.speed = 7f;
            missileProjectile.fromPlayer = false;
            missileProjectile.damagesBunkers = true;
            missileProjectile.bunkerDamageRadius = 0.4f;
            missileProjectile.spinSpeed = 360f; // ромб крутится в полёте
            // Хитбокс ровно по светлому квадрату (128 px из 136), без прозрачного поля
            missile.GetComponent<BoxCollider2D>().size = Vector2.one * (128f / MissilePpu);
            missile.transform.localScale = Vector3.one * 1.6f;
            missilePrefab = SavePrefab(missile).GetComponent<Projectile>();

            var coin = NewSpriteObject("Coin", coinSprite, 2);
            coin.transform.localScale = Vector3.one * PickupScale;
            AddTrigger(coin, true);
            coin.AddComponent<Coin>();
            coinPrefab = SavePrefab(coin).GetComponent<Coin>();

            var explosion = NewSpriteObject("Explosion", explosionSprite, 3);
            explosion.AddComponent<Explosion>();
            explosionPrefab = SavePrefab(explosion);

            // Хилка: спрайт банки не генерируется, его кладут в NewSprites
            if (canSprite != null)
            {
                var heal = NewSpriteObject("HealthPickup", canSprite, 2);
                heal.transform.localScale = Vector3.one * PickupScale;
                AddTrigger(heal, true);
                heal.AddComponent<HealthPickup>();
                healPrefab = SavePrefab(heal).GetComponent<HealthPickup>();
            }

            invaderPrefabs = new[]
            {
                MakeInvaderPrefab("Invader_Squid", squidFrames, 30),
                MakeInvaderPrefab("Invader_Crab", crabFrames, 20),
                MakeInvaderPrefab("Invader_Octopus", octopusFrames, 10),
            };
            eliteInvaderPrefab = MakeInvaderPrefab("Invader_Elite", eliteFrames, 40, 2, 1f, 1.3f); // всегда целится в игрока, стреляет в 1.3 раза чаще

            var bossLaser = NewSpriteObject("BossLaser", uiSquareSprite, 4);
            bossLaser.AddComponent<LaserBeam>();
            bossLaserPrefab = SavePrefab(bossLaser).GetComponent<LaserBeam>();

            waves[2].bossPrefab = MakeBossPrefab("Boss_1", boss1Sprite, boss1AngrySprite, 3f, 100, 500, 3f, 9f,
                new[] { BossAttack.Fan, BossAttack.Burst, BossAttack.Dash }, false, null);
            waves[4].bossPrefab = MakeBossPrefab("Boss_2", boss2Sprite, boss2AngrySprite, 4f, 200, 1000, 3.5f, 10f,
                new[] { BossAttack.Wall, BossAttack.Spiral, BossAttack.Laser, BossAttack.Summon }, true, invaderPrefabs[1]);
            waves[4].bossPrefab.playerBlinks = true;
            waves[4].bossPrefab.dramaticDeath = true; // анимация смерти ~5 с перед победой
            EditorUtility.SetDirty(waves[4].bossPrefab);
            EditorUtility.SetDirty(waves[2]);
            EditorUtility.SetDirty(waves[4]);

            MakeArcadeBosses();
        }

        // Bomber: мины, бомбы рывками, очередь, грозди. Phantom: телепорт с засадой, двойники, веер, огоньки, хватка, луч.
        // Пока запускаются клавишами F1/F2 в редакторе
        static void MakeArcadeBosses()
        {
            // Бомба: зелёная с четырьмя рёбрами и мигающим оранжевым ядром, падает по дуге с ускорением,
            // выбивает из бункера большой кусок
            var bomb = NewSpriteObject("Bomb", bombSprite, 1);
            var core = NewSpriteObject("Core", Art("bomb_core", 300f, uiSquareSprite), 2);
            core.transform.SetParent(bomb.transform, false);
            core.AddComponent<SpriteBlink>();
            AddTrigger(bomb, true);
            var bombProjectile = bomb.AddComponent<Projectile>();
            bombProjectile.direction = Vector2.down;
            bombProjectile.speed = 4f;
            bombProjectile.gravity = 10f;
            bombProjectile.fromPlayer = false;
            bombProjectile.damagesBunkers = true;
            bombProjectile.bunkerDamageRadius = 0.8f;
            bomb.transform.localScale = Vector3.one * 1.2f;
            var bombPrefab = SavePrefab(bomb).GetComponent<Projectile>();

            // Гроздь: красный шар, над бункерами лопается веером на 5 маленьких красных бомб
            var bomblet = NewSpriteObject("Bomblet", Art("bomblet", 300f, missileSprite), 1);
            AddTrigger(bomblet, true);
            var bombletProjectile = bomblet.AddComponent<Projectile>();
            bombletProjectile.direction = Vector2.down;
            bombletProjectile.gravity = 10f;
            bombletProjectile.fromPlayer = false;
            bombletProjectile.damagesBunkers = true;
            bombletProjectile.bunkerDamageRadius = 0.3f;
            bomblet.transform.localScale = Vector3.one * 1.3f;
            var bombletPrefab = SavePrefab(bomblet).GetComponent<Projectile>();

            var cluster = NewSpriteObject("ClusterBomb", Art("cluster", 300f, missileSprite), 1);
            AddTrigger(cluster, true);
            var clusterProjectile = cluster.AddComponent<Projectile>();
            clusterProjectile.direction = Vector2.down;
            clusterProjectile.gravity = 10f;
            clusterProjectile.fromPlayer = false;
            clusterProjectile.damagesBunkers = true;
            clusterProjectile.bunkerDamageRadius = 0.8f;
            cluster.transform.localScale = Vector3.one * 1.4f;
            var clusterSplit = cluster.AddComponent<ClusterBomb>();
            clusterSplit.bombletPrefab = bombletPrefab;
            clusterSplit.burstEffect = explosionPrefab;
            var clusterPrefab = SavePrefab(cluster).GetComponent<Projectile>();

            // Мина: зелёная морская, летит по дуге к кораблю, красный огонёк мигает, вокруг кольцо радиуса взрыва
            var mine = NewSpriteObject("Mine", mineSprite, 3);
            mine.transform.localScale = Vector3.one * 1.3f;
            var ring = NewSpriteObject("BlastRing", MakeRingSprite("MineRing", 0.06f), 2);
            ring.transform.SetParent(mine.transform, false);
            var lamp = NewSpriteObject("Lamp", mineLightSprite, 4);
            lamp.transform.SetParent(mine.transform, false);
            var mineScript = mine.AddComponent<BossMine>();
            mineScript.body = mine.GetComponent<SpriteRenderer>();
            mineScript.lamp = lamp.GetComponent<SpriteRenderer>();
            mineScript.ring = ring.GetComponent<SpriteRenderer>();
            mineScript.explosionPrefab = explosionPrefab;
            var minePrefab = SavePrefab(mine).GetComponent<BossMine>();

            // Огонёк Phantom-а: преследует корабль, сбивается одним попаданием
            var wisp = NewSpriteObject("Wisp", wispSprite, 3);
            wisp.transform.localScale = Vector3.one * 1.2f;
            var wispCollider = wisp.AddComponent<CircleCollider2D>();
            wispCollider.isTrigger = true;
            wispCollider.radius = wispSprite.bounds.extents.x * 0.8f; // с запасом: по огоньку должно быть легко попасть
            wisp.AddComponent<Wisp>().explosionPrefab = explosionPrefab;
            var wispPrefab = SavePrefab(wisp).GetComponent<Wisp>();

            bomberBossPrefab = MakeBossPrefab("Boss_Bomber", bomberSprite, bomberAngrySprite, 3.2f, 150, 800, 2.2f, 9f,
                new[] { BossAttack.Mines, BossAttack.Carpet, BossAttack.Burst, BossAttack.Cluster }, true, null);
            bomberBossPrefab.jerkyMovement = true; // резко перелетает с места на место, сбрасывая бомбы
            bomberBossPrefab.grantsAchievement = true;
            bomberBossPrefab.achievement = AchievementId.BombSquad;
            bomberBossPrefab.clusterPrefab = clusterPrefab;
            bomberBossPrefab.minePrefab = minePrefab;
            bomberBossPrefab.bombPrefab = bombPrefab;
            EditorUtility.SetDirty(bomberBossPrefab);

            phantomBossPrefab = MakeBossPrefab("Boss_Phantom", phantomSprite, phantomAngrySprite, 3.4f, 100, 800, 3f, 9f,
                new[] { BossAttack.Teleport, BossAttack.Decoys, BossAttack.Fan, BossAttack.Wisps, BossAttack.Grab, BossAttack.Drain }, true, null);
            phantomBossPrefab.ghostShotsInRage = true;
            phantomBossPrefab.grantsAchievement = true;
            phantomBossPrefab.achievement = AchievementId.Exorcist;
            phantomBossPrefab.wispPrefab = wispPrefab;
            phantomBossPrefab.grabMarkSprite = Art("grab_mark", 300f, null);
            phantomBossPrefab.grabHandSprite = Art("grab_hand", 300f, null);
            phantomBossPrefab.drainBeamSprite = uiSquareSprite;
            EditorUtility.SetDirty(phantomBossPrefab);
            AssetDatabase.SaveAssets();
        }

        static Invader MakeInvaderPrefab(string name, Sprite[] frames, int points, int hitPoints = 1, float aimedShotChance = 0f, float fireRateMultiplier = 1f)
        {
            var go = NewSpriteObject(name, frames[0], 0);
            go.transform.localScale = Vector3.one * InvaderScale;
            AddTrigger(go, false);
            var invader = go.AddComponent<Invader>();
            invader.points = points;
            invader.hitPoints = hitPoints;
            invader.aimedShotChance = aimedShotChance;
            invader.fireRateMultiplier = fireRateMultiplier;
            invader.frames = frames;
            invader.explosionPrefab = explosionPrefab;
            invader.coinPrefab = coinPrefab;
            return SavePrefab(go).GetComponent<Invader>();
        }

        static Boss MakeBossPrefab(string name, Sprite sprite, Sprite angrySprite, float scale, int health, int points, float speed, float missileSpeed,
            BossAttack[] attacks, bool rage, Invader minion)
        {
            // Корень: коллайдер и логика. Картинка в дочернем Visual, чтобы её можно было трясти,
            // поверх неё красная копия для приступов ярости (BossAnger)
            var go = new GameObject(name);
            go.transform.localScale = Vector3.one * scale;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = sprite.bounds.size;

            var visual = NewSpriteObject("Visual", sprite, 0);
            visual.transform.SetParent(go.transform, false);
            var angry = NewSpriteObject("Angry", angrySprite != null ? angrySprite : sprite, 1);
            angry.transform.SetParent(visual.transform, false);
            angry.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0f);

            var boss = go.AddComponent<Boss>();
            var so = new SerializedObject(boss);
            so.FindProperty("spriteRenderer").objectReferenceValue = visual.GetComponent<SpriteRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var anger = go.AddComponent<BossAnger>();
            anger.visual = visual.transform;
            anger.body = visual.GetComponent<SpriteRenderer>();
            anger.angry = angry.GetComponent<SpriteRenderer>();

            boss.maxHealth = health;
            boss.points = points;
            boss.moveSpeed = speed;
            boss.missileSpeed = missileSpeed;
            boss.attacks = attacks;
            boss.missilePrefab = missilePrefab;
            boss.laserPrefab = bossLaserPrefab;
            boss.coinPrefab = coinPrefab;
            boss.explosionPrefab = explosionPrefab;
            boss.rageAtHalfHealth = rage;
            boss.minionPrefab = minion;
            return SavePrefab(go).GetComponent<Boss>();
        }

        // ================= Сцена Game =================

        static void BuildGameScene()
        {
            MakeCamera();

            var managers = new GameObject("SpinvGameManager");
            managers.AddComponent<SpinvGameManager>();
            var sound = managers.AddComponent<SpinvSoundManager>();
            managers.GetComponent<AudioSource>().playOnAwake = false;
            sound.shoot = shootClip;
            sound.explosion = explosionClip;
            sound.coin = coinClip;
            sound.playerHit = hitClip;

            // Спрайт из дизайна, если он есть: коллайдер только по видимой части корабля
            var heroSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameArtDir + "/hero.png");
            var player = NewSpriteObject("Player", heroSprite, 0);
            player.transform.position = new Vector3(0f, -8f, 0f); // повыше от края: палец не попадает в зону системных жестов
            player.transform.localScale = Vector3.one * BodyScale;
            AddTrigger(player, false);
            if (heroSprite != null)
                player.GetComponent<BoxCollider2D>().size = new Vector2(1.0824f, 0.6353f) * 0.65f; // хитбокс 65% корабля: легче уворачиваться на телефоне
            player.AddComponent<PlayerController>().laserPrefab = laserPrefab;
            player.AddComponent<ShipSparks>().sparkSprite = uiSquareSprite; // оранжевые искры при ускоренной стрельбе
            BuildShipMotion(player);
            BuildPlayerHearts(player.transform);

            var gridObject = new GameObject("InvaderGrid");
            gridObject.transform.position = new Vector3(0f, 9.2f, 0f);
            var grid = gridObject.AddComponent<InvaderGrid>();
            grid.invaderPrefabs = invaderPrefabs;
            grid.eliteInvaderPrefab = eliteInvaderPrefab;
            grid.missilePrefab = missilePrefab;
            grid.player = player.GetComponent<PlayerController>();

            player.GetComponent<PlayerController>().shieldVisual = BuildShieldDome(player.transform);

            var waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();
            waveManager.waves = waves;
            waveManager.grid = grid;
            waveManager.healPrefab = healPrefab;
            waveManager.testBosses = new[] { bomberBossPrefab, phantomBossPrefab }; // F1 / F2 в редакторе
            // Аркада: в конце каждого цикла случайный из четырёх боссов, по 25%
            waveManager.arcadeBosses = new[] { waves[2].bossPrefab, bomberBossPrefab, waves[4].bossPrefab, phantomBossPrefab };

            var abilities = new GameObject("AbilityManager").AddComponent<AbilityManager>();
            abilities.player = player.GetComponent<PlayerController>();
            abilities.bunkers = BuildBunkers();
            abilities.abilities = new[]
            {
                new Ability { type = AbilityType.Shield, cost = 3, cooldown = 10f, duration = 2.5f },
                new Ability { type = AbilityType.RepairBunkers, cost = 5, cooldown = 20f, duration = 0f },
                new Ability { type = AbilityType.RapidFire, cost = 10, cooldown = 20f, duration = 5f },
            };

            BuildGameUI(waveManager, abilities);
            BlinkEffect blink = BuildBlinkOverlay(waveManager);
            waveManager.blink = blink; // аркада: метка DARK и сонливость
            waveManager.bunkers = abilities.bunkers.gameObject; // аркада: метка NO BUNKERS
            waveManager.nightCutscene = BuildNightCutscene(blink);
            waveManager.nightCutscene.player = player.GetComponent<PlayerController>(); // в сценке не стреляет
            BuildAchievementToast();
            MakeEventSystem();
        }

        // Анимация корабля: наклон при движении, выхлоп под подставкой и бегущие огни по диску (ShipMotion).
        // Координаты в локальных единицах корабля: пивот hero.png в 35 px от низа, низ подставки - в 9 px
        static void BuildShipMotion(GameObject player)
        {
            const int size = 64;
            const float flameWidth = 0.16f;
            // Треугольник остриём вверх с пивотом в основании; масштаб по y со знаком минус разворачивает его вниз
            var flameSprite = MakeGeneratedSprite("ShipFlame", size, size, size / flameWidth, (x, y) =>
            {
                float halfWidth = size * 0.5f * (1f - (y + 0.5f) / size);
                return Mathf.Clamp01(halfWidth - Mathf.Abs(x + 0.5f - size * 0.5f) + 0.5f);
            });

            var flame = NewSpriteObject("Flame", flameSprite, -1);
            flame.transform.SetParent(player.transform, false);
            flame.transform.localPosition = new Vector3(0.01f, -0.25f, 0f);
            flame.transform.localScale = new Vector3(1f, -1f, 1f);
            var flameRenderer = flame.GetComponent<SpriteRenderer>();
            flameRenderer.color = new Color(0.65f, 0.96f, 0.73f);

            var core = NewSpriteObject("FlameCore", flameSprite, -1);
            core.transform.SetParent(flame.transform.parent, false);
            core.transform.localPosition = new Vector3(0.01f, -0.25f, 0f);
            core.transform.localScale = new Vector3(0.5f, -0.6f, 1f);
            var coreRenderer = core.GetComponent<SpriteRenderer>();
            coreRenderer.color = Color.white;
            coreRenderer.sortingOrder = 0;

            var motion = player.AddComponent<ShipMotion>();
            motion.flame = flameRenderer;
            motion.flameCore = coreRenderer;
            motion.lightSprite = uiSquareSprite;
            motion.lightCenter = new Vector2(0.01f, -0.115f);
        }

        static SpriteRenderer[] playerHearts;

        // Жизни: три сердечка в ряд под выхлопом, дети корабля - наклоняются вместе с ним.
        // Координаты в локальных единицах корабля (масштаб корабля BodyScale)
        static void BuildPlayerHearts(Transform player)
        {
            const float heartWidth = 0.28f;
            const float spacing = 0.33f;
            const float y = -0.72f; // ниже самого длинного пламени (≈ -0.55)
            float scale = heartSprite != null && heartSprite.bounds.size.x > 0f ? heartWidth / heartSprite.bounds.size.x : 1f;

            playerHearts = new SpriteRenderer[3];
            for (int i = 0; i < playerHearts.Length; i++)
            {
                var heart = NewSpriteObject($"Heart{i + 1}", heartSprite, 0);
                heart.transform.SetParent(player, false);
                heart.transform.localPosition = new Vector3((i - 1) * spacing, y, 0f);
                heart.transform.localScale = Vector3.one * scale;
                playerHearts[i] = heart.GetComponent<SpriteRenderer>();
            }
        }

        // Щит-купол над кораблём: заливка (она же маска для сканлайнов), пульсирующий край и бегущие сканлайны.
        // Размеры в локальных единицах корабля: основание купола на нижнем краю диска
        static GameObject BuildShieldDome(Transform player)
        {
            const float radius = 0.62f;
            const float baseY = -0.19f; // пивот hero.png в 35 px от низа, низ диска - в 16 px
            const int texWidth = 256, texHeight = 128;
            const float ppu = texHeight / radius;
            const int scanPeriodPx = 20, scanHeightPx = 160;
            Color green = PhosphorBright;

            var dome = MakeGeneratedSprite("ShieldDome", texWidth, texHeight, ppu, (x, y) =>
            {
                float dx = x + 0.5f - texWidth * 0.5f, dy = y + 0.5f;
                return Mathf.Clamp01(texHeight - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            });
            var edgeSprite = MakeGeneratedSprite("ShieldDomeEdge", texWidth, texHeight, ppu, (x, y) =>
            {
                float dx = x + 0.5f - texWidth * 0.5f, dy = y + 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01(texHeight - distance + 0.5f) * Mathf.Clamp01(distance - (texHeight - 6f) + 0.5f);
            });
            var linesSprite = MakeGeneratedSprite("ShieldScanlines", texWidth, scanHeightPx, ppu,
                (x, y) => y % scanPeriodPx < 3 ? 1f : 0f);

            var shield = new GameObject("Shield", typeof(SpriteRenderer), typeof(SpriteMask));
            shield.transform.SetParent(player, false);
            shield.transform.localPosition = new Vector3(0f, baseY, 0f);
            var fill = shield.GetComponent<SpriteRenderer>();
            fill.sprite = dome;
            fill.color = new Color(green.r, green.g, green.b, 0.12f);
            fill.sortingOrder = 5;
            shield.GetComponent<SpriteMask>().sprite = dome;

            var lines = NewSpriteObject("Scanlines", linesSprite, 6);
            lines.transform.SetParent(shield.transform, false);
            var linesRenderer = lines.GetComponent<SpriteRenderer>();
            linesRenderer.color = new Color(green.r, green.g, green.b, 0.35f);
            linesRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var edge = NewSpriteObject("Edge", edgeSprite, 7);
            edge.transform.SetParent(shield.transform, false);
            var edgeRenderer = edge.GetComponent<SpriteRenderer>();
            edgeRenderer.color = green;

            var anim = shield.AddComponent<ShieldDome>();
            anim.fill = fill;
            anim.edge = edgeRenderer;
            anim.scanlines = linesRenderer;
            anim.scanPeriod = scanPeriodPx / ppu;
            return shield;
        }

        // Белая текстура по функции прозрачности (x, y от левого нижнего угла), пивот по центру низа
        static Sprite MakeGeneratedSprite(string name, int width, int height, float pixelsPerUnit, System.Func<int, int, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha(x, y));
            texture.SetPixels(pixels);
            texture.Apply();

            string path = $"{SpritesDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Transform BuildBunkers()
        {
            const float blockSize = 0.1f;
            float[] positionsX = { -4.5f, -1.5f, 1.5f, 4.5f };

            var root = new GameObject("Bunkers");
            for (int i = 0; i < positionsX.Length; i++)
            {
                string[] shape = BunkerShapes[i % BunkerShapes.Length];
                int width = shape[0].Length;
                int height = shape.Length;

                var bunker = new GameObject($"Bunker {i + 1}");
                bunker.transform.SetParent(root.transform);
                bunker.transform.position = new Vector3(positionsX[i], -4.5f, 0f);

                var grid = new BunkerBlock[height, width];
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        if (shape[row][col] != '#') continue;

                        // Каждый третий ряд с тёмной бороздкой, верхний край - светлый
                        Sprite body = row % 3 == 2 ? blockGrooveSprite : blockSprite;
                        var block = NewSpriteObject("Block", body, 0);
                        block.transform.SetParent(bunker.transform, false);
                        block.transform.localPosition = new Vector3((col - (width - 1) / 2f) * blockSize, -row * blockSize, 0f);
                        AddTrigger(block, false);
                        var bunkerBlock = block.AddComponent<BunkerBlock>();
                        bunkerBlock.bodySprite = body;
                        bunkerBlock.edgeSprite = blockTopSprite;
                        grid[row, col] = bunkerBlock;
                    }
                }

                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        BunkerBlock block = grid[row, col];
                        if (block == null) continue;
                        block.above = row > 0 ? grid[row - 1, col] : null;
                        block.below = row < height - 1 ? grid[row + 1, col] : null;
                        block.RefreshLook();
                    }
                }
            }
            return root.transform;
        }

        static void BuildGameUI(WaveManager waveManager, AbilityManager abilities)
        {
            Transform canvas = MakeCanvas().transform;
            var ui = canvas.gameObject.AddComponent<UIManager>();
            ui.waveManager = waveManager;

            var topLeft = new Vector2(0f, 1f);
            var top = new Vector2(0.5f, 1f);
            var topRight = new Vector2(1f, 1f);
            var center = new Vector2(0.5f, 0.5f);

            Transform safeArea = MakeSafeArea(canvas);
            // Аркадная шапка: маленькая тусклая подпись над ярким числом.
            // SCORE по центру, WAVE справа рядом с кнопкой паузы
            var scoreLabel = MakeText(safeArea, "ScoreLabel", "SCORE", 32, TextAlignmentOptions.Top, top, new Vector2(0f, -28f), new Vector2(400f, 50f));
            scoreLabel.color = PhosphorMid;
            ui.scoreText = MakeText(safeArea, "Score", "00000", 60, TextAlignmentOptions.Top, top, new Vector2(0f, -70f), new Vector2(400f, 80f));
            var waveLabel = MakeText(safeArea, "WaveLabel", "WAVE", 32, TextAlignmentOptions.Top, topRight, new Vector2(-200f, -28f), new Vector2(200f, 50f));
            waveLabel.color = PhosphorMid;
            ui.waveText = MakeText(safeArea, "Wave", "1/5", 48, TextAlignmentOptions.Top, topRight, new Vector2(-200f, -72f), new Vector2(200f, 70f));

            // Жизни: сердечки в ряд под кораблём (BuildPlayerHearts)
            ui.hearts = playerHearts;

            // Монеты: крупная иконка и число в левом верхнем углу
            var coinIcon = MakeChildImage(safeArea, "CoinIcon", coinSprite, Color.white);
            coinIcon.preserveAspect = true;
            var coinIconRect = coinIcon.rectTransform;
            coinIconRect.anchorMin = coinIconRect.anchorMax = coinIconRect.pivot = topLeft;
            coinIconRect.anchoredPosition = new Vector2(30f, -24f);
            coinIconRect.sizeDelta = new Vector2(100f, 100f);
            ui.coinsText = MakeText(safeArea, "Coins", "0", 72, TextAlignmentOptions.TopLeft, topLeft, new Vector2(145f, -30f), new Vector2(260f, 100f));
            ui.coinsText.color = TextColor;
            // Высокая: в аркаде бывает три строки - LOOP N, WAVE N и метки волны
            ui.bannerText = MakeText(canvas, "WaveBanner", "WAVE 1", 110, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(1200f, 420f));

            BuildBossBar(safeArea, ui);

            // Панель способностей: столбик у левого края, по центру экрана
            MakeAbilitySlot(safeArea, abilities, 0, shieldIconSprite, 220f);
            MakeAbilitySlot(safeArea, abilities, 1, bunkerIconSprite, 0f);
            MakeAbilitySlot(safeArea, abilities, 2, rapidFireIconSprite, -220f);

            // Обучение: окно подсказок, жёлтое кольцо-указатель и знак «зажми и веди».
            // Выше HUD, но ниже паузы и экранов конца игры (они создаются дальше)
            BuildTutorialUI(canvas, ui, abilities, waveManager, coinIconRect);

            // Круглая аркадная кнопка паузы в правом верхнем углу, подсвечивается при нажатии
            var pauseButton = MakeRoundButton(safeArea, "PauseButton", topRight, new Vector2(-95f, -85f), 130f);

            // Часы «утро-день-ночь» под кнопкой паузы: поворачиваются в начале каждой волны
            hudDayClock = MakeDayClock(safeArea, "DayClock", topRight, new Vector2(-95f, -240f), 130f);
            hudDayClock.waveManager = waveManager;
            waveManager.dayClock = hudDayClock; // новая волна ждёт поворота часов

            // Экраны конца игры: аркадная победа и окно Game Over
            ui.victoryScreen = BuildVictoryScreen(canvas);
            ui.gameOverWindow = BuildGameOverWindow(canvas);

            // Пауза
            var pause = canvas.gameObject.AddComponent<PauseMenu>();
            pause.pauseButton = pauseButton;
            var pausePanel = MakePanel(canvas, "PausePanel", new Color(0f, 0f, 0f, 0.7f));
            pause.panel = pausePanel;
            MakeText(pausePanel.transform, "Title", "PAUSED", 120, TextAlignmentOptions.Center, center, new Vector2(0f, 320f), new Vector2(1000f, 170f));
            // Аркадные кнопки как на экранах конца игры: RESUME главная (залитая)
            var mid = new Vector2(0.5f, 0.5f);
            var menuSize = new Vector2(640f, 150f);
            pause.resumeButton = MakeArcadeButton(pausePanel.transform, "ResumeButton", "RESUME", true, mid, new Vector2(0f, 80f), menuSize);
            pause.restartButton = MakeArcadeButton(pausePanel.transform, "RestartButton", "RESTART", false, mid, new Vector2(0f, -100f), menuSize);
            pause.menuButton = MakeArcadeButton(pausePanel.transform, "MenuButton", "MAIN MENU", false, mid, new Vector2(0f, -280f), menuSize);
            pausePanel.SetActive(false);
        }

        // Отдельный Canvas поверх экрана: рисуется после CRT-эффекта, поэтому веки «настоящие»
        static BlinkEffect BuildBlinkOverlay(WaveManager waveManager)
        {
            var go = new GameObject("BlinkOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0f;

            Sprite curve = MakeEyelidCurveSprite();
            var blink = go.AddComponent<BlinkEffect>();
            blink.waveManager = waveManager;
            MakeEyelid(go.transform, "TopLid", true, curve, out blink.topLid, out blink.topCurve);
            MakeEyelid(go.transform, "BottomLid", false, curve, out blink.bottomLid, out blink.bottomCurve);
            return blink;
        }

        // ================= Часы и сценка «наступает ночь» =================

        static DayClock hudDayClock;

        // Круглая рамка как у кнопки паузы, внутри вращается диск неба, сверху указатель
        static DayClock MakeDayClock(Transform parent, string name, Vector2 anchor, Vector2 position, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var frame = MakeChildImage(go.transform, "Frame", Art("round_outline", 100f, uiSquareSprite), Color.white);
            Stretch(frame.rectTransform);

            var disc = MakeChildImage(go.transform, "Disc", Art("clock_disc", 100f, uiSquareSprite), Color.white);
            disc.rectTransform.sizeDelta = Vector2.one * size * 0.84f;

            var pointer = MakeChildImage(go.transform, "Pointer", Art("clock_pointer", 100f, uiSquareSprite), Color.white);
            var pointerRect = pointer.rectTransform;
            pointerRect.anchorMin = pointerRect.anchorMax = new Vector2(0.5f, 1f);
            pointerRect.pivot = new Vector2(0.5f, 1f);
            pointerRect.anchoredPosition = new Vector2(0f, size * 0.06f);
            pointerRect.sizeDelta = new Vector2(size * 0.2f, size * 0.15f);

            var clock = go.AddComponent<DayClock>();
            clock.disc = disc.rectTransform;
            return clock;
        }

        // Сценка перед Боссом 2: свой Canvas под CRT (на камере) поверх игры и HUD, но под веками
        static NightCutscene BuildNightCutscene(BlinkEffect blink)
        {
            var go = new GameObject("NightCutscene", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = sceneCamera;
            canvas.planeDistance = 4.5f;
            canvas.sortingOrder = 150;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0f;

            // Затемнение на весь экран, оно же ловит касание
            var dimmer = go.GetComponent<Image>();
            dimmer.color = new Color(0.008f, 0.035f, 0.02f, 0.86f);
            dimmer.raycastTarget = true;

            var cutscene = go.AddComponent<NightCutscene>();
            cutscene.group = go.GetComponent<CanvasGroup>();
            cutscene.blink = blink;
            cutscene.hudClock = hudDayClock;

            var center = new Vector2(0.5f, 0.5f);
            cutscene.bigClock = MakeDayClock(go.transform, "BigClock", center, new Vector2(0f, 120f), 640f);

            var dialog = MakeChildImage(go.transform, "Dialog", windowSprite, Color.white);
            dialog.type = Image.Type.Sliced;
            dialog.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            dialog.rectTransform.sizeDelta = new Vector2(1200f, 240f);
            cutscene.dialog = dialog.gameObject;
            cutscene.line = MakeText(dialog.transform, "Line", "", 56, TextAlignmentOptions.Left, center, new Vector2(0f, 10f), new Vector2(1080f, 100f));
            cutscene.line.textWrappingMode = TextWrappingModes.NoWrap;
            cutscene.tapHint = MakeText(dialog.transform, "Tap", "", 34, TextAlignmentOptions.Right, center, new Vector2(0f, -78f), new Vector2(1080f, 50f));
            cutscene.tapHint.color = PhosphorMid;
            dialog.gameObject.SetActive(false);

            go.SetActive(false);
            return cutscene;
        }

        static void MakeEyelid(Transform parent, string name, bool top, Sprite curve, out RectTransform lid, out RectTransform edge)
        {
            float y = top ? 1f : 0f;
            var lidImage = MakeChildImage(parent, name, null, Color.black);
            lid = lidImage.rectTransform;
            lid.anchorMin = new Vector2(0f, y);
            lid.anchorMax = new Vector2(1f, y);
            lid.pivot = new Vector2(0.5f, y);
            lid.sizeDelta = Vector2.zero;

            // Изогнутый край века, прилегает к внутренней стороне
            var edgeImage = MakeChildImage(lid, "Edge", curve, Color.black);
            edge = edgeImage.rectTransform;
            edge.anchorMin = new Vector2(0f, 1f - y);
            edge.anchorMax = new Vector2(1f, 1f - y);
            edge.pivot = new Vector2(0.5f, y);
            edge.sizeDelta = Vector2.zero;
            if (!top) edge.localScale = new Vector3(1f, -1f, 1f);
        }

        // Белая заливка с выпуклым вниз краем: сверху сплошная, к краям сужается
        static Sprite MakeEyelidCurveSprite()
        {
            const int width = 256, height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f) * 2f - 1f;
                    float edgeFromTop = height * (1f - u * u);
                    float fromTop = height - 1 - y;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edgeFromTop - fromTop));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            string path = $"{SpritesDir}/EyelidCurve.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ================= Сцена SpinvMainMenu =================

        static void BuildMenuScene()
        {
            MakeCamera();

            // Декоративные пришельцы над заголовком: машут кадрами, шагают строем и покачиваются
            Sprite[][] decor = { squidFrames, crabFrames, octopusFrames };
            for (int i = 0; i < decor.Length; i++)
            {
                var go = NewSpriteObject("Decor " + decor[i][0].name, decor[i][0], 0);
                go.transform.position = new Vector3((i - 1) * 2.5f, 8f, 0f);
                go.transform.localScale = Vector3.one * 1.5f;
                var anim = go.AddComponent<MenuInvader>();
                anim.frames = decor[i];
                anim.bobPhase = i * 2.1f;
            }

            Transform canvas = MakeCanvas().transform;
            var menu = canvas.gameObject.AddComponent<SpinvMainMenu>();
            var center = new Vector2(0.5f, 0.5f);

            if (logoSprite != null)
            {
                var logo = MakeChildImage(canvas, "Logo", logoSprite, Color.white);
                logo.preserveAspect = true;
                logo.rectTransform.anchoredPosition = new Vector2(0f, 300f);
                logo.rectTransform.sizeDelta = new Vector2(1100f, 360f);
            }
            else
            {
                var title = MakeText(canvas, "Title", "TINY UFO", 110, TextAlignmentOptions.Center, center, new Vector2(0f, 250f), new Vector2(1200f, 200f));
                title.color = PlayerColor;
            }

            // Аркадная шапка: тусклая подпись HI-SCORE над ярким рекордом (монеты в меню не показываем)
            menu.highScoreLabel = MakeText(canvas, "HighScoreLabel", "HI-SCORE", 34, TextAlignmentOptions.Center, center, new Vector2(0f, 105f), new Vector2(500f, 50f));
            menu.highScoreLabel.color = PhosphorMid;
            menu.highScoreText = MakeText(canvas, "HighScore", "00000", 64, TextAlignmentOptions.Center, center, new Vector2(0f, 40f), new Vector2(500f, 80f));
            // Рекорд аркады (жёлтый): виден, когда аркада открыта, тогда рекорды встают в два столбика
            menu.arcadeScoreLabel = MakeText(canvas, "ArcadeScoreLabel", "ARCADE", 34, TextAlignmentOptions.Center, center, new Vector2(0f, 105f), new Vector2(500f, 50f));
            menu.arcadeScoreLabel.color = new Color(HighScoreYellow.r * 0.8f, HighScoreYellow.g * 0.8f, HighScoreYellow.b * 0.8f);
            menu.arcadeScoreText = MakeText(canvas, "ArcadeScore", "00000", 64, TextAlignmentOptions.Center, center, new Vector2(0f, 40f), new Vector2(500f, 80f));
            menu.arcadeScoreText.color = HighScoreYellow;

            // Аркадные кнопки как на экранах конца игры: PLAY главная (залитая).
            // Когда аркада открыта, PLAY называется CLASSIC, под ней жёлтая ARCADE MODE, QUIT уезжает ниже
            menu.playButton = MakeArcadeButton(canvas, "PlayButton", "PLAY", true, center, new Vector2(0f, -170f), new Vector2(640f, 150f));
            menu.arcadeButton = MakeArcadeButton(canvas, "ArcadeButton", "ARCADE MODE", false, center, new Vector2(0f, -350f), new Vector2(640f, 150f));
            menu.arcadeButton.normalColor = menu.arcadeButton.litColor = HighScoreYellow;
            menu.arcadeButton.normalLabelColor = HighScoreYellow;
            menu.arcadeButton.background.color = HighScoreYellow;
            menu.arcadeButton.label.color = HighScoreYellow;
            if (menu.arcadeButton.glow != null) menu.arcadeButton.glow.color = HighScoreYellow;
            // Справа от ARCADE MODE - жёлтая круглая «?», как пауза: обучение аркады (метки волн и циклы)
            menu.arcadeHelpButton = MakeRoundButton(canvas, "ArcadeHelpButton", center, new Vector2(425f, -350f), 130f, null, 0f, 0f, "?");
            menu.arcadeHelpButton.label.fontSize = 80f;
            menu.arcadeHelpButton.normalColor = menu.arcadeHelpButton.litColor = HighScoreYellow;
            menu.arcadeHelpButton.normalLabelColor = HighScoreYellow;
            menu.arcadeHelpButton.background.color = HighScoreYellow;
            menu.arcadeHelpButton.label.color = HighScoreYellow;
            if (menu.arcadeHelpButton.glow != null) menu.arcadeHelpButton.glow.color = HighScoreYellow;
            menu.tutorialButton = MakeArcadeButton(canvas, "TutorialButton", "TUTORIAL", false, center, new Vector2(0f, -350f), new Vector2(640f, 150f));
            menu.quitButton = MakeArcadeButton(canvas, "QuitButton", "QUIT", false, center, new Vector2(0f, -530f), new Vector2(640f, 150f));

            var controls = MakeText(canvas, "Controls", "Drag your finger to move\nShooting is automatic",
                44, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1200f, 160f));
            controls.color = new Color(0.13f, 0.76f, 0.29f);

            // Кубок в левом нижнем углу, под ним счётчик достижений
            var bottomLeft = new Vector2(0f, 0f);
            menu.trophyButton = MakeRoundButton(canvas, "TrophyButton", bottomLeft, new Vector2(130f, 260f), 170f, "icon_trophy", 0.56f, 0.56f);
            menu.trophyCount = MakeText(canvas, "TrophyCount", "0 / 0", 46, TextAlignmentOptions.Center, bottomLeft, new Vector2(30f, 110f), new Vector2(200f, 70f));
            menu.trophyCount.color = TextColor;
            menu.starMap = BuildStarMap(canvas);

            menu.quitEffect = BuildQuitOverlay();
            BuildAchievementToast(); // плашки, отложенные до меню (IMPOSTOR)
            BuildIntroOverlay();
            MakeEventSystem();
        }

        // Включение автомата при запуске: отдельный Canvas поверх меню, перехватывает нажатия, пока не исчезнет.
        // На камере, поэтому под CRT-эффектом: это часть картинки автомата
        static void BuildIntroOverlay()
        {
            var go = new GameObject("IntroBoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = sceneCamera;
            canvas.planeDistance = 4f;
            canvas.sortingOrder = 300;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0f;

            var intro = go.AddComponent<IntroBoot>();
            intro.group = go.GetComponent<CanvasGroup>();

            // Не "Background": под этим именем фон звёзд ищет SpaceInvadersBackground
            intro.background = MakeChildImage(go.transform, "BootScreen", null, Color.black);
            intro.background.raycastTarget = true;
            Stretch(intro.background.rectTransform);

            intro.bars = new Image[6];
            for (int i = 0; i < intro.bars.Length; i++)
            {
                intro.bars[i] = MakeChildImage(go.transform, $"Bar{i + 1}", null, Color.clear);
                var rect = intro.bars[i].rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }

            var noiseGo = new GameObject("Noise", typeof(RectTransform), typeof(RawImage));
            noiseGo.transform.SetParent(go.transform, false);
            intro.noise = noiseGo.GetComponent<RawImage>();
            intro.noise.raycastTarget = false;
            Stretch(intro.noise.rectTransform);

            string[] texts = { "TINY UFO SYSTEM", "ROM CHECK ........ OK", "RAM CHECK ........ OK", "PHOSPHOR WARMUP ... OK", "TAP TO START" };
            intro.lines = new TMP_Text[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                intro.lines[i] = MakeText(go.transform, $"Line{i + 1}", texts[i], 60, TextAlignmentOptions.TopLeft,
                    new Vector2(0f, 1f), new Vector2(94f, -640f - i * 128f), new Vector2(1150f, 90f));
                intro.lines[i].color = i == texts.Length - 1 ? TextColor : PlayerColor;
            }
        }

        // Выключение при выходе: тоже на камере, под CRT-эффектом
        static QuitEffect BuildQuitOverlay()
        {
            var go = new GameObject("QuitEffect", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = sceneCamera;
            canvas.planeDistance = 4f;
            canvas.sortingOrder = 290;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0f;

            var effect = go.AddComponent<QuitEffect>();
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            Stretch((RectTransform)content.transform);
            effect.content = content;

            effect.blackScreen = MakeChildImage(content.transform, "Black", null, Color.black);
            effect.blackScreen.raycastTarget = true;
            Stretch(effect.blackScreen.rectTransform);

            effect.screen = MakeChildImage(content.transform, "Screen", null, new Color(0.012f, 0.063f, 0.024f));
            Stretch(effect.screen.rectTransform);
            effect.message = MakeText(content.transform, "PowerOff", "POWER OFF", 86, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 140f));
            effect.message.color = TextColor;

            effect.glow = MakeChildImage(content.transform, "Glow", null, Color.clear);
            Stretch(effect.glow.rectTransform);

            effect.topBar = MakeBar(content.transform, "TopBar", 1f);
            effect.bottomBar = MakeBar(content.transform, "BottomBar", 0f);

            effect.line = MakeChildImage(content.transform, "Line", null, new Color(0.78f, 1f, 0.84f)).rectTransform;
            effect.dot = MakeChildImage(content.transform, "Dot", MakeSprite("QuitDot", CircleArt(32),
                new Dictionary<char, Color> { { '#', Color.white }, { '+', Color.white } }, 100), Color.white);
            return effect;
        }

        static RectTransform MakeBar(Transform parent, string name, float y)
        {
            var rect = MakeChildImage(parent, name, null, Color.black).rectTransform;
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // ================= Помощники =================

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Dictionary<char, Color> Palette(Color main)
        {
            return new Dictionary<char, Color> { { '#', main } };
        }

        static Sprite MakeSprite(string name, string[] rows, Dictionary<char, Color> palette, float pixelsPerUnit)
        {
            int height = rows.Length;
            int width = rows[0].Length;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char c = x < rows[y].Length ? rows[y][x] : '.';
                    // Строка 0 в рисунке - верх, а в текстуре y = 0 - низ
                    pixels[(height - 1 - y) * width + x] = palette.TryGetValue(c, out Color color) ? color : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            string path = $"{SpritesDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Простой 8-битный звук: квадратная волна со сдвигом частоты + шум, затухает к концу
        static AudioClip MakeSound(string name, float duration, float startFreq, float endFreq, float noise, float volume = 0.35f)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(sampleRate * duration);
            var random = new System.Random(name.Length * 7919);
            string path = $"{AudioDir}/{name}.wav";

            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + count * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);           // PCM
                writer.Write((short)1);           // моно
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);     // байт в секунду
                writer.Write((short)2);           // байт на сэмпл
                writer.Write((short)16);          // бит на сэмпл
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(count * 2);

                float phase = 0f;
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / count;
                    phase = (phase + Mathf.Lerp(startFreq, endFreq, t) / sampleRate) % 1f;
                    float square = phase < 0.5f ? 1f : -1f;
                    float hiss = (float)(random.NextDouble() * 2.0 - 1.0);
                    float sample = Mathf.Lerp(square, hiss, noise) * (1f - t) * volume;
                    writer.Write((short)(sample * short.MaxValue));
                }
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static GameObject NewSpriteObject(string name, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        // Все коллайдеры - триггеры. Rigidbody2D (Kinematic) нужен только движущимся пулям и монеткам
        static void AddTrigger(GameObject go, bool withRigidbody)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
                box.size = renderer.sprite.bounds.size;

            if (!withRigidbody) return;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }

        static GameObject SavePrefab(GameObject go)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/{go.name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void MakeCamera()
        {
            if (sceneCamera != null) return; // одна камера на сцену: меню и игра её делят
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            sceneCamera = cam;
            cam.orthographic = true;
            cam.orthographicSize = 13f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.063f, 0.024f);
            go.AddComponent<AudioListener>();
        }

        static Canvas MakeCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // На камере, а не поверх экрана: иначе CRT-эффект не ляжет на интерфейс
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = sceneCamera;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0f;
            return canvas;
        }

        static Transform MakeSafeArea(Transform canvas)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go.transform;
        }

        static void MakeEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return; // один на сцену
            var go = new GameObject("EventSystem", typeof(EventSystem));
    #if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
    #elif ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    #endif
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
            TextAlignmentOptions alignment, Vector2 anchor, Vector2 position, Vector2 boxSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false; // Текст не должен перехватывать палец

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;
            return label;
        }

        static GameObject MakePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        // Круг: '#' обод, '+' заливка
        static string[] CircleArt(int size)
        {
            var rows = new string[size];
            float center = (size - 1) / 2f;
            float outer = size / 2f;
            for (int y = 0; y < size; y++)
            {
                var row = new StringBuilder(size);
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    row.Append(distance > outer ? '.' : distance > outer - 1.5f ? '#' : '+');
                }
                rows[y] = row.ToString();
            }
            return rows;
        }

        static Image MakeChildImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static readonly string[] SkullArt =
        {
            "..#######..",
            ".#########.",
            "###########",
            "###########",
            "##...#...##",
            "##...#...##",
            "###########",
            ".####.####.",
            "..#######..",
            "..#.#.#.#..",
            "..#.#.#.#..",
        };

        // Здоровье босса по центру экрана над боссом: сверху подпись «черепок BOSS HP», под ней полоса.
        // Полоса меняет цвет от зелёного к жёлтому и красному, за ней тянется белый след отнятого здоровья.
        // Ширина 840 (x 222-1062): справа на этой высоте часы (с x 1124)
        static void BuildBossBar(Transform parent, UIManager ui)
        {
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var center = new Vector2(0.5f, 0.5f);
            const float width = 840f, labelHeight = 44f, barHeight = 32f, gap = 6f;

            var bossBar = new GameObject("BossBar", typeof(RectTransform));
            bossBar.transform.SetParent(parent, false);
            var barRect = (RectTransform)bossBar.transform;
            barRect.anchorMin = barRect.anchorMax = top;
            barRect.pivot = bottom;
            barRect.anchoredPosition = new Vector2(0f, -245f - barHeight / 2f); // центр полосы на -245: над боссом с небольшим зазором
            barRect.sizeDelta = new Vector2(width, labelHeight + gap + barHeight);
            ui.bossBar = bossBar;

            // Подпись по центру: черепок слева от текста
            var labelRow = new GameObject("LabelRow", typeof(RectTransform));
            labelRow.transform.SetParent(bossBar.transform, false);
            var rowRect = (RectTransform)labelRow.transform;
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = top;
            rowRect.anchoredPosition = Vector2.zero;
            rowRect.sizeDelta = new Vector2(200f, labelHeight);

            var skull = MakeChildImage(labelRow.transform, "Skull", MakeSprite("Icon_Skull", SkullArt, Palette(TextColor), 100), Color.white);
            skull.preserveAspect = true;
            var skullRect = skull.rectTransform;
            skullRect.anchorMin = skullRect.anchorMax = skullRect.pivot = new Vector2(0f, 0.5f);
            skullRect.anchoredPosition = Vector2.zero;
            skullRect.sizeDelta = new Vector2(38f, 38f);

            var label = MakeText(labelRow.transform, "Label", "BOSS HP", 32, TextAlignmentOptions.Center, center, new Vector2(24f, 0f), new Vector2(152f, labelHeight));
            label.color = TextColor;

            // Рамка: яркая зелёная подложка и тёмная середина поверх. Не мятная: у самого верха экрана CRT
            // сильно сдвигает красный и синий каналы, и у светлой тонкой рамки появлялась пурпурная кайма
            var frame = MakeChildImage(bossBar.transform, "Frame", uiSquareSprite, PhosphorBright);
            var frameRect = frame.rectTransform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = bottom;
            frameRect.anchoredPosition = Vector2.zero;
            frameRect.sizeDelta = new Vector2(width, barHeight);

            var inside = MakeChildImage(frame.transform, "Inside", uiSquareSprite, ScreenDark);
            Inset(inside.rectTransform, 4f);

            ui.bossBarLag = MakeChildImage(frame.transform, "DamageTrail", uiSquareSprite, new Color(1f, 1f, 1f, 0.85f));
            ui.bossBarLag.type = Image.Type.Filled;
            ui.bossBarLag.fillMethod = Image.FillMethod.Horizontal;
            Inset(ui.bossBarLag.rectTransform, 7f);

            ui.bossBarFill = MakeChildImage(frame.transform, "Fill", uiSquareSprite, PhosphorBright);
            ui.bossBarFill.type = Image.Type.Filled;
            ui.bossBarFill.fillMethod = Image.FillMethod.Horizontal;
            Inset(ui.bossBarFill.rectTransform, 7f);
        }

        static void Inset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        static void MakeAbilitySlot(Transform parent, AbilityManager manager, int index, Sprite iconSprite, float y)
        {
            var go = new GameObject($"Ability{index + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(24f, y);
            rect.sizeDelta = new Vector2(200f, 200f);

            var slot = go.AddComponent<AbilityButton>();
            slot.manager = manager;
            slot.index = index;
            // Круглая ячейка как кнопка паузы: кольцо с тёмной серединой
            slot.background = go.GetComponent<Image>();
            slot.background.sprite = Art("round_outline", 100f, null);
            slot.button = go.GetComponent<Button>();
            slot.button.targetGraphic = slot.background;
            slot.button.transition = Selectable.Transition.None;

            slot.icon = MakeChildImage(go.transform, "Icon", iconSprite, Color.white);
            slot.icon.preserveAspect = true;
            slot.icon.rectTransform.sizeDelta = new Vector2(110f, 110f);

            // Перезарядка: яркое кольцо поверх тусклой обводки дорастает по часовой стрелке
            slot.cooldownRing = MakeChildImage(go.transform, "CooldownRing", MakeRingSprite("AbilityRing", 0.11f), PhosphorBright);
            slot.cooldownRing.type = Image.Type.Filled;
            slot.cooldownRing.fillMethod = Image.FillMethod.Radial360;
            slot.cooldownRing.fillOrigin = (int)Image.Origin360.Top;
            slot.cooldownRing.fillClockwise = true;
            slot.cooldownRing.fillAmount = 0f;
            Stretch(slot.cooldownRing.rectTransform);

            var center = new Vector2(0.5f, 0.5f);
            slot.cooldownText = MakeText(go.transform, "CooldownText", "", 80, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(200f, 120f));
            slot.cooldownText.color = PhosphorBright;

            // Цена на круглой «монетке» в правом нижнем углу
            slot.costBadge = MakeChildImage(go.transform, "CostBadge", Art("round_fill", 100f, uiSquareSprite), PhosphorBright);
            var badgeRect = slot.costBadge.rectTransform;
            badgeRect.anchoredPosition = new Vector2(72f, -72f);
            badgeRect.sizeDelta = new Vector2(80f, 80f);
            slot.costText = MakeText(slot.costBadge.transform, "Cost", "", 44, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(80f, 80f));
        }

        // Белое сглаженное кольцо во всю текстуру; thickness - толщина в долях радиуса
        static Sprite MakeRingSprite(string name, float thickness)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float outer = size * 0.5f - 1f;
            float inner = outer * (1f - thickness);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(outer - distance + 0.5f) * Mathf.Clamp01(distance - inner + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            string path = $"{SpritesDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ================= Экраны конца игры =================

        static readonly Color ScreenDark = new Color(0.024f, 0.082f, 0.043f);   // #06150B
        static readonly Color PhosphorMid = new Color(0.28f, 0.83f, 0.42f);     // #48D46C
        static readonly Color PhosphorLine = new Color(0.12f, 0.54f, 0.24f);    // #1F8A3E
        static readonly Color PhosphorBright = new Color(0.22f, 1f, 0.42f);     // #39FF6B
        static readonly Color HighScoreYellow = new Color(1f, 0.83f, 0.35f);    // #FFD45A

        static TextMeshProUGUI TopText(Transform parent, string name, string text, float size, float x, float y,
            float width, TextAlignmentOptions alignment, Color color)
        {
            var label = MakeText(parent, name, text, size, alignment, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(width, size * 1.5f));
            label.color = color;
            return label;
        }

        static void TopLine(Transform parent, float y)
        {
            var line = MakeChildImage(parent, "Line", uiSquareSprite, PhosphorLine);
            var rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(1100f, 8f);
        }

        static void AddBlink(Component text, float period = 1f)
        {
            text.gameObject.AddComponent<BlinkGraphic>().period = period;
        }

        // Экран достижений: звёзды-созвездия по разделам, описание выбранной звезды печатается в окне.
        // Координаты звёзд - в макете 300x260, порядок как в AchievementId
        static StarMapScreen BuildStarMap(Transform canvas)
        {
            Vector2[] points =
            {
                new Vector2(40, 40), new Vector2(105, 30), new Vector2(175, 28), new Vector2(235, 45),
                new Vector2(275, 95), new Vector2(220, 110), new Vector2(160, 80), new Vector2(35, 130),
                new Vector2(85, 160), new Vector2(135, 135), new Vector2(205, 205), new Vector2(265, 180),
            };
            int[] from = { 0, 2, 3, 4, 5, 6, 7, 8, 10 };
            int[] to = { 1, 3, 4, 5, 6, 2, 8, 9, 11 };
            const float mapScale = 3.9f;
            const float mapShiftY = 60f;
            Vector2 Place(Vector2 p) => new Vector2((p.x - 150f) * mapScale, (130f - p.y) * mapScale + mapShiftY);

            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);

            var screen = canvas.gameObject.AddComponent<StarMapScreen>();
            var panel = MakeChildImage(canvas, "StarMapPanel", null, ScreenDark);
            panel.raycastTarget = true; // перекрывает кнопки меню
            Stretch(panel.rectTransform);
            screen.root = panel.gameObject;
            Transform root = panel.transform;

            MakeText(root, "Title", "STAR MAP", 56, TextAlignmentOptions.Center, top, new Vector2(0f, -210f), new Vector2(900f, 80f)).color = PhosphorMid;
            screen.counterText = MakeText(root, "Counter", "0 / 0", 90, TextAlignmentOptions.Center, top, new Vector2(0f, -300f), new Vector2(900f, 120f));
            screen.counterText.color = PhosphorBright;

            // Линии созвездий: под звёздами
            screen.lines = new Image[from.Length];
            screen.lineFrom = from;
            screen.lineTo = to;
            for (int i = 0; i < from.Length; i++)
            {
                Vector2 a = Place(points[from[i]]), b = Place(points[to[i]]);
                var line = MakeChildImage(root, $"Line{i + 1}", uiSquareSprite, PhosphorLine);
                var rect = line.rectTransform;
                rect.anchoredPosition = (a + b) * 0.5f;
                rect.sizeDelta = new Vector2((b - a).magnitude, 5f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                screen.lines[i] = line;
            }

            var ring = MakeChildImage(root, "SelectionRing", MakeRingSprite("StarRing", 0.08f), HighScoreYellow);
            ring.rectTransform.sizeDelta = new Vector2(190f, 190f);
            screen.selectionRing = ring.rectTransform;

            var starSprite = Art("star", 100f, uiSquareSprite);
            screen.stars = new Image[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var star = MakeChildImage(root, $"Star{i + 1}", starSprite, Color.white);
                star.preserveAspect = true;
                star.raycastTarget = true;
                star.rectTransform.anchoredPosition = Place(points[i]);
                star.rectTransform.sizeDelta = new Vector2(140f, 140f);
                var button = star.gameObject.AddComponent<Button>();
                button.targetGraphic = star;
                button.transition = Selectable.Transition.None;
                screen.stars[i] = star;
            }

            // Окно описания - как окно диалога в сценках
            var dialog = MakeChildImage(root, "Dialog", windowSprite, Color.white);
            dialog.type = Image.Type.Sliced;
            dialog.rectTransform.anchoredPosition = new Vector2(0f, -760f);
            dialog.rectTransform.sizeDelta = new Vector2(1200f, 270f);
            screen.dialogTitle = MakeText(dialog.transform, "Name", "", 56, TextAlignmentOptions.Left, center, new Vector2(0f, 62f), new Vector2(1080f, 80f));
            screen.dialogTitle.color = PhosphorBright;
            screen.dialogDescription = MakeText(dialog.transform, "Description", "", 44, TextAlignmentOptions.TopLeft, center, new Vector2(0f, -38f), new Vector2(1080f, 120f));

            screen.backButton = MakeArcadeButton(root, "BackButton", "BACK", false, center, new Vector2(0f, -1040f), new Vector2(520f, 140f));
            return screen;
        }

        // Плашка «ACHIEVEMENT UNLOCKED» поверх игры: свой канвас, чтобы быть выше экранов конца игры
        static void BuildAchievementToast()
        {
            var canvas = MakeCanvas();
            canvas.gameObject.name = "AchievementToastCanvas";
            canvas.sortingOrder = 250;
            canvas.planeDistance = 4.5f;

            var toast = canvas.gameObject.AddComponent<AchievementToast>();
            var top = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);

            var panel = MakeChildImage(canvas.transform, "Panel", windowSprite, Color.white);
            panel.type = Image.Type.Sliced;
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = top;
            rect.pivot = center;
            rect.sizeDelta = new Vector2(1000f, 190f);
            toast.panel = rect;

            var icon = MakeChildImage(panel.transform, "Trophy", Art("icon_trophy", 100f, null), HighScoreYellow);
            icon.preserveAspect = true;
            icon.rectTransform.anchoredPosition = new Vector2(-400f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(120f, 120f);

            MakeText(panel.transform, "Header", "ACHIEVEMENT UNLOCKED", 38, TextAlignmentOptions.Left, center, new Vector2(70f, 38f), new Vector2(760f, 56f)).color = PhosphorMid;
            toast.titleText = MakeText(panel.transform, "Title", "", 58, TextAlignmentOptions.Left, center, new Vector2(70f, -32f), new Vector2(760f, 80f));
            toast.titleText.color = PhosphorBright;
        }

        static void BuildTutorialUI(Transform canvas, UIManager ui, AbilityManager abilities, WaveManager waveManager, RectTransform coinIcon)
        {
            var center = new Vector2(0.5f, 0.5f);
            var root = new GameObject("Tutorial", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch((RectTransform)root.transform);

            var tutorial = root.AddComponent<SpinvTutorial>();
            tutorial.root = root;
            tutorial.game = Object.FindAnyObjectByType<SpinvGameManager>();
            tutorial.waves = waveManager;
            tutorial.grid = waveManager.grid;
            tutorial.player = abilities.player;
            tutorial.abilities = abilities;
            tutorial.coinPrefab = coinPrefab;
            tutorial.coinCounter = coinIcon;
            tutorial.waveText = ui.waveText;
            tutorial.scoreCounter = ui.scoreText.rectTransform;
            tutorial.canvasRect = (RectTransform)canvas;
            tutorial.uiCamera = sceneCamera;

            // Окно подсказки - как окно диалога в сценках, выше слотов способностей (не закрывает щит).
            // Фон полупрозрачный, текст - нет
            var window = MakeChildImage(root.transform, "TipWindow", windowSprite, new Color(1f, 1f, 1f, 0.5f));
            window.type = Image.Type.Sliced;
            window.rectTransform.anchoredPosition = new Vector2(0f, 700f);
            window.rectTransform.sizeDelta = new Vector2(1150f, 240f);
            tutorial.tipText = MakeText(window.transform, "Tip", "", 46, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(1050f, 200f));
            tutorial.tipText.textWrappingMode = TextWrappingModes.Normal;

            var ring = MakeChildImage(root.transform, "Pointer", MakeRingSprite("StarRing", 0.08f), HighScoreYellow);
            ring.rectTransform.sizeDelta = new Vector2(200f, 200f);
            tutorial.ring = ring.rectTransform;

            var drag = MakeChildImage(root.transform, "DragHint", Art("icon_drag", 100f, uiSquareSprite), new Color(0.65f, 0.96f, 0.73f));
            drag.preserveAspect = true;
            drag.rectTransform.sizeDelta = new Vector2(260f, 130f);
            tutorial.dragHint = drag.rectTransform;
        }

        // Аркадная кнопка-капсула: filled - залитая (главная), иначе с обводкой
        static PressableButton MakeArcadeButton(Transform parent, string name, string label, bool filled,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var button = go.AddComponent<PressableButton>();

            if (btnGlowSprite != null)
            {
                button.glow = MakeChildImage(go.transform, "Glow", btnGlowSprite, Color.white);
                button.glow.rectTransform.sizeDelta = new Vector2(size.x * 700f / 540f, size.y * 310f / 150f);
            }

            // Не "Background": под этим именем фон игры ищет SpaceInvadersBackground и, найдя, не добавляет его
            button.background = MakeChildImage(go.transform, "Plate", filled ? btnFillSprite : btnOutlineSprite, Color.white);
            button.background.raycastTarget = true;
            Stretch(button.background.rectTransform);

            var mint = new Color(0.66f, 0.96f, 0.74f);
            button.normalSprite = filled ? btnFillSprite : btnOutlineSprite;
            button.normalColor = filled ? mint : Color.white;
            button.normalLabelColor = filled ? ScreenDark : mint;
            button.litSprite = btnFillSprite;
            button.litColor = filled ? new Color(0.95f, 1f, 0.96f) : mint;
            button.litLabelColor = ScreenDark;

            button.label = MakeText(go.transform, "Label", label, size.y * 0.37f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            button.label.color = button.normalLabelColor;
            return button;
        }

        // Круглая аркадная кнопка со значком паузы: кольцо, при нажатии заливка и свечение
        static PressableButton MakeRoundButton(Transform parent, string name, Vector2 anchor, Vector2 position, float size,
            string iconName = "icon_pause", float iconWidth = 0.32f, float iconHeight = 0.36f, string fallbackLabel = "II")
        {
            var button = MakeArcadeButton(parent, name, "", false, anchor, position, new Vector2(size, size));
            var outline = Art("round_outline", 100f, null);
            var fill = Art("round_fill", 100f, null);
            var glow = Art("round_glow", 100f, null);
            if (outline == null || fill == null) return button; // без картинок остаётся капсула

            button.normalSprite = outline;
            button.litSprite = fill;
            button.background.sprite = outline;
            if (button.glow != null && glow != null)
            {
                button.glow.sprite = glow;
                button.glow.rectTransform.sizeDelta = Vector2.one * size * 310f / 150f;
            }

            var iconSprite = iconName != null ? Art(iconName, 100f, null) : null;
            if (iconSprite != null)
            {
                var icon = MakeChildImage(button.transform, "Icon", iconSprite, button.normalLabelColor);
                icon.rectTransform.sizeDelta = new Vector2(size * iconWidth, size * iconHeight);
                button.icon = icon;
            }
            else
            {
                button.label.text = fallbackLabel;
            }
            return button;
        }

        // Поле ввода TextMeshPro в стиле кнопки-обводки. Привязка к верху родителя
        static TMP_InputField MakeInputField(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var frame = go.GetComponent<Image>();
            frame.sprite = btnOutlineSprite;
            frame.color = Color.white;

            // Область текста с обрезкой по краям
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(40f, 10f);
            areaRect.offsetMax = new Vector2(-40f, -10f);

            var center = new Vector2(0.5f, 0.5f);
            var placeholder = MakeText(area.transform, "Placeholder", "NAME", 60, TextAlignmentOptions.Center, center, Vector2.zero, Vector2.zero);
            placeholder.color = PhosphorLine;
            Stretch(placeholder.rectTransform);
            var text = MakeText(area.transform, "Text", "", 60, TextAlignmentOptions.Center, center, Vector2.zero, Vector2.zero);
            Stretch(text.rectTransform);

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = frame;
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = 60;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 8;
            input.customCaretColor = true;
            input.caretColor = TextColor;
            input.caretWidth = 4;
            input.selectionColor = new Color(0.28f, 0.83f, 0.42f, 0.45f);
            input.keyboardType = TouchScreenKeyboardType.Default;
            return input;
        }

        static VictoryScreen BuildVictoryScreen(Transform canvas)
        {
            var panel = MakePanel(canvas, "VictoryScreen", ScreenDark);
            var screen = panel.AddComponent<VictoryScreen>();
            Transform area = MakeSafeArea(panel.transform);
            var left = TextAlignmentOptions.Left;
            var right = TextAlignmentOptions.Right;
            var mid = TextAlignmentOptions.Center;


            screen.titleText = TopText(area, "Title", "CONGRATULATIONS!", 96, 0f, -60f, 1240f, mid, TextColor);
            AddBlink(screen.titleText);
            screen.subtitleText = TopText(area, "Subtitle", "EARTH IS SAVED\nALL 5 WAVES CLEARED", 44, 0f, -240f, 1200f, mid, PhosphorMid);
            screen.subtitleText.rectTransform.sizeDelta = new Vector2(1200f, 140f);
            TopLine(area, -420f);

            // Подсчёт очков: подпись слева, число справа
            screen.scoreLabel = TopText(area, "ScoreLabel", "SCORE", 48, 0f, -470f, 1080f, left, TextColor);
            screen.scoreValue = TopText(area, "ScoreValue", "", 48, 0f, -470f, 1080f, right, TextColor);
            screen.livesLabel = TopText(area, "LivesLabel", "LIVES", 48, 0f, -560f, 1080f, left, TextColor);
            screen.livesValue = TopText(area, "LivesValue", "", 48, 0f, -560f, 1080f, right, TextColor);
            screen.coinsLabel = TopText(area, "CoinsLabel", "COINS", 48, 0f, -650f, 1080f, left, TextColor);
            screen.coinsValue = TopText(area, "CoinsValue", "", 48, 0f, -650f, 1080f, right, TextColor);
            screen.totalLabel = TopText(area, "TotalLabel", "TOTAL", 62, 0f, -755f, 1080f, left, TextColor);
            screen.totalValue = TopText(area, "TotalValue", "", 62, 0f, -755f, 1080f, right, TextColor);

            screen.newHighScore = TopText(area, "NewHighScore", "NEW HIGH SCORE!", 54, 0f, -870f, 1200f, mid, HighScoreYellow);
            AddBlink(screen.newHighScore);
            TopLine(area, -970f);

            // Таблица TOP 5
            var table = new GameObject("Table", typeof(RectTransform));
            table.transform.SetParent(area, false);
            Stretch((RectTransform)table.transform);
            screen.tableRoot = table;
            screen.tableTitle = TopText(table.transform, "TableTitle", "- TOP 5 -", 40, 0f, -1005f, 600f, mid, PhosphorMid);

            string[] ranks = { "1ST", "2ND", "3RD", "4TH", "5TH" };
            screen.rankTexts = new TMP_Text[ranks.Length];
            screen.nameTexts = new TMP_Text[ranks.Length];
            screen.scoreTexts = new TMP_Text[ranks.Length];
            screen.rowHighlights = new Image[ranks.Length];
            for (int i = 0; i < ranks.Length; i++)
            {
                float y = -1080f - i * 90f;
                var highlight = MakeChildImage(table.transform, $"Row{i + 1}", uiSquareSprite, new Color(0.07f, 0.23f, 0.12f));
                var rect = highlight.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, y + 6f);
                rect.sizeDelta = new Vector2(1100f, 84f);
                screen.rowHighlights[i] = highlight;

                screen.rankTexts[i] = TopText(table.transform, $"Rank{i + 1}", ranks[i], 46, 0f, y, 1040f, left, PhosphorMid);
                // Колонка имени шириной под 8 символов
                screen.nameTexts[i] = TopText(table.transform, $"Name{i + 1}", "---", 46, 10f, y, 560f, left, TextColor);
                screen.scoreTexts[i] = TopText(table.transform, $"Score{i + 1}", "00000", 46, 0f, y, 1040f, right, TextColor);
            }

            // Ввод имени: поле с клавиатурой (экранной на телефоне) и OK
            var initials = new GameObject("Initials", typeof(RectTransform));
            initials.transform.SetParent(table.transform, false);
            Stretch((RectTransform)initials.transform);
            screen.initialsPanel = initials;
            screen.initialsHint = TopText(initials.transform, "Hint", "ENTER YOUR NAME", 36, 0f, -1540f, 800f, mid, PhosphorMid);
            screen.initialsInput = MakeInputField(initials.transform, "NameInput", new Vector2(-130f, -1670f), new Vector2(620f, 130f));
            screen.initialsOkButton = MakeArcadeButton(initials.transform, "NameOk", "OK", false,
                new Vector2(0.5f, 1f), new Vector2(330f, -1670f), new Vector2(220f, 130f));

            // Кнопок меню и рестарта нет: после ввода имени - POWER OFF и выход из игры
            screen.quitEffect = BuildQuitOverlay();

            // Окно «???:» как в сценке с часами: «You are not Gekata.» для попытки ввести имя автора
            // и «Arcade mode unlocked.» после ввода своего имени (с мигающим TAP)
            var impostorDialog = MakeChildImage(area, "ImpostorDialog", windowSprite, Color.white);
            impostorDialog.type = Image.Type.Sliced;
            impostorDialog.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            impostorDialog.rectTransform.sizeDelta = new Vector2(1200f, 240f);
            screen.impostorDialog = impostorDialog.gameObject;
            screen.impostorLine = MakeText(impostorDialog.transform, "Line", "", 56, TextAlignmentOptions.Left,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 100f));
            screen.impostorLine.textWrappingMode = TextWrappingModes.NoWrap;
            screen.dialogTapHint = MakeText(impostorDialog.transform, "Tap", "", 34, TextAlignmentOptions.Right,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(1080f, 50f));
            screen.dialogTapHint.color = PhosphorMid;
            impostorDialog.gameObject.SetActive(false);

            // Корабль игрока под таблицей: после IMPOSTOR рассыпается в пыль, затем подпись красным
            var heroArt = AssetDatabase.LoadAssetAtPath<Sprite>(GameArtDir + "/hero.png");
            var ship = MakeChildImage(area, "ImpostorShip", heroArt, Color.white);
            var shipRect = ship.rectTransform;
            shipRect.anchorMin = shipRect.anchorMax = shipRect.pivot = new Vector2(0.5f, 1f);
            shipRect.anchoredPosition = new Vector2(0f, -1600f);
            Vector2 shipPixels = ship.sprite != null ? ship.sprite.rect.size : new Vector2(104f, 112f);
            shipRect.sizeDelta = shipPixels * (280f / shipPixels.y); // высота 280, пропорции картинки
            screen.impostorShip = ship;
            ship.gameObject.SetActive(false);
            screen.impostorCaption = TopText(area, "ImpostorCaption", "", 44, 0f, -1920f, 1200f, TextAlignmentOptions.Center, new Color(1f, 0.29f, 0.24f));

            panel.SetActive(false);
            return screen;
        }

        static GameOverWindow BuildGameOverWindow(Transform canvas)
        {
            var root = new GameObject("GameOverWindow", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch((RectTransform)root.transform);
            var window = root.AddComponent<GameOverWindow>();

            // Затемнение перехватывает касания, чтобы игра под окном не реагировала
            window.dimmer = MakeChildImage(root.transform, "Dimmer", null, new Color(0.01f, 0.04f, 0.02f, 0f));
            window.dimmer.raycastTarget = true;
            Stretch(window.dimmer.rectTransform);

            var frame = MakeChildImage(root.transform, "Window", windowSprite, Color.white);
            frame.type = Image.Type.Sliced;
            window.window = frame.rectTransform;
            window.window.sizeDelta = new Vector2(1200f, 1340f);
            Transform w = frame.transform;
            var left = TextAlignmentOptions.Left;
            var right = TextAlignmentOptions.Right;
            var mid = TextAlignmentOptions.Center;

            window.titleTop = TopText(w, "TitleTop", "GAME", 140, 0f, -70f, 1000f, mid, TextColor);
            window.titleBottom = TopText(w, "TitleBottom", "OVER", 140, 0f, -230f, 1000f, mid, TextColor);
            TopLine(w, -440f);

            float[] rows = { -490f, -580f, -670f, -760f };
            window.scoreLabel = TopText(w, "ScoreLabel", "", 50, 0f, rows[0], 1040f, left, PhosphorMid);
            window.scoreValue = TopText(w, "ScoreValue", "", 50, 0f, rows[0], 1040f, right, TextColor);
            window.waveLabel = TopText(w, "WaveLabel", "", 50, 0f, rows[1], 1040f, left, PhosphorMid);
            window.waveValue = TopText(w, "WaveValue", "", 50, 0f, rows[1], 1040f, right, TextColor);
            window.coinsLabel = TopText(w, "CoinsLabel", "", 50, 0f, rows[2], 1040f, left, PhosphorMid);
            window.coinsValue = TopText(w, "CoinsValue", "", 50, 0f, rows[2], 1040f, right, TextColor);
            window.bestLabel = TopText(w, "BestLabel", "", 50, 0f, rows[3], 1040f, left, PhosphorMid);
            window.bestValue = TopText(w, "BestValue", "", 50, 0f, rows[3], 1040f, right, TextColor);

            window.newHighScore = TopText(w, "NewHighScore", "", 46, 0f, -870f, 1100f, mid, HighScoreYellow);
            AddBlink(window.newHighScore);
            window.newHighScore.GetComponent<BlinkGraphic>().enabled = false;

            var top = new Vector2(0.5f, 1f);
            window.menuButton = MakeArcadeButton(w, "MenuButton", "", false, top, new Vector2(-275f, -1040f), new Vector2(520f, 140f));
            window.restartButton = MakeArcadeButton(w, "RestartButton", "", true, top, new Vector2(275f, -1040f), new Vector2(520f, 140f));

            window.insertCoin = TopText(w, "InsertCoin", "", 36, 0f, -1190f, 800f, mid, PhosphorMid);
            AddBlink(window.insertCoin);
            window.insertCoin.GetComponent<BlinkGraphic>().enabled = false;

            root.SetActive(false);
            return window;
        }

    }
}
