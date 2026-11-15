using System;

namespace GC26.Lines.Core
{
    /// <summary>
    /// The player's choices in Settings (GDD → Screens → Settings): music, sounds and vibration (all on at
    /// first) and the language ("en", "ru", or empty until chosen: then the phone's language decides).
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public bool music = true;
        public bool sounds = true;
        public bool vibration = true;
        public string language = "";
        /// <summary>L09: the mode chosen on the menu, "classic" or "timed" (remembered).</summary>
        public string mode = "classic";

        public GameSettings Copy() => (GameSettings)MemberwiseClone();
    }
}
