using System.Linq;
using UnityEditor;

// Следит, чтобы сцены MainMenu и Game всегда были в Build Settings (MainMenu первой)
[InitializeOnLoad]
public static class SpaceInvadersBuildSettings
{
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string GameScenePath = "Assets/Scenes/Game.unity";

    static SpaceInvadersBuildSettings()
    {
        EnsureScenes();
        EditorApplication.delayCall += EnsureScenes;
    }

    static void EnsureScenes()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            return;

        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length >= 2 && scenes[0].path == MenuScenePath && scenes[1].path == GameScenePath)
            return;

        var others = scenes.Where(s => s.path != MenuScenePath && s.path != GameScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        }.Concat(others).ToArray();
    }
}
