using Lines98.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lines98.EditorTools
{
    /// <summary>
    /// Creates the Main scene and applies project settings from code.
    /// Run from the command line:
    ///   Unity -batchmode -projectPath . -executeMethod Lines98.EditorTools.ProjectSetup.Run -quit
    /// Safe to run again: it rebuilds the Main scene from scratch.
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        static readonly Color Background = new Color(0.96f, 0.95f, 0.92f);

        [MenuItem("Lines98/Run Project Setup")]
        public static void Run()
        {
            ApplySettings();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Lines98: project setup finished");
        }

        static void ApplySettings()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

            PlayerSettings.companyName = "GC26";
            PlayerSettings.productName = "Lines 98";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        static void CreateMainScene()
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
        }

        // TextMeshPro's essential files (Assets/TextMesh Pro) are imported once with
        // Unity's -importPackage command-line option, because AssetDatabase.ImportPackage
        // does not finish before a batch-mode run quits.
    }
}
