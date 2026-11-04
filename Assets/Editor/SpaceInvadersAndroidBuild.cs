using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Сборка APK: Tools → Space Invaders → Build Android APK. Файл появляется в папке Builds
public static class SpaceInvadersAndroidBuild
{
    const string OutputPath = "Builds/SpaceInvaders.apk";
    public const string ResultPath = "Builds/last_android_build.txt";

    [MenuItem("Tools/Space Invaders/Build Android APK")]
    public static void Build()
    {
        Directory.CreateDirectory("Builds");
        File.Delete(ResultPath);

        PlayerSettings.productName = "Space Invaders";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gc26.spaceinvaders");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;
        AssetDatabase.SaveAssets();

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Game.unity" },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });

        string result = $"{report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize} time={report.summary.totalTime}";
        File.WriteAllText(ResultPath, result);
        Debug.Log("Space Invaders: Android build " + result);
    }
}
