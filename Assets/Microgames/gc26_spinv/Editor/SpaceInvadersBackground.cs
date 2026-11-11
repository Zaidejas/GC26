using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GC26.Spinv
{
    // Ставит фон (Sprites/background_phosphor.png) в сцену игры: он общий для меню и партии.
    // Срабатывает сам после компиляции, если фона в сцене ещё нет (в том числе после Build Game).
    [InitializeOnLoad]
    public static class SpaceInvadersBackground
    {
        const string SpritePath = SpaceInvadersBuilder.RootDir + "/Sprites/background_phosphor.png";
        const string ObjectName = "Background";
        const float PixelsPerUnit = 41.54f; // 1080 px на 26 юнитов высоты камеры
        const int SortingOrder = -100;

        static readonly string[] ScenePaths = { SpaceInvadersBuilder.MainScenePath };

        static SpaceInvadersBackground()
        {
            EditorApplication.delayCall += ApplyIfMissing;
        }

        [MenuItem("Tools/Space Invaders/Apply Background")]
        public static void ApplyIfMissing()
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
            // Анимированные звёзды «в полёте» вместо статичной картинки
            background.AddComponent<StarfieldBackground>();

            EditorSceneManager.SaveScene(scene);
            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);

            Debug.Log("Space Invaders: background added to " + scenePath);
        }
    }
}
