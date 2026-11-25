using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Plays the waves: when LevelFlow starts wave N, sends the enemies of LevelData.waves[N-1] on their schedule,
    /// registers each one with LevelFlow, and calls FinishSpawning after the last one.
    /// Enemy HP grows with the wave number: x (1 + hpPerWave x (wave - 1)), as in the prototype.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] LevelFlow level;
        [SerializeField] EnemyPath path;
        [SerializeField] PlaceholderArt art;
        [Tooltip("Enemy HP x (1 + this x (wave - 1)). Prototype: 0.08.")]
        [SerializeField, Min(0f)] float hpPerWave = 0.08f;

        [Header("Testing (until towers exist)")]
        [Tooltip("Damage per second dealt to every enemy, to test kills and gold. Keep 0 in the real game.")]
        [SerializeField, Min(0f)] float debugDamagePerSecond;

        struct PendingSpawn
        {
            public float time;
            public EnemyData enemy;
            public float hpMultiplier;
        }

        readonly List<PendingSpawn> _schedule = new();
        readonly List<EnemyUnit> _alive = new();
        int _next;
        float _waveTime;
        int _wave;
        bool _running;

        public IReadOnlyList<EnemyUnit> Alive => _alive;

        void OnEnable()
        {
            if (level == null) return;
            level.WaveStarted += OnWaveStarted;
            level.StateChanged += OnStateChanged;
            level.LevelBegun += ClearEnemies;
            level.LevelClosed += ClearEnemies;
        }

        void OnDisable()
        {
            if (level == null) return;
            level.WaveStarted -= OnWaveStarted;
            level.StateChanged -= OnStateChanged;
            level.LevelBegun -= ClearEnemies;
            level.LevelClosed -= ClearEnemies;
        }

        void OnWaveStarted(int wave)
        {
            _schedule.Clear();
            _next = 0;
            _waveTime = 0f;
            _wave = wave;
            _running = false;

            var waves = level.Data != null ? level.Data.waves : null;
            var data = waves != null && wave - 1 < waves.Length ? waves[wave - 1] : null;
            if (path == null || data == null)
            {
                Debug.LogError($"[{TowerdefenceGame.Id}] WaveSpawner: no path or no data for wave {wave}", this);
                level.FinishSpawning();
                return;
            }

            foreach (var g in data.groups)
            {
                if (g.enemy == null) continue;
                for (int i = 0; i < g.count; i++)
                    _schedule.Add(new PendingSpawn { time = g.startTime + g.interval * i, enemy = g.enemy, hpMultiplier = g.hpMultiplier });
            }
            _schedule.Sort((a, b) => a.time.CompareTo(b.time));
            _running = true;
        }

        /// <summary>Restart or back to the menu: stop sending and remove every enemy on the field.</summary>
        void ClearEnemies()
        {
            _running = false;
            foreach (var unit in _alive)
                if (unit != null) Destroy(unit.gameObject);
            _alive.Clear();
        }

        // Restart or game over: stop sending the rest of the wave.
        void OnStateChanged(LevelState state)
        {
            if (state != LevelState.Wave) _running = false;
        }

        void Update()
        {
            if (_running)
            {
                _waveTime += Time.deltaTime;
                float waveScale = 1f + hpPerWave * (_wave - 1);
                while (_next < _schedule.Count && _schedule[_next].time <= _waveTime)
                {
                    var s = _schedule[_next++];
                    Spawn(s.enemy, waveScale * s.hpMultiplier);
                    if (level.IsOver) { _running = false; break; }
                }
                if (_running && _next >= _schedule.Count)
                {
                    _running = false;
                    level.FinishSpawning();
                }
            }

            _alive.RemoveAll(u => u == null || !u.IsAlive);
            HealAroundHealers();
            if (debugDamagePerSecond > 0f)
                foreach (var unit in _alive.ToArray())
                    unit.TakeDamage(debugDamagePerSecond * Time.deltaTime);
        }

        void Spawn(EnemyData enemy, float hpMultiplier)
        {
            var unit = EnemyUnit.Spawn(enemy, path, transform, hpMultiplier, art);
            level.RegisterEnemy(unit);
            _alive.Add(unit);

            // Healers (Shaman) show their healing radius as a faint green ring.
            if (enemy.healPerSecond > 0f && enemy.healRadius > 0f && enemy.prefab == null)
            {
                var ring = art.NewShape("HealRadius", art.Ring, unit.transform, unit.transform.position, 1f,
                    new Color(0.4f, 0.9f, 0.6f, 0.45f), 9);
                ring.transform.localScale = Vector3.one * (enemy.healRadius * 2f / unit.transform.localScale.x);
            }
        }

        /// <summary>
        /// Every living healer restores healPerSecond HP per second to every other enemy within its healRadius
        /// (not to itself), as in the prototype. Several healers stack.
        /// </summary>
        void HealAroundHealers()
        {
            foreach (var healer in _alive)
            {
                var d = healer.Data;
                if (d.healPerSecond <= 0f || !healer.IsAlive) continue;
                float r2 = d.healRadius * d.healRadius;
                float amount = d.healPerSecond * Time.deltaTime;
                foreach (var e in _alive)
                {
                    if (e == healer || !e.IsAlive) continue;
                    if ((e.transform.position - healer.transform.position).sqrMagnitude <= r2)
                        e.Heal(amount);
                }
            }
        }
    }
}
