using System.IO;
using UnityEngine;

namespace GC26.Core
{
    /// <summary>
    /// Where a game keeps its saves. The format and the save logic are up to each game;
    /// only the location is shared, so saves of different games never collide.
    /// </summary>
    public static class MicrogameSave
    {
        /// <summary>persistentDataPath/&lt;id&gt;/, created on first use. Put all save files here.</summary>
        public static string Folder(string gameId)
        {
            var dir = Path.Combine(Application.persistentDataPath, gameId);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>PlayerPrefs key with the game prefix: &lt;id&gt;_&lt;key&gt;.</summary>
        public static string Key(string gameId, string key) => gameId + "_" + key;
    }
}
