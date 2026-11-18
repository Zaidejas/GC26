namespace GC26.Lines.Game
{
    /// <summary>
    /// The game's version, shown small at the bottom of the start screen. Phone builds get the same number
    /// (PhoneAppSettings), so the screen and the phone's app info always agree. The project-wide
    /// PlayerSettings version belongs to the organiser, so the number lives here.
    /// Raise it before a build that brings new features to the phones.
    /// </summary>
    public static class LinesVersion
    {
        public const string Number = "0.2.0";

        /// <summary>What the start screen shows.</summary>
        public const string Label = "v" + Number;
    }
}
