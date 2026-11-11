using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GC26.Spinv
{
    // Сборка APK отдельной игрой: Tools → Space Invaders → Build Android APK. Файл появляется в папке Builds.
    // Настройки игрока ставятся только на время сборки в копии проекта (в репозиторий их не коммитим)
    public static class SpaceInvadersAndroidBuild
    {
        const string OutputPath = "Builds/SpaceInvaders.apk";
        public const string ResultPath = "Builds/last_android_build.txt";
        const string IconPath = SpaceInvadersBuilder.RootDir + "/Sprites/app_icon.png";

        [MenuItem("Tools/Space Invaders/Build Android APK")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds");
            File.Delete(ResultPath);

            PlayerSettings.productName = "Tiny UFO"; // так игра называется на телефоне
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gc26.spaceinvaders");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Иконка приложения: капсула-пришелец с выстрелом на тёмном фоне
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
            else
                Debug.LogWarning("Space Invaders: app icon not found at " + IconPath);
            EditorUserBuildSettings.buildAppBundle = false;
            AssetDatabase.SaveAssets();

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SpaceInvadersBuilder.MainScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            string result = $"{report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize} time={report.summary.totalTime}";
            File.WriteAllText(ResultPath, result);
            Debug.Log("Space Invaders: Android build " + result);
        }
    }
}
