using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GC26._2048Nastya.EditorTools
{
    /// <summary>
    /// After every script reload in the editor: if the entry scene was built for an older
    /// <see cref="Game2048Nastya.SceneVersion"/>, run <see cref="ColoursBuilder2048.Build"/> automatically,
    /// so pulling new code never leaves a scene that cannot start. Skipped in Play mode, in batch mode
    /// and while any open scene has unsaved changes (then it asks in the Console to rebuild from the menu).
    /// </summary>
    [InitializeOnLoad]
    static class ColoursSceneUpdater2048
    {
        static ColoursSceneUpdater2048()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (ColoursBuilder2048.IsSceneUpToDate()) return;

            bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
            if (dirty)
            {
                Debug.LogWarning("[gc26_2048_nastya] the entry scene is out of date, but an open scene has unsaved " +
                                 "changes. Save them, then click GC26 → Colours (nastya) → Rebuild assets and scene.");
                return;
            }

            // Come back to whatever scene was open (the builder leaves its own scene open).
            string previous = SceneManager.GetActiveScene().path;
            Debug.Log("[gc26_2048_nastya] the entry scene is out of date: rebuilding it");
            ColoursBuilder2048.Build();
            if (!string.IsNullOrEmpty(previous) && previous != ColoursBuilder2048.EntryScenePath)
                EditorSceneManager.OpenScene(previous);
        }
    }
}
