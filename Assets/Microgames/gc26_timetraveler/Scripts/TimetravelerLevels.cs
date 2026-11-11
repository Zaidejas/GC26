using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>One story level: a fixed track (same seed every attempt, so it can be learned) with a finish line.</summary>
    public class TimetravelerLevel
    {
        public string Key;          // save key part, e.g. "dino_1"
        public string Title;        // shown on the map, e.g. "1-1"
        public int Order;           // position in the story (0 = first level)
        public int LengthMeters;
        public int Seed;
        /// <summary>Obstacle groups: 0 basic, 1 +steps/ledge/tall wall, 2 +world switch, 3 +moving traps.</summary>
        public int Tier;
        public Vector2 MapPosition;
    }

    /// <summary>A timeline region on the world map.</summary>
    public class TimetravelerEra
    {
        public string Name;
        public float MapBottom;     // region spans MapBottom .. MapBottom + EraHeight on the map
        public bool Playable;
    }

    /// <summary>Level and era data. Edit here to change lengths, order or map layout (read-only data, no state).</summary>
    public static class TimetravelerLevels
    {
        public const float EraHeight = 34f;
        public const float MapHalfWidth = 10f;

        public static readonly TimetravelerLevel[] Era1 =
        {
            new TimetravelerLevel { Key = "dino_1", Title = "1-1", Order = 0, LengthMeters = 120, Seed = 101, Tier = 0, MapPosition = new Vector2(-4f, 4f) },
            new TimetravelerLevel { Key = "dino_2", Title = "1-2", Order = 1, LengthMeters = 160, Seed = 102, Tier = 1, MapPosition = new Vector2(3.5f, 9f) },
            new TimetravelerLevel { Key = "dino_3", Title = "1-3", Order = 2, LengthMeters = 200, Seed = 103, Tier = 2, MapPosition = new Vector2(-3f, 15f) },
            new TimetravelerLevel { Key = "dino_4", Title = "1-4", Order = 3, LengthMeters = 240, Seed = 104, Tier = 3, MapPosition = new Vector2(4f, 21f) },
            new TimetravelerLevel { Key = "dino_5", Title = "1-5", Order = 4, LengthMeters = 300, Seed = 105, Tier = 3, MapPosition = new Vector2(-2f, 27f) },
        };

        /// <summary>The endless-run node on the map (after the last level of the first timeline).</summary>
        public static readonly Vector2 EndlessMapPosition = new Vector2(5f, 30f);

        public static readonly TimetravelerEra[] Eras =
        {
            new TimetravelerEra { Name = "Age of Dinosaurs", MapBottom = 0f, Playable = true },
            new TimetravelerEra { Name = "The Age of Exploration", MapBottom = EraHeight, Playable = false },
            new TimetravelerEra { Name = "Renaissance", MapBottom = EraHeight * 2f, Playable = false },
        };
    }
}
