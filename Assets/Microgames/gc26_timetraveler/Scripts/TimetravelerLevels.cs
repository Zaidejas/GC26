using System.Linq;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>One story level: a fixed track (same seed every attempt, so it can be learned) with a finish line.</summary>
    public class TimetravelerLevel
    {
        public string Key;          // save key part, e.g. "dino_1"
        public int Era;             // index in TimetravelerLevels.Eras (track style, colours)
        public string Title;        // shown on the map, e.g. "1-1"
        public int Order;           // position in the story (0 = first level)
        public int LengthMeters;
        public int Seed;
        /// <summary>Obstacle groups: 0 basic, 1 +steps/ledge/tall wall, 2 +world switch, 3 +moving traps.</summary>
        public int Tier;
        public Vector2 MapPosition;
        /// <summary>Run speed at the start and the top speed (units per second).</summary>
        public float StartSpeed = 7f, MaxSpeed = 12f;
        /// <summary>Ground between patterns: 1 = normal, smaller = obstacles closer together.</summary>
        public float Density = 1f;
        /// <summary>Adds triple and mixed combos.</summary>
        public bool Hard;
    }

    public enum PlaceKind { Npc, Shop, Minigame }

    /// <summary>A non-level spot on the map (NPC, shop, minigame). Placeholder until those systems exist.</summary>
    public class TimetravelerPlace
    {
        public PlaceKind Kind;
        public string Name;
        public Vector2 MapPosition;
    }

    /// <summary>A timeline region on the world map.</summary>
    public class TimetravelerEra
    {
        public string Name;
        public float MapBottom;     // region spans MapBottom .. MapBottom + EraHeight on the map
        public TimetravelerLevel[] Levels;
        /// <summary>Colour of this timeline's runner background and parallax hills (the PAST world;
        /// the FUTURE world stays cold blue everywhere so the two worlds never look alike).</summary>
        public Color Sky;
        public Color Hills;
        /// <summary>Opens when its first level opens (the last level of the timeline before is done).</summary>
        public bool IsOpen(TimetravelerSave save) => Levels.Length > 0 && save.IsOpen(Levels[0]);
    }

    /// <summary>Level and era data. Edit here to change lengths, order or map layout (read-only data, no state).</summary>
    public static class TimetravelerLevels
    {
        /// <summary>Each timeline region is this tall on the map (units); regions stack upwards.</summary>
        public const float EraHeight = 40f;
        /// <summary>The map is wider than the screen (16.9 units): the camera scrolls with the traveler.</summary>
        public const float MapHalfWidth = 22f;

        public static readonly TimetravelerLevel[] Era1 =
        {
            new TimetravelerLevel { Key = "dino_1", Title = "1-1", Order = 0, LengthMeters = 150, Seed = 101, Tier = 0, StartSpeed = 7f, MaxSpeed = 9.5f, Density = 1f, MapPosition = new Vector2(-12f, 4f) },
            new TimetravelerLevel { Key = "dino_2", Title = "1-2", Order = 1, LengthMeters = 220, Seed = 102, Tier = 1, StartSpeed = 7.5f, MaxSpeed = 10.5f, Density = 0.85f, MapPosition = new Vector2(-1f, 8f) },
            new TimetravelerLevel { Key = "dino_3", Title = "1-3", Order = 2, LengthMeters = 280, Seed = 103, Tier = 2, StartSpeed = 8f, MaxSpeed = 11.5f, Density = 0.75f, MapPosition = new Vector2(11f, 12f) },
            new TimetravelerLevel { Key = "dino_4", Title = "1-4", Order = 3, LengthMeters = 340, Seed = 104, Tier = 3, StartSpeed = 8.5f, MaxSpeed = 12.5f, Density = 0.65f, MapPosition = new Vector2(15f, 24f) },
            new TimetravelerLevel { Key = "dino_5", Title = "1-5", Order = 4, LengthMeters = 420, Seed = 105, Tier = 3, StartSpeed = 9f, MaxSpeed = 13f, Density = 0.55f, Hard = true, MapPosition = new Vector2(2f, 30f) },
        };

        // The Age of Exploration: islands, sea gaps and bridges between worlds.
        public static readonly TimetravelerLevel[] Era2 =
        {
            new TimetravelerLevel { Key = "sea_1", Title = "2-1", Era = 1, Order = 5, LengthMeters = 300, Seed = 201, Tier = 3, StartSpeed = 8f, MaxSpeed = 11f, Density = 0.8f, MapPosition = new Vector2(14f, 44f) },
            new TimetravelerLevel { Key = "sea_2", Title = "2-2", Era = 1, Order = 6, LengthMeters = 340, Seed = 202, Tier = 3, StartSpeed = 8.5f, MaxSpeed = 11.5f, Density = 0.75f, MapPosition = new Vector2(3f, 49f) },
            new TimetravelerLevel { Key = "sea_3", Title = "2-3", Era = 1, Order = 7, LengthMeters = 380, Seed = 203, Tier = 3, StartSpeed = 8.5f, MaxSpeed = 12f, Density = 0.7f, Hard = true, MapPosition = new Vector2(-10f, 54f) },
            new TimetravelerLevel { Key = "sea_4", Title = "2-4", Era = 1, Order = 8, LengthMeters = 420, Seed = 204, Tier = 3, StartSpeed = 9f, MaxSpeed = 12.5f, Density = 0.65f, Hard = true, MapPosition = new Vector2(-15f, 64f) },
            new TimetravelerLevel { Key = "sea_5", Title = "2-5", Era = 1, Order = 9, LengthMeters = 480, Seed = 205, Tier = 3, StartSpeed = 9.5f, MaxSpeed = 13f, Density = 0.6f, Hard = true, MapPosition = new Vector2(-3f, 71f) },
        };

        // The Renaissance: workshops full of machines (blades, crushers, gear rows).
        public static readonly TimetravelerLevel[] Era3 =
        {
            new TimetravelerLevel { Key = "ren_1", Title = "3-1", Era = 2, Order = 10, LengthMeters = 340, Seed = 301, Tier = 3, StartSpeed = 8.5f, MaxSpeed = 11.5f, Density = 0.75f, MapPosition = new Vector2(10f, 84f) },
            new TimetravelerLevel { Key = "ren_2", Title = "3-2", Era = 2, Order = 11, LengthMeters = 380, Seed = 302, Tier = 3, StartSpeed = 9f, MaxSpeed = 12f, Density = 0.7f, Hard = true, MapPosition = new Vector2(-2f, 89f) },
            new TimetravelerLevel { Key = "ren_3", Title = "3-3", Era = 2, Order = 12, LengthMeters = 420, Seed = 303, Tier = 3, StartSpeed = 9f, MaxSpeed = 12.5f, Density = 0.65f, Hard = true, MapPosition = new Vector2(-13f, 94f) },
            new TimetravelerLevel { Key = "ren_4", Title = "3-4", Era = 2, Order = 13, LengthMeters = 480, Seed = 304, Tier = 3, StartSpeed = 9.5f, MaxSpeed = 13f, Density = 0.6f, Hard = true, MapPosition = new Vector2(-14f, 104f) },
            new TimetravelerLevel { Key = "ren_5", Title = "3-5", Era = 2, Order = 14, LengthMeters = 550, Seed = 305, Tier = 3, StartSpeed = 10f, MaxSpeed = 13.5f, Density = 0.55f, Hard = true, MapPosition = new Vector2(0f, 111f) },
        };

        /// <summary>Every story level in play order (Order = index).</summary>
        public static readonly TimetravelerLevel[] All = Era1.Concat(Era2).Concat(Era3).ToArray();

        /// <summary>The endless-run node on the map (after the last level of the first timeline).</summary>
        public static readonly Vector2 EndlessMapPosition = new Vector2(-12f, 34f);

        /// <summary>Spots for later systems (diary NPCs, shop, minigames). Move or add freely.</summary>
        public static readonly TimetravelerPlace[] Places =
        {
            new TimetravelerPlace { Kind = PlaceKind.Npc, Name = "Companion", MapPosition = new Vector2(-6f, 1f) },
            new TimetravelerPlace { Kind = PlaceKind.Shop, Name = "Shop", MapPosition = new Vector2(-16f, 17f) },
            new TimetravelerPlace { Kind = PlaceKind.Npc, Name = "Cave dweller", MapPosition = new Vector2(5f, 18f) },
            new TimetravelerPlace { Kind = PlaceKind.Minigame, Name = "Egg hunt", MapPosition = new Vector2(18f, 33f) },
            new TimetravelerPlace { Kind = PlaceKind.Npc, Name = "Sailor", MapPosition = new Vector2(-14f, 45f) },
            new TimetravelerPlace { Kind = PlaceKind.Shop, Name = "Harbour", MapPosition = new Vector2(12f, 58f) },
            new TimetravelerPlace { Kind = PlaceKind.Npc, Name = "Inventor", MapPosition = new Vector2(14f, 96f) },
            new TimetravelerPlace { Kind = PlaceKind.Minigame, Name = "Painting", MapPosition = new Vector2(-4f, 100f) },
        };

        public static readonly TimetravelerEra[] Eras =
        {
            new TimetravelerEra { Name = "Age of Dinosaurs", MapBottom = 0f, Levels = Era1,
                Sky = new Color(0.16f, 0.13f, 0.1f), Hills = new Color(0.30f, 0.22f, 0.18f) },
            new TimetravelerEra { Name = "The Age of Exploration", MapBottom = EraHeight, Levels = Era2,
                Sky = new Color(0.12f, 0.16f, 0.2f), Hills = new Color(0.16f, 0.27f, 0.32f) },
            new TimetravelerEra { Name = "Renaissance", MapBottom = EraHeight * 2f, Levels = Era3,
                Sky = new Color(0.18f, 0.12f, 0.14f), Hills = new Color(0.36f, 0.22f, 0.2f) },
        };
    }
}
