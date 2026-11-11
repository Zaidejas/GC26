using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GC26.Lines.EditorTools
{
    /// <summary>
    /// When the editor opens on an empty, unsaved scene (batch runs such as the layout screenshots leave
    /// one behind), it switches to the game's scene, Scenes/gc26_lines_Main.unity, so Play starts the
    /// game instead of a blank blue screen. Play itself is left alone, so the test runner's own scenes work.
    /// Only while Lines is the only game in Assets/Microgames (our own branch): in the merged GC26 project
    /// the other teams' editors are left alone.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromMainScene
    {
        static PlayFromMainScene()
        {
            EditorApplication.delayCall += OpenMainSceneIfBlank;
        }

        static void OpenMainSceneIfBlank()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.sceneCount != 1) return;
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) || scene.isDirty) return; // a saved scene, or unsaved work
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.MainScenePath) == null) return;
            if (System.IO.Directory.GetDirectories("Assets/Microgames").Length != 1) return; // other games here too
            EditorSceneManager.OpenScene(ProjectSetup.MainScenePath);
        }
    }
}
