using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26.Minesweeper.EditorTools
{
    // GC26 → Minesweeper menu:
    //   Create Main Scene     – makes Scenes/gc26_minesweeper_Main.unity (camera + MinesweeperGame)
    //   Build Windows Player  – a standalone Red Flags build in Builds/RedFlags_Windows/ (not committed)
    // Neither changes Build Settings or ProjectSettings (class rules): the build lists its scene itself,
    // and the player settings it needs are put back afterwards.
    public static class MinesweeperBuildTools
    {
        const string ScenePath = "Assets/Microgames/" + MinesweeperGame.Id + "/Scenes/" + MinesweeperGame.Id + "_Main.unity";
        const string BuildPath = "Builds/RedFlags_Windows/RedFlags.exe";

        [MenuItem("GC26/Minesweeper/Create Main Scene")]
        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Only needed when the scene runs on its own; it just clears the screen behind the UI.
            var camGo = new GameObject("MinesweeperCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x12, 0x15, 0x1C, 0xFF);
            cam.orthographic = true;
            cam.cullingMask = 0;

            new GameObject("MinesweeperGame").AddComponent<MinesweeperGame>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[{MinesweeperGame.Id}] created {ScenePath}");
        }

        [MenuItem("GC26/Minesweeper/Build Windows Player")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateMainScene();

            // Remember the shared settings, change them for this build only, then put them back.
            string productName = PlayerSettings.productName;
            FullScreenMode mode = PlayerSettings.fullScreenMode;
            int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
            bool resizable = PlayerSettings.resizableWindow;
            BuildReport report;
            try
            {
                PlayerSettings.productName = "Red Flags";
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 540;
                PlayerSettings.defaultScreenHeight = 960;
                PlayerSettings.resizableWindow = true;

                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = BuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                PlayerSettings.productName = productName;
                PlayerSettings.fullScreenMode = mode;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.resizableWindow = resizable;
            }

            Debug.Log($"[{MinesweeperGame.Id}] build {report.summary.result}: {BuildPath}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} error(s)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
