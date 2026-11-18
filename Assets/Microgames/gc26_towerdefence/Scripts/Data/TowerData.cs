using UnityEngine;

namespace GC26.Towerdefence
{
    public enum TowerAttack
    {
        /// <summary>Fires a projectile that flies to the target (Archer, Cannon, Frost).</summary>
        Projectile,
        /// <summary>Hits the target and jumps to nearby enemies (Tesla).</summary>
        Chain,
        /// <summary>Hits instantly (Sniper).</summary>
        Hitscan,
    }

    /// <summary>
    /// One tower type. Values come from TOWERS in Docs~/towerdefence_prototype/game.js.
    /// Distances are in tiles, fire rate in shots per second.
    /// </summary>
    [CreateAssetMenu(menuName = "GC26/Towerdefence/Tower", fileName = "NewTower")]
    public class TowerData : ScriptableObject
    {
        [Header("Name")]
        public string displayName = "Archer";
        [Tooltip("Letter drawn on the placeholder tower.")]
        public string letter = "A";

        [Header("Stats")]
        [Min(0)] public int cost = 100;
        [Min(0f)] public float damage = 10f;
        [Tooltip("Shots per second")]
        [Min(0.01f)] public float fireRate = 1f;
        [Tooltip("Tiles")]
        [Min(0.1f)] public float range = 3.5f;
        [Tooltip("Can shoot flying enemies (Bat).")]
        public bool hitsAir = true;

        [Header("Attack")]
        public TowerAttack attack = TowerAttack.Projectile;
        [Tooltip("Projectile only: damages every ground enemy within this radius of the impact (Cannon). 0 = single target.")]
        [Min(0f)] public float splashRadius;
        [Tooltip("Projectile only: speed is reduced by this share, 0..1 (Frost 0.4 = 40% slower).")]
        [Range(0f, 0.9f)] public float slowAmount;
        [Tooltip("Seconds the slow lasts.")]
        [Min(0f)] public float slowDuration;
        [Tooltip("Chain only: how many enemies one shot hits in total.")]
        [Min(1)] public int chainCount = 1;
        [Tooltip("Chain only: max distance of each jump, in tiles.")]
        [Min(0f)] public float chainJumpRange = 1.6f;
        [Tooltip("Damage ignores enemy armor (Sniper).")]
        public bool ignoresArmor;

        [Header("Look")]
        [Tooltip("Placeholder color until real art exists.")]
        public Color color = new(0.247f, 0.561f, 0.227f);
    }
}
