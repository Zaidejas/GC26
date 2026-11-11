using System;

namespace GC26.Bomber
{
    public enum CoreState { Normal, Overdrive, Overheated }

    /// <summary>Tuning values of the core. Defaults match the HTML prototype.</summary>
    [Serializable]
    public struct HeatSettings
    {
        public float CoolPerSecond;        // heat removed per second once cooling starts
        public float CoolDelay;            // seconds after the last shot before cooling starts
        public float OverdriveAt;          // heat at which Overdrive starts
        public float OverheatSeconds;      // weapons offline after reaching 100
        public float OverdriveDamageBonus; // +30% damage in Overdrive
        public float RecoverTo;            // heat right after an overheat ends

        public static HeatSettings Default => new HeatSettings
        {
            CoolPerSecond = 25f, CoolDelay = 0.5f, OverdriveAt = 80f,
            OverheatSeconds = 2f, OverdriveDamageBonus = 0.3f, RecoverTo = 40f,
        };
    }

    /// <summary>
    /// The core heat mechanic, pure C# (no UnityEngine) so it is covered by EditMode tests.
    /// Shots add heat; at OverdriveAt damage goes up; at 100 the weapons go offline for OverheatSeconds.
    /// </summary>
    public sealed class HeatModel
    {
        public const float Max = 100f;

        public HeatSettings Settings;
        public float Heat { get; private set; }
        public CoreState State { get; private set; }
        public float OverheatLeft { get; private set; }
        public int OverheatCount { get; private set; }

        public event Action Overheated;
        public event Action EnteredOverdrive;

        float _sinceShot = 999f;

        public HeatModel(HeatSettings settings) { Settings = settings; }

        public bool CanFire => State != CoreState.Overheated;
        public float DamageMultiplier => State == CoreState.Overdrive ? 1f + Settings.OverdriveDamageBonus : 1f;
        public float Normalized => Heat / Max;

        public void AddHeat(float amount)
        {
            if (!CanFire || amount <= 0f) return;
            Heat = Math.Min(Max, Heat + amount);
            _sinceShot = 0f;
            if (Heat >= Max)
            {
                State = CoreState.Overheated;
                OverheatLeft = Settings.OverheatSeconds;
                OverheatCount++;
                Overheated?.Invoke();
            }
            else UpdateState();
        }

        /// <summary>Removes heat (the Vent tactic). Does nothing while overheated.</summary>
        public void Vent(float amount)
        {
            if (State == CoreState.Overheated || amount <= 0f) return;
            Heat = Math.Max(0f, Heat - amount);
            UpdateState();
        }

        public void Tick(float dt, float coolMultiplier = 1f)
        {
            if (dt <= 0f) return;
            if (State == CoreState.Overheated)
            {
                OverheatLeft -= dt;
                var rate = (Max - Settings.RecoverTo) / Math.Max(0.5f, Settings.OverheatSeconds);
                Heat = Math.Max(Settings.RecoverTo, Heat - rate * dt);
                if (OverheatLeft <= 0f) { State = CoreState.Normal; Heat = Settings.RecoverTo; OverheatLeft = 0f; }
                return;
            }
            _sinceShot += dt;
            if (_sinceShot > Settings.CoolDelay) Heat = Math.Max(0f, Heat - Settings.CoolPerSecond * coolMultiplier * dt);
            UpdateState();
        }

        void UpdateState()
        {
            var prev = State;
            State = Heat >= Settings.OverdriveAt ? CoreState.Overdrive : CoreState.Normal;
            if (prev != CoreState.Overdrive && State == CoreState.Overdrive) EnteredOverdrive?.Invoke();
        }
    }
}
