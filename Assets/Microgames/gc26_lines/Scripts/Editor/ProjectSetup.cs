using GC26.Lines.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26.Lines.EditorTools
{
    /// <summary>
    /// Creates the game's only scene, Scenes/gc26_lines_Main.unity, from code: a camera, GameBootstrap (which
    /// builds everything else at runtime) and LinesMicrogame (the link to the shared GC26 menu).
    /// It changes nothing outside the game folder: project settings and Build Settings belong to the
    /// organiser (RULES.md); the phone-app settings are applied only while a build runs (PhoneAppSettings).
    /// Run from the command line:
    ///   Unity -batchmode -projectPath . -executeMethod GC26.Lines.EditorTools.ProjectSetup.Run -quit
    /// Safe to run again: it rebuilds the scene from scratch.
    /// </summary>
    public static class ProjectSetup
    {
        public const string GameFolder = "Assets/Microgames/gc26_lines";
        public const string MainScenePath = GameFolder + "/Scenes/" + LinesMicrogame.Id + "_Main.unity";

        [MenuItem("Lines/Run Project Setup")]
        public static void Run()
        {
            CreateMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Lines: project setup finished");
        }

        static void CreateMainScene()
        {
            System.IO.Directory.CreateDirectory(GameFolder + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = View.Palette.Screen;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var game = new GameObject("GameBootstrap");
            game.AddComponent<GameBootstrap>();
            game.AddComponent<LinesMicrogame>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        // TextMeshPro's essential files (Assets/TextMesh Pro, from main) are imported once with
        // Unity's -importPackage command-line option, because AssetDatabase.ImportPackage
        // does not finish before a batch-mode run quits.
    }
}
