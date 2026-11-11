using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Debug log for Time Traveler Runner. Every line starts with [gc26_timetraveler] so it is easy
    /// to filter in the Console (type "timetraveler" in the Console search box).
    /// Stateless (rule 6): only formats and forwards to Unity's Debug.
    /// </summary>
    public static class TimetravelerLog
    {
        const string Prefix = "[" + TimetravelerGame.Id + "] ";

        public static void Info(string message, Object context = null)
        {
            Debug.Log(Prefix + message, context);
        }

        public static void Warn(string message, Object context = null)
        {
            Debug.LogWarning(Prefix + message, context);
        }

        public static void Error(string message, Object context = null)
        {
            Debug.LogError(Prefix + message, context);
        }
    }
}
