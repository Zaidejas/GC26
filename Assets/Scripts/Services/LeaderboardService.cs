using System;
using System.IO;
using Lines.Core;
using UnityEngine;

namespace Lines.Services
{
    /// <summary>
    /// Keeps the leaderboard (top 10 and the player-name memory) in its own JSON file, separate from the
    /// game in progress (GDD → Saving). Uses the same folder as <see cref="SaveService"/>, so tests that
    /// point saving at a temporary folder also get a fresh leaderboard.
    /// </summary>
    public class LeaderboardService
    {
        public const string FileName = "leaderboard.json";

        readonly string path;

        public LeaderboardService(string folder = null)
        {
            folder = folder ?? SaveService.FolderOverride ?? Application.persistentDataPath;
            path = Path.Combine(folder, FileName);
        }

        public string FilePath => path;

        /// <summary>The saved leaderboard, or an empty one if there is none or it can't be read.</summary>
        public Leaderboard Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var board = JsonUtility.FromJson<Leaderboard>(File.ReadAllText(path));
                    if (board != null)
                    {
                        board.Tidy();
                        return board;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: could not read the leaderboard: " + e.Message);
            }
            return new Leaderboard();
        }

        public void Save(Leaderboard board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(board));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Lines: could not save the leaderboard: " + e.Message);
            }
        }

        /// <summary>Now, in the leaderboard's time format (milliseconds since 1 Jan 1970, UTC).</summary>
        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
