using Lines.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lines.EditorTools
{
    /// <summary>
    /// Creates the Main scene and applies project settings from code.
    /// Run from the command line:
    ///   Unity -batchmode -projectPath . -executeMethod Lines.EditorTools.ProjectSetup.Run -quit
    /// Safe to run again: it rebuilds the Main scene from scratch.
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        static readonly Color Background = new Color(0.96f, 0.95f, 0.92f);

        [MenuItem("Lines/Run Project Setup")]
        public static void Run()
        {
            ApplySettings();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Lines: project setup finished");
        }

        public const string BundleId = "com.gc26.lines";

        static void ApplySettings()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

            PlayerSettings.companyName = "GC26";
            PlayerSettings.productName = "Lines";
            // A unique app id, needed to install on a real iPhone with a free Apple account (and on Android).
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, BundleId);
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
