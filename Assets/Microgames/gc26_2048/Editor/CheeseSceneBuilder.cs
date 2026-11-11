using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26._2048.EditorTools
{
    /// <summary>
    /// Builds Scenes/gc26_2048_Main.unity: one root object with Game2048 (the UI is made from code at runtime)
    /// and imports the item art as sprites. Menu: GC26 → gc26_2048 → Build Main Scene.
    /// Batch mode: -executeMethod GC26._2048.EditorTools.CheeseSceneBuilder.Build
    /// </summary>
    public static class CheeseSceneBuilder
    {
        const string Folder = "Assets/Microgames/gc26_2048";
        const string ScenePath = Folder + "/Scenes/gc26_2048_Main.unity";

        [MenuItem("GC26/gc26_2048/Build Main Scene")]
        public static void Build()
        {
            ImportArt();
            Directory.CreateDirectory(Folder + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("gc26_2048");
            root.AddComponent<Game2048>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[gc26_2048] scene saved: {ScenePath}");
        }

        /// <summary>
        /// Builds a Windows player with only gc26_2048_Main for a quick look, without touching Build Settings.
        /// Batch mode: -executeMethod GC26._2048.EditorTools.CheeseSceneBuilder.BuildPreviewPlayer -previewOut &lt;folder&gt;
        /// </summary>
        public static void BuildPreviewPlayer()
        {
            string output = "Temp/gc26_2048_preview";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-previewOut") output = args[i + 1];
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(output, "gc26_2048.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log($"[gc26_2048] preview build: {report.summary.result} -> {output}");
        }

        static void ImportArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder + "/Resources/gc26_2048" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
    }
}
