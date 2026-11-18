using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// One wave: several groups that can overlap in time.
    /// Same shape as WAVES in Docs~/towerdefence_prototype/game.js: g(type, count, every, start, hpMul).
    /// </summary>
    [CreateAssetMenu(menuName = "GC26/Towerdefence/Wave", fileName = "NewWave")]
    public class WaveData : ScriptableObject
    {
        public List<SpawnGroup> groups = new();

        /// <summary>Seconds from the wave start until its last enemy is sent.</summary>
        public float SpawnDuration
        {
            get
            {
                float end = 0f;
                foreach (var g in groups)
                    if (g.enemy != null && g.count > 0)
                        end = Mathf.Max(end, g.startTime + g.interval * (g.count - 1));
                return end;
            }
        }
    }

    [Serializable]
    public class SpawnGroup
    {
        public EnemyData enemy;
        [Min(1)] public int count = 1;
        [Tooltip("Seconds between two enemies of this group.")]
        [Min(0.05f)] public float interval = 1f;
        [Tooltip("Seconds after the wave starts when the first enemy of this group appears.")]
        [Min(0f)] public float startTime;
        [Tooltip("Extra HP multiplier for this group (the prototype boss uses 0.35).")]
        [Min(0.01f)] public float hpMultiplier = 1f;
    }
}
