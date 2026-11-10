using UnityEditor;
using UnityEditor.SceneManagement;

namespace Lines.EditorTools
{
    /// <summary>
    /// Pressing Play in the editor always starts the game's only scene (Assets/Scenes/Main.unity),
    /// whatever scene happens to be open; for example, an empty one left behind by a batch run.
    /// The open scene comes back when Play stops.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromMainScene
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        static PlayFromMainScene()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
        }
    }
}
