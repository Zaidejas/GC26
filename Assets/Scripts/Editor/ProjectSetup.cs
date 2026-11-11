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
            ApplyIconAndSplash();
        }

        public const string IconFolder = ArtImportSettings.AppIconFolder;
        const string SplashBackground = "Assets/Resources/Backgrounds/forest_plain.png";

        /// <summary>
        /// The app icon (one glowing mushroom on the dark forest) and the splash screen (the same mushroom and
        /// the "Lines" title over the forest, no Unity logo). Android 8+ gets the icon as two layers, so the
        /// phone can cut it into its own shape.
        /// </summary>
        static void ApplyIconAndSplash()
        {
            var icon = Load<Texture2D>("icon.png");
            if (icon == null) { Debug.LogWarning("Lines: app icon not found in " + IconFolder); return; }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // Android's adaptive icon kind lives in the Android module; it is looked up by name so this
            // still compiles on a Unity without that module.
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            var foreground = Load<Texture2D>("icon_foreground.png");
            var background = Load<Texture2D>("icon_background.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                bool adaptive = kind.ToString().Contains("Adaptive");
                foreach (var platformIcon in icons)
                {
                    if (adaptive) platformIcon.SetTextures(background, foreground);
                    else platformIcon.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }

            var screen = Lines.View.Palette.Screen;
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = screen;
            PlayerSettings.SplashScreen.background = AssetDatabase.LoadAssetAtPath<Sprite>(SplashBackground);
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(2f, AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "splash_logo.png")),
            };
            // The screen iOS shows before Unity starts: a plain colour, the same dark as the game.
            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.Default);
        }

        static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>(IconFolder + file);

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
