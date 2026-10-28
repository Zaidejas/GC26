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
// Рисует спрайты, генерирует звуки, создаёт префабы, 3 волны, сцены MainMenu и Game
// и добавляет сцены в Build Settings. Всё созданное потом можно менять руками в редакторе.
public static class SpaceInvadersBuilder
{
    const string SpritesDir = "Assets/Sprites";
    const string AudioDir = "Assets/Audio";
    const string PrefabsDir = "Assets/Prefabs";
    const string WavesDir = "Assets/Waves";
    const string ScenesDir = "Assets/Scenes";
    const string MenuScenePath = ScenesDir + "/MainMenu.unity";
    const string GameScenePath = ScenesDir + "/Game.unity";

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

    static readonly string[] BunkerShape =
    {
        "..######..",
        ".########.",
        "##########",
        "##########",
        "###....###",
        "##......##",
    };

    // ---------- Цвета ----------

    static readonly Color PlayerColor = new Color(0.3f, 1f, 0.4f);
    static readonly Color SquidColor = new Color(1f, 0.4f, 0.9f);
    static readonly Color CrabColor = new Color(0.4f, 0.9f, 1f);
    static readonly Color OctopusColor = new Color(1f, 0.85f, 0.3f);
    static readonly Color MissileColor = new Color(1f, 0.35f, 0.25f);
    static readonly Color GoldColor = new Color(1f, 0.85f, 0.2f);
    static readonly Color DarkGoldColor = new Color(0.75f, 0.5f, 0.05f);
    static readonly Color BunkerColor = new Color(0.3f, 0.85f, 0.3f);
    static readonly Color ButtonColor = new Color(0.15f, 0.55f, 0.25f);

    // ---------- Созданные ассеты ----------

    static Sprite playerSprite, laserSprite, missileSprite, coinSprite, explosionSprite, blockSprite;
    static Sprite[] squidFrames, crabFrames, octopusFrames;
    static AudioClip shootClip, explosionClip, coinClip, hitClip;
    static Projectile laserPrefab, missilePrefab;
    static Coin coinPrefab;
    static GameObject explosionPrefab;
    static Invader[] invaderPrefabs;
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
        laserSprite = MakeSprite("Laser", LaserArt, Palette(Color.white), 16);
        missileSprite = MakeSprite("Missile", MissileArt, Palette(MissileColor), 14);
        coinSprite = MakeSprite("Coin", CoinArt, new Dictionary<char, Color> { { '#', DarkGoldColor }, { '+', GoldColor } }, 20);
        explosionSprite = MakeSprite("Explosion", ExplosionArt, Palette(new Color(1f, 0.8f, 0.5f)), 12);
        blockSprite = MakeSprite("BunkerBlock", BlockArt, Palette(BunkerColor), 20);
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
            MakeWave("Wave1", 4, 8, 1.0f, 1.4f, 0.20f),
            MakeWave("Wave2", 5, 9, 1.4f, 1.0f, 0.25f),
            MakeWave("Wave3", 5, 11, 1.8f, 0.7f, 0.30f),
        };
    }

    static WaveData MakeWave(string name, int rows, int columns, float speed, float missileInterval, float coinChance)
    {
        var wave = ScriptableObject.CreateInstance<WaveData>();
        wave.rows = rows;
        wave.columns = columns;
        wave.speed = speed;
        wave.missileInterval = missileInterval;
        wave.coinDropChance = coinChance;

        string path = $"{WavesDir}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(wave, path);
        return wave;
    }

    static void CreatePrefabs()
    {
        // Пуля игрока: летит вверх, бункеры не ломает
        var laser = NewSpriteObject("Laser", laserSprite, 1);
        AddTrigger(laser, true);
        var laserProjectile = laser.AddComponent<Projectile>();
        laserProjectile.direction = Vector2.up;
        laserProjectile.speed = 12f;
        laserProjectile.fromPlayer = true;
        laserProjectile.damagesBunkers = false;
        laserPrefab = SavePrefab(laser).GetComponent<Projectile>();

        // Ракета пришельца: летит вниз, ломает бункеры
        var missile = NewSpriteObject("Missile", missileSprite, 1);
        AddTrigger(missile, true);
        var missileProjectile = missile.AddComponent<Projectile>();
        missileProjectile.direction = Vector2.down;
        missileProjectile.speed = 6f;
        missileProjectile.fromPlayer = false;
        missileProjectile.damagesBunkers = true;
        missilePrefab = SavePrefab(missile).GetComponent<Projectile>();

        var coin = NewSpriteObject("Coin", coinSprite, 2);
        AddTrigger(coin, true);
        coin.AddComponent<Coin>();
        coinPrefab = SavePrefab(coin).GetComponent<Coin>();

        var explosion = NewSpriteObject("Explosion", explosionSprite, 3);
        explosion.AddComponent<Explosion>();
        explosionPrefab = SavePrefab(explosion);

        invaderPrefabs = new[]
        {
            MakeInvaderPrefab("Invader_Squid", squidFrames, 30),
            MakeInvaderPrefab("Invader_Crab", crabFrames, 20),
            MakeInvaderPrefab("Invader_Octopus", octopusFrames, 10),
        };
    }

    static Invader MakeInvaderPrefab(string name, Sprite[] frames, int points)
    {
        var go = NewSpriteObject(name, frames[0], 0);
        AddTrigger(go, false);
        var invader = go.AddComponent<Invader>();
        invader.points = points;
        invader.frames = frames;
        invader.explosionPrefab = explosionPrefab;
        invader.coinPrefab = coinPrefab;
        return SavePrefab(go).GetComponent<Invader>();
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

        var player = NewSpriteObject("Player", playerSprite, 0);
        player.transform.position = new Vector3(0f, -5f, 0f);
        AddTrigger(player, false);
        player.AddComponent<PlayerController>().laserPrefab = laserPrefab;

        var gridObject = new GameObject("InvaderGrid");
        gridObject.transform.position = new Vector3(0f, 3.8f, 0f);
        var grid = gridObject.AddComponent<InvaderGrid>();
        grid.invaderPrefabs = invaderPrefabs;
        grid.missilePrefab = missilePrefab;

        var waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();
        waveManager.waves = waves;
        waveManager.grid = grid;

        BuildBunkers();
        BuildGameUI(waveManager);
        MakeEventSystem();
    }

    static void BuildBunkers()
    {
        const float blockSize = 0.2f;
        float[] positionsX = { -6f, -2f, 2f, 6f };
        int width = BunkerShape[0].Length;

        var root = new GameObject("Bunkers");
        for (int i = 0; i < positionsX.Length; i++)
        {
            var bunker = new GameObject($"Bunker {i + 1}");
            bunker.transform.SetParent(root.transform);
            bunker.transform.position = new Vector3(positionsX[i], -2.6f, 0f);

            for (int row = 0; row < BunkerShape.Length; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (BunkerShape[row][col] != '#') continue;

                    var block = NewSpriteObject("Block", blockSprite, 0);
                    block.transform.SetParent(bunker.transform, false);
                    block.transform.localPosition = new Vector3((col - (width - 1) / 2f) * blockSize, -row * blockSize, 0f);
                    AddTrigger(block, false);
                    block.AddComponent<BunkerBlock>();
                }
            }
        }
    }

    static void BuildGameUI(WaveManager waveManager)
    {
        Transform canvas = MakeCanvas().transform;
        var ui = canvas.gameObject.AddComponent<UIManager>();
        ui.waveManager = waveManager;

        var topLeft = new Vector2(0f, 1f);
        var top = new Vector2(0.5f, 1f);
        var topRight = new Vector2(1f, 1f);
        var center = new Vector2(0.5f, 0.5f);

        ui.scoreText = MakeText(canvas, "Score", "SCORE 0", 40, TextAlignmentOptions.TopLeft, topLeft, new Vector2(30f, -20f), new Vector2(500f, 60f));
        ui.livesText = MakeText(canvas, "Lives", "LIVES 3", 40, TextAlignmentOptions.TopLeft, topLeft, new Vector2(30f, -70f), new Vector2(500f, 60f));
        ui.livesText.color = PlayerColor;
        ui.waveText = MakeText(canvas, "Wave", "WAVE 1/3", 40, TextAlignmentOptions.Top, top, new Vector2(0f, -20f), new Vector2(500f, 60f));
        ui.coinsText = MakeText(canvas, "Coins", "COINS 0", 40, TextAlignmentOptions.TopRight, topRight, new Vector2(-30f, -20f), new Vector2(500f, 60f));
        ui.coinsText.color = GoldColor;
        ui.bannerText = MakeText(canvas, "WaveBanner", "WAVE 1", 110, TextAlignmentOptions.Center, center, Vector2.zero, new Vector2(1400f, 200f));

        // Экран конца игры
        var endPanel = MakePanel(canvas, "EndPanel", new Color(0f, 0f, 0f, 0.8f));
        ui.endPanel = endPanel;
        ui.endTitleText = MakeText(endPanel.transform, "Title", "GAME OVER", 120, TextAlignmentOptions.Center, center, new Vector2(0f, 230f), new Vector2(1400f, 170f));
        ui.endStatsText = MakeText(endPanel.transform, "Stats", "", 44, TextAlignmentOptions.Center, center, new Vector2(0f, 40f), new Vector2(1200f, 220f));
        ui.restartButton = MakeButton(endPanel.transform, "RestartButton", "PLAY AGAIN", new Vector2(0f, -150f));
        ui.menuButton = MakeButton(endPanel.transform, "MenuButton", "MAIN MENU", new Vector2(0f, -250f));
        endPanel.SetActive(false);

        // Пауза
        var pause = canvas.gameObject.AddComponent<PauseMenu>();
        var pausePanel = MakePanel(canvas, "PausePanel", new Color(0f, 0f, 0f, 0.7f));
        pause.panel = pausePanel;
        MakeText(pausePanel.transform, "Title", "PAUSED", 120, TextAlignmentOptions.Center, center, new Vector2(0f, 200f), new Vector2(1000f, 170f));
        pause.resumeButton = MakeButton(pausePanel.transform, "ResumeButton", "RESUME", new Vector2(0f, 30f));
        pause.restartButton = MakeButton(pausePanel.transform, "RestartButton", "RESTART", new Vector2(0f, -70f));
        pause.menuButton = MakeButton(pausePanel.transform, "MenuButton", "MAIN MENU", new Vector2(0f, -170f));
        pausePanel.SetActive(false);
    }

    // ================= Сцена MainMenu =================

    static void BuildMenuScene()
    {
        MakeCamera();

        // Декоративные пришельцы над заголовком
        Sprite[] decor = { squidFrames[0], crabFrames[0], octopusFrames[0] };
        for (int i = 0; i < decor.Length; i++)
        {
            var go = NewSpriteObject("Decor " + decor[i].name, decor[i], 0);
            go.transform.position = new Vector3((i - 1) * 2.5f, 4.2f, 0f);
            go.transform.localScale = Vector3.one * 1.5f;
        }

        Transform canvas = MakeCanvas().transform;
        var menu = canvas.gameObject.AddComponent<MainMenu>();
        var center = new Vector2(0.5f, 0.5f);

        var title = MakeText(canvas, "Title", "SPACE INVADERS", 130, TextAlignmentOptions.Center, center, new Vector2(0f, 200f), new Vector2(1700f, 200f));
        title.color = PlayerColor;

        menu.highScoreText = MakeText(canvas, "HighScore", "BEST 0", 48, TextAlignmentOptions.Center, center, new Vector2(0f, 70f), new Vector2(1000f, 70f));
        menu.coinsText = MakeText(canvas, "Coins", "COINS 0", 48, TextAlignmentOptions.Center, center, new Vector2(0f, 10f), new Vector2(1000f, 70f));
        menu.coinsText.color = GoldColor;

        menu.playButton = MakeButton(canvas, "PlayButton", "PLAY", new Vector2(0f, -120f));
        menu.quitButton = MakeButton(canvas, "QuitButton", "QUIT", new Vector2(0f, -220f));

        var controls = MakeText(canvas, "Controls", "A / D  or  ARROWS - move        ESC - pause        shooting is automatic",
            30, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1700f, 60f));
        controls.color = new Color(0.7f, 0.7f, 0.7f);

        MakeEventSystem();
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
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.07f);
        go.AddComponent<AudioListener>();
    }

    static Canvas MakeCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
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
        label.color = Color.white;

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

    static Button MakeButton(Transform parent, string name, string label, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(380f, 80f);

        var image = go.GetComponent<Image>();
        image.color = ButtonColor;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        MakeText(go.transform, "Label", label, 38, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta);
        return button;
    }
}
