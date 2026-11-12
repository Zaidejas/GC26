using System;
using System.IO;
using GC26.Core;
using GC26.Lines.Core;
using GC26.Lines.Game;
using UnityEngine;

namespace GC26.Lines.Services
{
    /// <summary>
    /// Keeps the Settings choices (<see cref="GameSettings"/>) in their own JSON file in the game's save folder,
    /// next to the game in progress and the leaderboard. A missing or damaged file means the defaults.
    /// </summary>
    public class SettingsService
    {
        public const string FileName = "settings.json";

        readonly string path;

        public SettingsService(string folder = null)
        {
            folder = folder ?? SaveService.FolderOverride ?? MicrogameSave.Folder(LinesMicrogame.Id);
            path = Path.Combine(folder, FileName);
        }

        public string FilePath => path;

        public GameSettings Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var settings = JsonUtility.FromJson<GameSettings>(File.ReadAllText(path));
                    if (settings != null) return settings;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: couldn't read the settings, using the defaults. " + e.Message);
            }
            return new GameSettings();
        }

        public void Save(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(settings));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: couldn't save the settings. " + e.Message);
            }
        }
    }
}
