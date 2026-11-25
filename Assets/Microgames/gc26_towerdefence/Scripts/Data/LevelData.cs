using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Everything that makes one level different: the road, the build slots, the towers on offer,
    /// the waves, and the starting gold and lives. Game rules that are the same for every level
    /// (bonuses, stars, upgrades) stay in code. A new level = a copy of this asset with other values.
    /// Points use prototype map coordinates on the 16x9 field (x right, y down, tile centers at .5),
    /// like PATH and SLOTS in Docs~/towerdefence_prototype/game.js.
    /// </summary>
    [CreateAssetMenu(menuName = "GC26/Towerdefence/Level", fileName = "NewLevel")]
    public class LevelData : ScriptableObject
    {
        public string displayName = "Level 1";

        [Header("Start")]
        [Min(0)] public int startGold = 250;
        [Min(1)] public int startLives = 20;

        [Header("Map")]
        [Tooltip("Ground road, in map coordinates. The last point is the castle. Flyers go straight from the first to the last.")]
        public Vector2[] path =
        {
            new(-0.5f, 1.5f), new(3.5f, 1.5f), new(3.5f, 6.5f), new(7.5f, 6.5f),
            new(7.5f, 2.5f), new(11.5f, 2.5f), new(11.5f, 6.5f), new(15f, 6.5f),
        };

        [Tooltip("Build slots, in map coordinates.")]
        public Vector2[] slots =
        {
            new(1.5f, 3f), new(2f, 5.5f), new(5.5f, 3f), new(5.5f, 5f), new(5.5f, 8f), new(9.5f, 1f),
            new(9.5f, 4.5f), new(9.5f, 7.8f), new(13.5f, 1.5f), new(13.5f, 4.5f), new(13.5f, 8f),
        };

        [Header("Content")]
        [Tooltip("Towers offered in the build menu, in this order.")]
        public TowerData[] towers;

        [Tooltip("Waves in play order. The level is won after the last one.")]
        public WaveData[] waves;
    }
}
