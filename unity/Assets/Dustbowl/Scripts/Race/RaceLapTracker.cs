using System;

namespace Dustbowl.Race
{
    [Serializable]
    public sealed class RaceLapTracker
    {
        private float previousAlong;
        private bool sectorSeen;

        public int Lap { get; private set; } = 1;
        public bool Finished { get; private set; }

        public void Reset(float initialAlong)
        {
            previousAlong = initialAlong;
            sectorSeen = false;
            Lap = 1;
            Finished = false;
        }

        public bool Update(float along, float lapLength, int totalLaps, out bool raceFinished)
        {
            raceFinished = false;
            if (Finished || lapLength <= 0f)
            {
                previousAlong = along;
                return false;
            }

            if (along > lapLength * .45f && along < lapLength * .75f)
            {
                sectorSeen = true;
            }

            bool wrapped = previousAlong > lapLength * .8f && along < lapLength * .2f;
            previousAlong = along;
            if (!wrapped || !sectorSeen)
            {
                return false;
            }

            sectorSeen = false;
            if (Lap >= totalLaps)
            {
                Finished = true;
                raceFinished = true;
            }
            else
            {
                Lap++;
            }

            return true;
        }

        public float EffectiveDistance(float along, float lapLength, int totalLaps, float finishTime)
        {
            return Finished
                ? (totalLaps + 1) * lapLength - finishTime
                : (Lap - 1) * lapLength + along;
        }
    }
}
