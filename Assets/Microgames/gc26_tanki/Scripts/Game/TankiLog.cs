using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// The game's own lines in the Unity Console, all starting with [Tanki] so they are easy to filter.
    /// Info lines (pages, buttons, battle results) appear only in the editor and development builds;
    /// warnings and errors always do.
    /// </summary>
    static class TankiLog
    {
        const string Tag = "[Tanki] ";

        public static void Info(string text)
        {
            if (Debug.isDebugBuild) Debug.Log(Tag + text);
        }

        public static void Warn(string text) => Debug.LogWarning(Tag + text);

        public static void Error(string text) => Debug.LogError(Tag + text);

        /// <summary>Loads from Resources and warns, instead of failing later with a bare NullReferenceException.</summary>
        public static T Load<T>(string path) where T : Object
        {
            T asset = Resources.Load<T>(path);
            if (asset == null) Warn("Не найден ресурс Resources/" + path + " (" + typeof(T).Name + ")");
            return asset;
        }
    }
}
