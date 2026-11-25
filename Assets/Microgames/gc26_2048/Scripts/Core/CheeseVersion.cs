namespace GC26._2048
{
    /// <summary>
    /// Version of the game. Raise it with GC26 → gc26_2048 → Build (it also updates microgame.json and CHANGELOG.md);
    /// CheeseVersionTests keep them in step. Player Settings' bundleVersion belongs to the whole GC26 project,
    /// so it is not used here.
    /// </summary>
    public static class CheeseVersion
    {
        /// <summary>MAJOR.MINOR.PATCH of the latest release.</summary>
        public const string Current = "0.0.1";

        /// <summary>Build number: +1 on every build made from the menu, release or development.</summary>
        public const int Build = 2;

        /// <summary>Shown to the player: "0.0.1" in a release build, "0.0.1-dev.7" in a development build or the editor.</summary>
        public static string Display => UnityEngine.Debug.isDebugBuild ? $"{Current}-dev.{Build}" : Current;
    }
}
