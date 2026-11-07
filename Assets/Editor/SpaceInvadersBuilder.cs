using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Сборщик игры: Tools → Space Invaders → Build Game
// Рисует спрайты, генерирует звуки, создаёт префабы, 5 волн, сцены MainMenu и Game
// и добавляет сцены в Build Settings. Всё созданное потом можно менять руками в редакторе.
public static class SpaceInvadersBuilder
{
    const string SpritesDir = "Assets/Sprites";
    const string GameArtDir = "Assets/GameArt";
    const string AudioDir = "Assets/Audio";
    const string PrefabsDir = "Assets/Prefabs";
    const string WavesDir = "Assets/Waves";
    const string ScenesDir = "Assets/Scenes";
    const string MenuScenePath = ScenesDir + "/MainMenu.unity";
    const string GameScenePath = ScenesDir + "/Game.unity";
    const float BodyScale = 1.8f; // Игрок на вертикальном экране
    const float InvaderScale = 1.3333f;
    const float PickupScale = 3f; // Монетка и хилка
    const float MissilePpu = 624f;

    // ---------- Пиксель-арт ('#' - основной цвет, '+' - второй цвет, '.' - пусто) ----------

    static readonly string[] PlayerArt =
    {
        "......#......",
        ".....###.....",
        ".....###.....",
        ".###########.",
        "#############",
        "#############",
        "#############",
        "#############",
    };

    static readonly string[] SquidArt1 =
    {
        "...##...",
        "..####..",
        ".######.",
        "##.##.##",
        "########",
        "..#..#..",
        ".#.##.#.",
        "#.#..#.#",
    };

    static readonly string[] SquidArt2 =
    {
        "...##...",
        "..####..",
        ".######.",
        "##.##.##",
        "########",
        ".#.##.#.",
        "#......#",
        ".#....#.",
    };

    static readonly string[] CrabArt1 =
    {
        "..#.....#..",
        "...#...#...",
        "..#######..",
        ".##.###.##.",
        "###########",
        "#.#######.#",
        "#.#.....#.#",
        "...##.##...",
    };

    static readonly string[] CrabArt2 =
    {
        "..#.....#..",
        "#..#...#..#",
        "#.#######.#",
        "###.###.###",
        "###########",
        ".#########.",
        "..#.....#..",
        ".#.......#.",
    };

    static readonly string[] OctopusArt1 =
    {
        "....####....",
        ".##########.",
        "############",
        "###..##..###",
        "############",
        "...##..##...",
        "..##.##.##..",
        "##........##",
    };

    static readonly string[] OctopusArt2 =
    {
        "....####....",
        ".##########.",
        "############",
        "###..##..###",
        "############",
        "..###..###..",
        ".##..##..##.",
        "..##....##..",
    };

    static readonly string[] LaserArt = { "##", "##", "##", "##", "##", "##", "##", "##" };

    static readonly string[] MissileArt = { ".#.", "#..", ".#.", "..#", ".#.", "#..", ".#." };

    static readonly string[] CoinArt =
    {
        "..####..",
        ".#++++#.",
        "#++##++#",
        "#+#++#+#",
        "#+#++#+#",
        "#++##++#",
        ".#++++#.",
        "..####..",
    };

    static readonly string[] ExplosionArt =
    {
        "#...#...#",
        ".#..#..#.",
        "..#...#..",
        "##.....##",
        "..#...#..",
        ".#..#..#.",
        "#...#...#",
    };

    static readonly string[] BlockArt = { "####", "####", "####", "####" };

    static readonly string[] EliteArt1 =
    {
        "#..#....#..#",
        ".#..#..#..#.",
        "..########..",
        ".##.####.##.",
        "############",
        "#.########.#",
        "#.#......#.#",
        "..##....##..",
    };

    static readonly string[] EliteArt2 =
    {
        "#..#....#..#",
        ".#..#..#..#.",
        "#.########.#",
        "###.####.###",
        "############",
        ".##########.",
        "..#......#..",
        ".#........#.",
    };

    static readonly string[] HeartArt =
    {
        ".###...###.",
        "#####.#####",
        "###########",
        "###########",
        "###########",
        ".#########.",
        "..#######..",
        "...#####...",
        "....###....",
        ".....#.....",
    };

    static readonly string[] BossArt =
    {
        "....############....",
        "..################..",
        ".##################.",
        "###..####..####..###",
        "###..####..####..###",
        "####################",
        "####################",
        "..###.##.##.##.###..",
        "..##..##.##.##..##..",
        ".##...##.##.##...##.",
        "##...##..##..##...##",
        "#....#...##...#....#",
    };

    static readonly string[] ShieldIconArt =
    {
        "#..######..#",
        "############",
        "##++++++++##",
        "##++++++++##",
        "##++++++++##",
        ".##++++++##.",
        ".##++++++##.",
        "..##++++##..",
        "...##++##...",
        "....####....",
        ".....##.....",
    };

    static readonly string[] RapidFireIconArt =
    {
        "#...#..###.",
        ".#.#.....#.",
        "..#....##..",
        ".#.#..#....",
        "#...#.#####",
    };

    // Гладкий купол из мелких блоков (0.1 юнита): выбоины получаются аккуратнее
    static readonly string[] BunkerShape =
    {
        "...##############...",
        ".##################.",
        "####################",
        "####################",
        "####################",
        "####################",
        "####################",
        "######........######",
        "#####..........#####",
        "####............####",
        "###..............###",
    };

    // ---------- Цвета ----------

    static readonly Color PlayerColor = new Color(0.22f, 1f, 0.42f);
    static readonly Color TextColor = new Color(0.78f, 1f, 0.84f);
    static readonly Color SquidColor = new Color(1f, 0.4f, 0.9f);
    static readonly Color CrabColor = new Color(0.4f, 0.9f, 1f);
    static readonly Color OctopusColor = new Color(1f, 0.85f, 0.3f);
    static readonly Color MissileColor = new Color(1f, 0.35f, 0.25f);
    static readonly Color GoldColor = new Color(0.66f, 1f, 0.74f);
    static readonly Color DarkGoldColor = new Color(0.75f, 0.5f, 0.05f);
    static readonly Color BunkerColor = new Color(0.3f, 0.85f, 0.3f);
    static readonly Color ButtonColor = new Color(0.15f, 0.55f, 0.25f);

    // ---------- Созданные ассеты ----------

    static Sprite playerSprite, laserSprite, missileSprite, coinSprite, explosionSprite, blockSprite, blockTopSprite, blockGrooveSprite;
    static Sprite boss1Sprite, boss2Sprite, boss1AngrySprite, boss2AngrySprite, heartSprite;
    static Sprite canSprite, slotSprite, pauseSprite, buttonSprite, logoSprite;
    static Sprite btnOutlineSprite, btnFillSprite, btnGlowSprite, windowSprite;
    static Sprite shieldBubbleSprite, shieldIconSprite, bunkerIconSprite, rapidFireIconSprite, uiSquareSprite;
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
                "Будут созданы (или перезаписаны) спрайты, звуки, префабы, волны и сцены MainMenu и Game.\n\nПродолжить?",
                "Да", "Отмена"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildAll();

        EditorUtility.DisplayDialog("Space Invaders",
            "Готово!\n\nСцена MainMenu открыта - нажми Play.", "OK");
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

        var gameScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreatePrefabs();
        BuildGameScene();
        EditorSceneManager.SaveScene(gameScene, GameScenePath);

        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildMenuScene();
        EditorSceneManager.SaveScene(menuScene, MenuScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        SpaceInvadersBackground.ApplyIfMissing();
        SpaceInvadersCrt.Apply();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Space Invaders: build complete");
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
        playerSprite = MakeSprite("Player", PlayerArt, Palette(PlayerColor), 12);
        squidFrames = new[]
        {
            MakeSprite("Squid_1", SquidArt1, Palette(SquidColor), 12),
            MakeSprite("Squid_2", SquidArt2, Palette(SquidColor), 12),
        };
        crabFrames = new[]
        {
            MakeSprite("Crab_1", CrabArt1, Palette(CrabColor), 12),
            MakeSprite("Crab_2", CrabArt2, Palette(CrabColor), 12),
        };
        octopusFrames = new[]
        {
            MakeSprite("Octopus_1", OctopusArt1, Palette(OctopusColor), 12),
            MakeSprite("Octopus_2", OctopusArt2, Palette(OctopusColor), 12),
        };
        var eliteColor = new Color(1f, 0.3f, 0.3f);
        eliteFrames = new[]
        {
            MakeSprite("Elite_1", EliteArt1, Palette(eliteColor), 12),
            MakeSprite("Elite_2", EliteArt2, Palette(eliteColor), 12),
        };
        laserSprite = MakeSprite("Laser", LaserArt, Palette(Color.white), 16);
        missileSprite = MakeSprite("Missile", MissileArt, Palette(MissileColor), 14);
        coinSprite = MakeSprite("Coin", CoinArt, new Dictionary<char, Color> { { '#', DarkGoldColor }, { '+', GoldColor } }, 20);
        explosionSprite = MakeSprite("Explosion", ExplosionArt, Palette(new Color(1f, 0.8f, 0.5f)), 12);
        blockSprite = MakeSprite("BunkerBlock", BlockArt, Palette(BunkerColor), 40);
        blockTopSprite = MakeSprite("BunkerBlockTop", BlockArt, Palette(TextColor), 40);
        blockGrooveSprite = blockSprite;

        heartSprite = MakeSprite("Heart", HeartArt, Palette(new Color(1f, 0.25f, 0.35f)), 100);
        boss1Sprite = MakeSprite("Boss_1", BossArt, Palette(new Color(1f, 0.55f, 0.15f)), 16);
        boss2Sprite = MakeSprite("Boss_2", BossArt, Palette(new Color(0.75f, 0.35f, 1f)), 16);

        var shieldColors = new Dictionary<char, Color> { { '#', new Color(0.5f, 0.9f, 1f, 0.95f) }, { '+', new Color(0.3f, 0.7f, 1f, 0.3f) } };
        shieldBubbleSprite = MakeSprite("ShieldBubble", CircleArt(32), shieldColors, 20);
        shieldIconSprite = MakeSprite("Icon_Shield", ShieldIconArt, new Dictionary<char, Color> { { '#', new Color(0.5f, 0.9f, 1f) }, { '+', new Color(0.2f, 0.45f, 0.8f) } }, 100);
        bunkerIconSprite = MakeSprite("Icon_Bunker", BunkerShape, Palette(BunkerColor), 100);
        rapidFireIconSprite = MakeSprite("Icon_RapidFire", RapidFireIconArt, Palette(new Color(1f, 0.6f, 0.2f)), 100);
        uiSquareSprite = MakeSprite("UISquare", BlockArt, Palette(Color.white), 100);

        LoadGameArt();
    }

    // Графика из Assets/GameArt. PPU подобран так, чтобы размеры в игре остались прежними.
    // Если файла нет, остаётся сгенерированный спрайт
    static void LoadGameArt()
    {
        squidFrames = new[] { Art("squid_1", 384f, squidFrames[0]), Art("squid_2", 384f, squidFrames[1]) };
        crabFrames = new[] { Art("crab_1", 390f, crabFrames[0]), Art("crab_2", 390f, crabFrames[1]) };
        octopusFrames = new[] { Art("octopus_1", 358f, octopusFrames[0]), Art("octopus_2", 358f, octopusFrames[1]) };
        eliteFrames = new[] { Art("elite_1", 358f, eliteFrames[0]), Art("elite_2", 358f, eliteFrames[1]) };
        boss1Sprite = Art("boss_1", 768f, boss1Sprite);
        boss2Sprite = Art("boss_2", 816f, boss2Sprite);
        // Красные копии для приступов ярости (каналы переставлены, размер тот же). Нет файла - без красноты
        boss1AngrySprite = Art("boss_1_angry", 768f, null);
        boss2AngrySprite = Art("boss_2_angry", 816f, null);
        laserSprite = Art("laser", 600f, laserSprite);
        missileSprite = Art("missile", MissilePpu, missileSprite); // квадрат 128 px на холсте 136, в игре повёрнут ромбом
        coinSprite = Art("coin", 640f, coinSprite);
        explosionSprite = Art("explosion", 683f, explosionSprite);
        shieldBubbleSprite = Art("shield", 320f, shieldBubbleSprite);
        blockSprite = Art("block", 500f, blockSprite);              // 50x50 -> 0.1 юнита
        blockTopSprite = Art("block_top", 500f, blockTopSprite);
        blockGrooveSprite = Art("block_groove", 500f, blockGrooveSprite);
        heartSprite = Art("heart", 100f, heartSprite);
        shieldIconSprite = Art("icon_shield", 100f, shieldIconSprite);
        bunkerIconSprite = Art("icon_repair", 100f, bunkerIconSprite);
        rapidFireIconSprite = Art("icon_rapidfire", 100f, rapidFireIconSprite);
        canSprite = Art("heart_pickup", 550f, null); // хилка: сердечко размером с монетку
        slotSprite = Art("ability_slot", 100f, null);
        pauseSprite = Art("pause", 100f, null);
        buttonSprite = Art("button", 100f, null);
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
        if (canSprite == null)
            canSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NewSprites/monster_can.png");
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
        EditorUtility.SetDirty(waves[4].bossPrefab);
        EditorUtility.SetDirty(waves[2]);
        EditorUtility.SetDirty(waves[4]);
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

        var managers = new GameObject("GameManager");
        managers.AddComponent<GameManager>();
        var sound = managers.AddComponent<SoundManager>();
        managers.GetComponent<AudioSource>().playOnAwake = false;
        sound.shoot = shootClip;
        sound.explosion = explosionClip;
        sound.coin = coinClip;
        sound.playerHit = hitClip;

        // Спрайт из дизайна, если он есть: коллайдер только по видимой части корабля
        var heroSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameArt/hero.png");
        var player = NewSpriteObject("Player", heroSprite != null ? heroSprite : playerSprite, 0);
        player.transform.position = new Vector3(0f, -9f, 0f);
        player.transform.localScale = Vector3.one * BodyScale;
        AddTrigger(player, false);
        if (heroSprite != null)
            player.GetComponent<BoxCollider2D>().size = new Vector2(1.0824f, 0.6353f);
        player.AddComponent<PlayerController>().laserPrefab = laserPrefab;

        var gridObject = new GameObject("InvaderGrid");
        gridObject.transform.position = new Vector3(0f, 9.2f, 0f);
        var grid = gridObject.AddComponent<InvaderGrid>();
        grid.invaderPrefabs = invaderPrefabs;
        grid.eliteInvaderPrefab = eliteInvaderPrefab;
        grid.missilePrefab = missilePrefab;
        grid.player = player.GetComponent<PlayerController>();

        var shield = NewSpriteObject("Shield", shieldBubbleSprite, 5);
        shield.transform.SetParent(player.transform, false);
        player.GetComponent<PlayerController>().shieldVisual = shield;

        var waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();
        waveManager.waves = waves;
        waveManager.grid = grid;
        waveManager.healPrefab = healPrefab;

        var abilities = new GameObject("AbilityManager").AddComponent<AbilityManager>();
        abilities.player = player.GetComponent<PlayerController>();
        abilities.bunkers = BuildBunkers();
        abilities.abilities = new[]
        {
            new Ability { type = AbilityType.Shield, cost = 3, cooldown = 10f, duration = 1f },
            new Ability { type = AbilityType.RepairBunkers, cost = 5, cooldown = 20f, duration = 0f },
            new Ability { type = AbilityType.RapidFire, cost = 10, cooldown = 20f, duration = 5f },
        };

        BuildGameUI(waveManager, abilities);
        BuildBlinkOverlay(waveManager);
        MakeEventSystem();
    }

    static Transform BuildBunkers()
    {
        const float blockSize = 0.1f;
        float[] positionsX = { -4.5f, -1.5f, 1.5f, 4.5f };
        int width = BunkerShape[0].Length;
        int height = BunkerShape.Length;

        var root = new GameObject("Bunkers");
        for (int i = 0; i < positionsX.Length; i++)
        {
            var bunker = new GameObject($"Bunker {i + 1}");
            bunker.transform.SetParent(root.transform);
            bunker.transform.position = new Vector3(positionsX[i], -5.5f, 0f);

            var grid = new BunkerBlock[height, width];
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (BunkerShape[row][col] != '#') continue;

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

        // Жизни: сердечки слева
        ui.hearts = new Image[3];
        for (int i = 0; i < ui.hearts.Length; i++)
        {
            ui.hearts[i] = MakeChildImage(safeArea, $"Heart{i + 1}", heartSprite, Color.white);
            ui.hearts[i].preserveAspect = true;
            var heartRect = ui.hearts[i].rectTransform;
            heartRect.anchorMin = heartRect.anchorMax = heartRect.pivot = topLeft;
            heartRect.anchoredPosition = new Vector2(30f + i * 100f, -20f);
            heartRect.sizeDelta = new Vector2(90f, 90f);
        }

        // Монеты: иконка и число под сердечками
        var coinIcon = MakeChildImage(safeArea, "CoinIcon", coinSprite, Color.white);
        coinIcon.preserveAspect = true;
        var coinIconRect = coinIcon.rectTransform;
        coinIconRect.anchorMin = coinIconRect.anchorMax = coinIconRect.pivot = topLeft;
        coinIconRect.anchoredPosition = new Vector2(35f, -128f);
        coinIconRect.sizeDelta = new Vector2(64f, 64f);
        ui.coinsText = MakeText(safeArea, "Coins", "0", 48, TextAlignmentOptions.TopLeft, topLeft, new Vector2(115f, -134f), new Vector2(300f, 70f));
        ui.coinsText.color = TextColor;
        ui.bannerText = MakeText(canvas, "WaveBanner", "WAVE 1", 110, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(1200f, 200f));

        // Полоска здоровья босса
        var bossBar = new GameObject("BossBar", typeof(RectTransform), typeof(Image));
        bossBar.transform.SetParent(safeArea, false);
        var bossBarRect = (RectTransform)bossBar.transform;
        bossBarRect.anchorMin = bossBarRect.anchorMax = bossBarRect.pivot = top;
        bossBarRect.anchoredPosition = new Vector2(0f, -245f);
        bossBarRect.sizeDelta = new Vector2(1040f, 40f);
        var bossBarBack = bossBar.GetComponent<Image>();
        bossBarBack.color = new Color(0f, 0f, 0f, 0.7f);
        bossBarBack.raycastTarget = false;
        ui.bossBar = bossBar;
        ui.bossBarFill = MakeChildImage(bossBar.transform, "Fill", uiSquareSprite, PlayerColor);
        ui.bossBarFill.type = Image.Type.Filled;
        ui.bossBarFill.fillMethod = Image.FillMethod.Horizontal;
        var bossFillRect = ui.bossBarFill.rectTransform;
        bossFillRect.anchorMin = Vector2.zero;
        bossFillRect.anchorMax = Vector2.one;
        bossFillRect.offsetMin = bossFillRect.offsetMax = Vector2.zero;

        // Панель способностей: столбик у левого края, по центру экрана
        MakeAbilitySlot(safeArea, abilities, 0, shieldIconSprite, 220f);
        MakeAbilitySlot(safeArea, abilities, 1, bunkerIconSprite, 0f);
        MakeAbilitySlot(safeArea, abilities, 2, rapidFireIconSprite, -220f);

        // Круглая аркадная кнопка паузы в правом верхнем углу, подсвечивается при нажатии
        var pauseButton = MakeRoundButton(safeArea, "PauseButton", topRight, new Vector2(-95f, -85f), 130f);

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
    static void BuildBlinkOverlay(WaveManager waveManager)
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

    // ================= Сцена MainMenu =================

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
        var menu = canvas.gameObject.AddComponent<MainMenu>();
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
            var title = MakeText(canvas, "Title", "SPACE INVADERS", 110, TextAlignmentOptions.Center, center, new Vector2(0f, 250f), new Vector2(1200f, 200f));
            title.color = PlayerColor;
        }

        // Аркадная шапка: тусклая подпись HI-SCORE над ярким рекордом (монеты в меню не показываем)
        MakeText(canvas, "HighScoreLabel", "HI-SCORE", 34, TextAlignmentOptions.Center, center, new Vector2(0f, 105f), new Vector2(600f, 50f))
            .color = PhosphorMid;
        menu.highScoreText = MakeText(canvas, "HighScore", "00000", 64, TextAlignmentOptions.Center, center, new Vector2(0f, 40f), new Vector2(600f, 80f));

        // Аркадные кнопки как на экранах конца игры: PLAY главная (залитая)
        menu.playButton = MakeArcadeButton(canvas, "PlayButton", "PLAY", true, center, new Vector2(0f, -170f), new Vector2(640f, 150f));
        menu.quitButton = MakeArcadeButton(canvas, "QuitButton", "QUIT", false, center, new Vector2(0f, -350f), new Vector2(640f, 150f));

        var controls = MakeText(canvas, "Controls", "Drag your finger to move\nShooting is automatic",
            44, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1200f, 160f));
        controls.color = new Color(0.13f, 0.76f, 0.29f);

        menu.quitEffect = BuildQuitOverlay();
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

        string[] texts = { "SPACE INVADERS SYSTEM", "ROM CHECK ........ OK", "RAM CHECK ........ OK", "PHOSPHOR WARMUP ... OK", "TAP TO START" };
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
        slot.background = go.GetComponent<Image>();
        slot.background.sprite = slotSprite;
        slot.button = go.GetComponent<Button>();
        slot.button.targetGraphic = slot.background;
        slot.button.transition = Selectable.Transition.None;

        slot.icon = MakeChildImage(go.transform, "Icon", iconSprite, Color.white);
        slot.icon.preserveAspect = true;
        var iconRect = slot.icon.rectTransform;
        iconRect.anchoredPosition = new Vector2(0f, 20f);
        iconRect.sizeDelta = new Vector2(130f, 130f);

        // Затемнение перезарядки уходит по кругу
        slot.cooldownOverlay = MakeChildImage(go.transform, "Cooldown", uiSquareSprite, new Color(0f, 0f, 0f, 0.7f));
        slot.cooldownOverlay.type = Image.Type.Filled;
        slot.cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        slot.cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
        slot.cooldownOverlay.fillClockwise = false;
        slot.cooldownOverlay.fillAmount = 0f;
        var overlayRect = slot.cooldownOverlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        var center = new Vector2(0.5f, 0.5f);
        slot.cooldownText = MakeText(go.transform, "CooldownText", "", 90, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(200f, 120f));
        slot.costText = MakeText(go.transform, "Cost", "", 44, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(200f, 56f));
    }

    // ================= Экраны конца игры =================

    static readonly Color ScreenDark = new Color(0.024f, 0.082f, 0.043f);   // #06150B
    static readonly Color PhosphorMid = new Color(0.28f, 0.83f, 0.42f);     // #48D46C
    static readonly Color PhosphorLine = new Color(0.12f, 0.54f, 0.24f);    // #1F8A3E
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
    static PressableButton MakeRoundButton(Transform parent, string name, Vector2 anchor, Vector2 position, float size)
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

        var iconSprite = Art("icon_pause", 100f, null);
        if (iconSprite != null)
        {
            var icon = MakeChildImage(button.transform, "Icon", iconSprite, button.normalLabelColor);
            icon.rectTransform.sizeDelta = new Vector2(size * 0.32f, size * 0.36f);
            button.icon = icon;
        }
        else
        {
            button.label.text = "II";
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

        // Шапка автомата: 1UP и HI
        screen.oneUpLabel = TopText(area, "1UP", "1UP", 40, -380f, -60f, 200f, mid, PhosphorMid);
        screen.topScoreText = TopText(area, "TopScore", "00000", 40, -160f, -60f, 260f, mid, TextColor);
        screen.hiLabel = TopText(area, "HI", "HI", 40, 150f, -60f, 160f, mid, PhosphorMid);
        screen.topBestText = TopText(area, "TopBest", "00000", 40, 360f, -60f, 260f, mid, TextColor);

        screen.titleText = TopText(area, "Title", "CONGRATULATIONS!", 96, 0f, -220f, 1240f, mid, TextColor);
        AddBlink(screen.titleText);
        screen.subtitleText = TopText(area, "Subtitle", "EARTH IS SAVED\nALL 5 WAVES CLEARED", 44, 0f, -400f, 1200f, mid, PhosphorMid);
        screen.subtitleText.rectTransform.sizeDelta = new Vector2(1200f, 140f);
        TopLine(area, -580f);

        // Подсчёт очков: подпись слева, число справа
        screen.scoreLabel = TopText(area, "ScoreLabel", "SCORE", 48, 0f, -630f, 1080f, left, TextColor);
        screen.scoreValue = TopText(area, "ScoreValue", "", 48, 0f, -630f, 1080f, right, TextColor);
        screen.livesLabel = TopText(area, "LivesLabel", "LIVES", 48, 0f, -720f, 1080f, left, TextColor);
        screen.livesValue = TopText(area, "LivesValue", "", 48, 0f, -720f, 1080f, right, TextColor);
        screen.coinsLabel = TopText(area, "CoinsLabel", "COINS", 48, 0f, -810f, 1080f, left, TextColor);
        screen.coinsValue = TopText(area, "CoinsValue", "", 48, 0f, -810f, 1080f, right, TextColor);
        screen.totalLabel = TopText(area, "TotalLabel", "TOTAL", 62, 0f, -915f, 1080f, left, TextColor);
        screen.totalValue = TopText(area, "TotalValue", "", 62, 0f, -915f, 1080f, right, TextColor);

        screen.newHighScore = TopText(area, "NewHighScore", "NEW HIGH SCORE!", 54, 0f, -1030f, 1200f, mid, HighScoreYellow);
        AddBlink(screen.newHighScore);
        TopLine(area, -1130f);

        // Таблица TOP 5
        var table = new GameObject("Table", typeof(RectTransform));
        table.transform.SetParent(area, false);
        Stretch((RectTransform)table.transform);
        screen.tableRoot = table;
        screen.tableTitle = TopText(table.transform, "TableTitle", "- TOP 5 -", 40, 0f, -1165f, 600f, mid, PhosphorMid);

        string[] ranks = { "1ST", "2ND", "3RD", "4TH", "5TH" };
        screen.rankTexts = new TMP_Text[ranks.Length];
        screen.nameTexts = new TMP_Text[ranks.Length];
        screen.scoreTexts = new TMP_Text[ranks.Length];
        screen.rowHighlights = new Image[ranks.Length];
        for (int i = 0; i < ranks.Length; i++)
        {
            float y = -1240f - i * 90f;
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
        screen.initialsHint = TopText(initials.transform, "Hint", "ENTER YOUR NAME", 36, 0f, -1700f, 800f, mid, PhosphorMid);
        screen.initialsInput = MakeInputField(initials.transform, "NameInput", new Vector2(-130f, -1830f), new Vector2(620f, 130f));
        screen.initialsOkButton = MakeArcadeButton(initials.transform, "NameOk", "OK", false,
            new Vector2(0.5f, 1f), new Vector2(330f, -1830f), new Vector2(220f, 130f));

        // Кнопки внизу экрана
        var bottom = new Vector2(0.5f, 0f);
        screen.menuButton = MakeArcadeButton(area, "MenuButton", "MENU", false, bottom, new Vector2(-290f, 260f), new Vector2(540f, 150f));
        screen.restartButton = MakeArcadeButton(area, "RestartButton", "RESTART", true, bottom, new Vector2(290f, 260f), new Vector2(540f, 150f));

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

    static Button MakeButton(Transform parent, string name, string label, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(640f, 140f);

        var image = go.GetComponent<Image>();
        image.sprite = buttonSprite;
        image.color = buttonSprite != null ? Color.white : ButtonColor;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        MakeText(go.transform, "Label", label, 60, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta);
        return button;
    }
}
