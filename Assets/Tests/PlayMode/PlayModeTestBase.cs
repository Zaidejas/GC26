using System.IO;
using Lines.Services;
using NUnit.Framework;

namespace Lines.Tests
{
    /// <summary>
    /// Every play-mode test saves into its own empty temporary folder, so it starts with no saved game
    /// and never touches the real one on this computer.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        protected string SaveFolder { get; private set; }

        [SetUp]
        public void UseTemporarySaveFolder()
        {
            SaveFolder = Path.Combine(Path.GetTempPath(), "LinesTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(SaveFolder);
            SaveService.FolderOverride = SaveFolder;
        }

        [TearDown]
        public void RemoveTemporarySaveFolder()
        {
            SaveService.FolderOverride = null;
            try { Directory.Delete(SaveFolder, true); } catch { /* best effort */ }
        }
    }
}
