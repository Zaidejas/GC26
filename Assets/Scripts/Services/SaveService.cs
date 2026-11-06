using System;
using System.IO;
using Lines.Core;
using UnityEngine;

namespace Lines.Services
{
    /// <summary>
    /// Keeps the game in progress in a JSON file on the device (Application.persistentDataPath).
    /// Writing goes to a temporary file first and then replaces the old one, so a crash mid-save can't
    /// leave a half-written file. A missing or damaged file simply means "no saved game".
    /// </summary>
    public class SaveService
    {
        public const string FileName = "game.json";

        /// <summary>Tests point this at a temporary folder so they never touch a real saved game.</summary>
        public static string FolderOverride;

        readonly string path;

        public SaveService(string folder = null)
        {
            folder = folder ?? FolderOverride ?? Application.persistentDataPath;
            path = Path.Combine(folder, FileName);
        }

        public string FilePath => path;
        public bool HasSave => File.Exists(path);

        public void Save(GameSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(save));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: could not save the game: " + e.Message);
            }
        }

        /// <summary>The saved game, or null if there is none or it can't be read.</summary>
        public GameSave Load()
        {
            try
            {
                if (!File.Exists(path)) return null;
                var save = JsonUtility.FromJson<GameSave>(File.ReadAllText(path));
                return save != null && save.board != null ? save : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: could not read the saved game: " + e.Message);
                return null;
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: could not delete the saved game: " + e.Message);
            }
        }
    }
}
