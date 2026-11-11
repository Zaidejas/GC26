using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The pocket watch's time slow: the whole run (traveler included) slows down while energy lasts.
    /// Toggle on and off; it also stops when the energy runs out.
    /// Changes Time.timeScale, so it always restores the value it found (rule 6): the session calls
    /// Restore() on death, restart, exit and OnDisable.
    /// </summary>
    public class RunnerTimeSlow
    {
        public float slowScale = 0.45f;
        [Tooltip("Seconds of slow time from a full watch (real time)")]
        public float fullDuration = 3f;
        [Tooltip("Seconds to refill an empty watch while not slowing (real time)")]
        public float refillDuration = 8f;
        [Tooltip("Energy needed to start (0..1)")]
        public float minEnergyToStart = 0.25f;

        public bool Active { get; private set; }
        public float Energy { get; private set; } = 1f;

        float _restoreScale = 1f;

        public void Reset()
        {
            Restore();
            Energy = 1f;
        }

        public void Toggle()
        {
            if (Active) Restore();
            else if (Energy >= minEnergyToStart)
            {
                _restoreScale = Time.timeScale;
                Time.timeScale = slowScale;
                Active = true;
            }
        }

        /// <summary>Call every frame with unscaled time.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (Active)
            {
                Energy -= unscaledDeltaTime / fullDuration;
                if (Energy <= 0f)
                {
                    Energy = 0f;
                    Restore();
                }
            }
            else
            {
                Energy = Mathf.Min(1f, Energy + unscaledDeltaTime / refillDuration);
            }
        }

        public void Restore()
        {
            if (!Active) return;
            Time.timeScale = _restoreScale;
            Active = false;
        }
    }
}
