using System.Collections.Generic;
using Dustbowl.Course;

namespace Dustbowl.Race
{
    /// <summary>
    /// One selectable event on the setup screen. Mirrors the web build's COURSES
    /// card data (name, round, lap count, tag line and blurb) without any course
    /// geometry, so future rounds only need an entry here plus their own scene
    /// content.
    /// </summary>
    public sealed class RaceEventDefinition
    {
        public RaceEventDefinition(string id, string name, string tag, string blurb, int laps, int riders)
        {
            Id = id;
            Name = name;
            Tag = tag;
            Blurb = blurb;
            Laps = laps;
            Riders = riders;
        }

        public string Id { get; }
        public string Name { get; }
        public string Tag { get; }
        public string Blurb { get; }
        public int Laps { get; }
        public int Riders { get; }
    }

    /// <summary>The National series calendar shown on the setup screen.</summary>
    public static class RaceEventCatalog
    {
        public static readonly IReadOnlyList<RaceEventDefinition> Events = new[]
        {
            new RaceEventDefinition(
                DustbowlFlatsNationalCourse.CourseId,
                DustbowlFlatsNationalCourse.CourseName,
                "the classic",
                "Where the series started. Rolling desert, banked corners, a lap that builds from a gentle opener to the big one.",
                DustbowlFlatsNationalCourse.Laps,
                DustbowlFlatsNationalCourse.RiderCount)
        };

        public static int Count => Events.Count;

        public static RaceEventDefinition Get(int index)
        {
            if (Events.Count == 0)
            {
                return null;
            }

            index = ((index % Events.Count) + Events.Count) % Events.Count;
            return Events[index];
        }
    }
}
