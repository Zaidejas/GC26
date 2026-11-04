using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ставит Assets/NewSprites/background.png фоном в сцены Game и MainMenu.
// Срабатывает сам после компиляции, если фона в сцене ещё нет (в том числе после Build Game).
[InitializeOnLoad]
public static class SpaceInvadersBackground
{
    const string SpritePath = "Assets/NewSprites/background.png";
    const string ObjectName = "Background";
    const float PixelsPerUnit = 90f; // 1080 px на 12 юнитов высоты камеры
    const int SortingOrder = -100;

    static readonly string[] ScenePaths = { "Assets/Scenes/Game.unity", "Assets/Scenes/MainMenu.unity" };

    static SpaceInvadersBackground()
    {
        EditorApplication.delayCall += ApplyIfMissing;
    }

    [MenuItem("Tools/Space Invaders/Apply Background")]
    static void ApplyIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SpritePath)) return;

        foreach (string scenePath in ScenePaths)
        {
            if (File.Exists(scenePath) && !File.ReadAllText(scenePath).Contains("m_Name: " + ObjectName))
                AddToScene(scenePath, LoadSprite());
        }
    }

    static Sprite LoadSprite()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static void AddToScene(string scenePath, Sprite sprite)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasOpen = scene.isLoaded;
        if (!wasOpen)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        var background = new GameObject(ObjectName);
        SceneManager.MoveGameObjectToScene(background, scene);
        background.transform.position = new Vector3(0f, 0f, 10f);
        background.transform.SetAsFirstSibling();

        var renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = SortingOrder;
        background.AddComponent<ScreenBackground>();

        EditorSceneManager.SaveScene(scene);
        if (!wasOpen)
            EditorSceneManager.CloseScene(scene, true);

        Debug.Log("Space Invaders: background added to " + scenePath);
    }
}
