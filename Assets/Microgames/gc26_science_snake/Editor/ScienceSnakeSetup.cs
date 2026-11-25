using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26.ScienceSnake.Editor
{
    /// <summary>
    /// Creates the game scene once (Scenes/gc26_science_snake_Main.unity) and adds menu items:
    /// Science Snake → Open Scene / Build Windows / Build Android. Builds pass the scene explicitly,
    /// so the shared Build Settings stay untouched.
    /// </summary>
    [InitializeOnLoad]
    public static class ScienceSnakeSetup
    {
        public const string ScenePath = "Assets/Microgames/gc26_science_snake/Scenes/gc26_science_snake_Main.unity";

        const string AndroidRequest = "Temp/ScienceSnake.build-android";

        static ScienceSnakeSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath)) CreateScene(true);
                else EnsureCamera();
                if (File.Exists(AndroidRequest)) { File.Delete(AndroidRequest); BuildAndroid(); }
            };
        }

        static void AddCamera(GameObject parent)
        {
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(parent.transform, false);
            cam.transform.localPosition = new Vector3(0, 0, -10);
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(19, 36, 26, 255);
            cam.gameObject.tag = "MainCamera";
        }

        /// <summary>Older scenes had no camera, so the Game view said "No cameras rendering" outside Play mode.</summary>
        static void EnsureCamera()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var game = Object.FindFirstObjectByType<ScienceSnakeGame>();
            if (game == null || game.GetComponentInChildren<Camera>() != null) return;
            AddCamera(game.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[gc26_science_snake] Added a camera to the scene.");
        }

        /// <summary>Builds the scene: one GameObject with ScienceSnakeGame and a camera. Everything else is created at runtime.</summary>
        public static void CreateScene(bool askToSaveOpenScenes)
        {
            if (askToSaveOpenScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Science Snake");
            go.AddComponent<ScienceSnakeGame>();
            AddCamera(go);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[gc26_science_snake] Scene created: " + ScenePath + " — press Play.");
        }

        /// <summary>For batch mode: -executeMethod GC26.ScienceSnake.Editor.ScienceSnakeSetup.CreateSceneBatch</summary>
        public static void CreateSceneBatch() => CreateScene(false);

        [MenuItem("Science Snake/Open Scene", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { CreateScene(true); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Science Snake/Build Windows", priority = 20)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/ScienceSnake/Windows/ScienceSnake.exe");

        [MenuItem("Science Snake/Build Android (APK)", priority = 21)]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/ScienceSnake/Android/ScienceSnake.apk");

        /// <summary>For batch mode: -executeMethod GC26.ScienceSnake.Editor.ScienceSnakeSetup.BuildWindowsBatch</summary>
        public static void BuildWindowsBatch()
        {
            if (!File.Exists(ScenePath)) CreateScene(false);
            var ok = Build(BuildTarget.StandaloneWindows64, "Builds/ScienceSnake/Windows/ScienceSnake.exe");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>For batch mode (start Unity with -buildTarget Android): -executeMethod GC26.ScienceSnake.Editor.ScienceSnakeSetup.BuildAndroidBatch</summary>
        public static void BuildAndroidBatch()
        {
            CreateScene(false);   // re-creates the scene, so it always has the camera
            var ok = Build(BuildTarget.Android, "Builds/ScienceSnake/Android/ScienceSnake.apk");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(BuildTarget target, string path)
        {
            if (!File.Exists(ScenePath)) CreateScene(true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log($"[gc26_science_snake] Build {target}: {report.summary.result} → {path}");
            if (ok && !Application.isBatchMode) EditorUtility.RevealInFinder(path);
            return ok;
        }
    }
}
