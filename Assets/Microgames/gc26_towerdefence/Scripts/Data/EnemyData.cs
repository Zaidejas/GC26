using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// One enemy type. Values come from ENEMIES in Docs~/towerdefence_prototype/game.js.
    /// Distances are in tiles, speed in tiles per second.
    /// </summary>
    [CreateAssetMenu(menuName = "GC26/Towerdefence/Enemy", fileName = "NewEnemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Name")]
        public string displayName = "Grunt";

        [Header("Stats")]
        [Min(1f)] public float maxHp = 50f;
        [Tooltip("Tiles per second")]
        [Min(0f)] public float speed = 1f;
        [Tooltip("Share of damage blocked, 0..1 (0.3 = 30%). Sniper ignores it.")]
        [Range(0f, 0.9f)] public float armor;
        [Tooltip("Absorbs this much damage before HP is hit (boss).")]
        [Min(0f)] public float shield;
        [Tooltip("HP per second given to other enemies within the healing radius (Shaman).")]
        [Min(0f)] public float healPerSecond;
        [Tooltip("Flies straight to the castle; only towers that hit air can shoot it.")]
        public bool flying;

        [Header("Reward and cost")]
        [Min(0)] public int gold = 5;
        [Tooltip("Lives the castle loses when this enemy reaches it.")]
        [Min(0)] public int livesLost = 1;

        [Header("Look")]
        [Tooltip("Radius in tiles. Used for the placeholder circle and for hit checks.")]
        [Min(0.05f)] public float size = 0.26f;
        [Tooltip("Placeholder color until real art exists.")]
        public Color color = new(0.486f, 0.702f, 0.259f);
        [Tooltip("Optional. Leave empty to use a colored circle.")]
        public GameObject prefab;
    }
}
