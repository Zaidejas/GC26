using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26.Timetraveler.EditorTools
{
    /// <summary>
    /// Editor helpers for Time Traveler Runner. Nothing here runs on its own,
    /// so other teams' editors are never changed.
    /// </summary>
    public static class TimetravelerEditorMenu
    {
        const string Root = "Assets/Microgames/" + TimetravelerGame.Id;
        public const string MainScenePath = Root + "/Scenes/" + TimetravelerGame.Id + "_Main.unity";

        [MenuItem("Tools/Time Traveler/View as Phone (Simulator)")]
        public static void ShowPhone()
        {
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.SimulatorView);
        }

        [MenuItem("Tools/Time Traveler/View as Portrait Game View (1080x1920)")]
        public static void ShowPortraitGameView()
        {
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1080, 1920, "Phone Portrait 1080x1920");
        }

        [MenuItem("Tools/Time Traveler/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (!File.Exists(MainScenePath)) CreateMainScene();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MainScenePath);
        }

        /// <summary>
        /// Creates Scenes/gc26_timetraveler_Main.unity with a camera and the IMicrogame entry
        /// component. Does nothing if the scene already exists. Also callable in batch mode:
        /// -executeMethod GC26.Timetraveler.EditorTools.TimetravelerEditorMenu.CreateMainScene
        /// </summary>
        public static void CreateMainScene()
        {
            if (File.Exists(MainScenePath))
            {
                Debug.Log($"[{TimetravelerGame.Id}] Main scene already exists: {MainScenePath}");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("TimetravelerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 15f; // 480 px tall canvas / 16 px per unit / 2
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("TimetravelerRoot").AddComponent<TimetravelerGame>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[{TimetravelerGame.Id}] Created {MainScenePath}");
        }
    }
}
