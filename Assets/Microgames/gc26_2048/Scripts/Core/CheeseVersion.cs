namespace GC26._2048
{
    /// <summary>
    /// Version of the game, MAJOR.MINOR.PATCH. The single source of truth: keep "version" in microgame.json
    /// and CHANGELOG.md in step (CheeseVersionTests checks microgame.json). Player Settings' bundleVersion belongs
    /// to the whole GC26 project, so it is not used here.
    /// </summary>
    public static class CheeseVersion
    {
        public const string Current = "0.0.1";
    }
}
