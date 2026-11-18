using System;
using System.IO;
using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Player settings, in their own file next to the save (MicrogameSave.Folder(Id)/settings.json),
    /// so "New game" keeps them. Changed on the Settings screen, written straight away.
    /// Nothing global is touched (no AudioListener.volume): each sound source reads its volume from here.
    /// </summary>
    public class TimetravelerSettings
    {
        public const string FileName = "settings.json";

        [Serializable]
        class Data
        {
            public float musicVolume = 0.8f;
            public float soundVolume = 0.9f;
            public int shake = 2;               // 0 off, 1 weak, 2 full
            public bool tutorialSlowMotion = true;
            public bool showControls = true;
            public int rhythmOffsetMs;          // + = notes judged later (for phones whose sound comes late)
        }

        readonly string _path;               // null = defaults only, never written (tests, previews)
        Data _data = new Data();

        public float MusicVolume { get => _data.musicVolume; set { _data.musicVolume = Mathf.Clamp01(value); Write(); } }
        public float SoundVolume { get => _data.soundVolume; set { _data.soundVolume = Mathf.Clamp01(value); Write(); } }
        /// <summary>0 = no camera shake, 1 = weak, 2 = full.</summary>
        public int Shake { get => _data.shake; set { _data.shake = Mathf.Clamp(value, 0, 2); Write(); } }
        public float ShakeFactor => _data.shake == 0 ? 0f : _data.shake == 1 ? 0.4f : 1f;
        public bool TutorialSlowMotion { get => _data.tutorialSlowMotion; set { _data.tutorialSlowMotion = value; Write(); } }
        public bool ShowControls { get => _data.showControls; set { _data.showControls = value; Write(); } }
        public int RhythmOffsetMs { get => _data.rhythmOffsetMs; set { _data.rhythmOffsetMs = Mathf.Clamp(value, -200, 200); Write(); } }

        /// <summary>The player's settings from disk (defaults if there is no file yet).</summary>
        public static TimetravelerSettings Load() =>
            new TimetravelerSettings(Path.Combine(MicrogameSave.Folder(TimetravelerGame.Id), FileName));

        /// <summary>Default settings that are never saved (tests, a run started without the game flow).</summary>
        public static TimetravelerSettings Defaults() => new TimetravelerSettings(null);

        TimetravelerSettings(string path)
        {
            _path = path;
            if (path == null || !File.Exists(path)) return;
            try
            {
                _data = JsonUtility.FromJson<Data>(File.ReadAllText(path)) ?? new Data();
            }
            catch (Exception e)
            {
                TimetravelerLog.Warn($"Settings file unreadable ({e.Message}); using defaults.");
                _data = new Data();
            }
        }

        public void ResetToDefaults()
        {
            _data = new Data();
            Write();
        }

        void Write()
        {
            if (_path == null) return;
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(_data, true));
            }
            catch (IOException e)
            {
                TimetravelerLog.Warn($"Could not save settings: {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                TimetravelerLog.Warn($"Could not save settings: {e.Message}");
            }
        }
    }
}
