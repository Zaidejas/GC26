using System;

namespace GC26.Bomber
{
    /// <summary>
    /// Phone auto-fire limiter (see TDD 11.1). In the safe zone it never lets a shot push the core
    /// into Overdrive; with Forsage on there is no limit, and Forsage switches itself off on overheat.
    /// </summary>
    public sealed class HeatGovernor
    {
        public const float Hysteresis = 12f;
        public const float SafeCeiling = 94f;

        public bool Forsage;
        bool _hold;

        public bool AllowShot(HeatModel core, float shotHeat)
        {
            if (!core.CanFire) { Forsage = false; _hold = false; return false; }
            var cap = Forsage ? HeatModel.Max + 1f : Math.Min(core.Settings.OverdriveAt - shotHeat - 1f, SafeCeiling);
            if (core.Heat >= cap) _hold = true;
            else if (core.Heat < cap - Hysteresis) _hold = false;
            return !_hold;
        }
    }
}
