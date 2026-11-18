using System.IO;
using GC26.Lines.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// Every play-mode test saves into its own empty temporary folder, so it starts with no saved game
    /// and never touches the real one on this computer.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        /// <summary>The game's scene, inside the game folder.</summary>
        public const string GameScenePath = "Assets/Microgames/gc26_lines/Scenes/gc26_lines_Main.unity";

        /// <summary>
        /// Starts the game's scene, as pressing Play does. It is loaded by its path, because the shared
        /// Build Settings (where a scene must be listed to load by name) belong to the organiser.
        /// </summary>
        protected static AsyncOperation LoadGameScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                GameScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(GameScenePath);
#endif
        }

        /// <summary>
        /// L17: marks every tutorial as seen in this game, for tests that play real moves: a random rainbow
        /// in NEXT would otherwise show its tutorial, which blocks the board and the buttons.
        /// </summary>
        protected static void SkipTutorials(GC26.Lines.Game.GameBootstrap game)
        {
            var state = game.Controller?.Game;
            if (state == null) return;
            foreach (var key in new[] { "rainbow", "ice", "super", "rescue" }) state.TutorialsShown.Add(key);
        }

        protected string SaveFolder { get; private set; }

        [SetUp]
        public void UseTemporarySaveFolder()
        {
            SaveFolder = Path.Combine(Path.GetTempPath(), "LinesTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(SaveFolder);
            SaveService.FolderOverride = SaveFolder;
            // English, whatever language this computer uses (tests check English texts).
            new SettingsService(SaveFolder).Save(new GC26.Lines.Core.GameSettings { language = "en" });
            GC26.Lines.UI.Texts.Set(GC26.Lines.UI.Language.English);
        }

        [TearDown]
        public void RemoveTemporarySaveFolder()
        {
            SaveService.FolderOverride = null;
            try { Directory.Delete(SaveFolder, true); } catch { /* best effort */ }
        }
    }
}
