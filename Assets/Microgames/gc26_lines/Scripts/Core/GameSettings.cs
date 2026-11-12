using System;

namespace GC26.Lines.Core
{
    /// <summary>
    /// The player's choices in Settings (GDD → Screens → Settings): music, sounds and vibration (all on at
    /// first) and the language ("en", "ru", or empty until chosen: then the phone's language decides).
    /// The game has no sound yet, so music, sounds and vibration are only remembered for now.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public bool music = true;
        public bool sounds = true;
        public bool vibration = true;
        public string language = "";

        public GameSettings Copy() => (GameSettings)MemberwiseClone();
    }
}
