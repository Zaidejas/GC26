namespace GC26._2048
{
    /// <summary>
    /// Counts time without player input. <see cref="Tick"/> returns true once per <see cref="Interval"/> seconds
    /// of idling (at 15 s, 30 s, 45 s…); any input calls <see cref="Reset"/> and the count starts over.
    /// </summary>
    public sealed class IdleTimer
    {
        /// <summary>Seconds without input between two mouse peeks.</summary>
        public const float DefaultInterval = 15f;

        public float Interval { get; }
        public float Idle { get; private set; }

        public IdleTimer(float interval = DefaultInterval)
        {
            Interval = interval > 0f ? interval : DefaultInterval;
        }

        public void Reset() => Idle = 0f;

        /// <summary>Adds <paramref name="seconds"/> of idling; true when the interval is reached (then counts again from 0).</summary>
        public bool Tick(float seconds)
        {
            if (seconds > 0f) Idle += seconds;
            if (Idle < Interval) return false;
            Idle = 0f; // a long pause (app in background) gives one peek, not a burst
            return true;
        }
    }
}
