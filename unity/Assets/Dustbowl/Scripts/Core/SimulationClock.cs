using System;

namespace Dustbowl.Core
{
    public static class SimulationTiming
    {
        public const int FixedHz = 60;
        public const float FixedDeltaSeconds = 1f / FixedHz;
    }

    [Serializable]
    public sealed class SimulationClock
    {
        public ulong Tick { get; private set; }
        public double ElapsedSeconds => Tick / (double)SimulationTiming.FixedHz;

        public void Advance()
        {
            Tick++;
        }

        public void Reset()
        {
            Tick = 0;
        }
    }
}
