using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Plays the waves: when LevelFlow starts wave N, sends the enemies of waves[N-1] on their schedule,
    /// registers each one with LevelFlow, and calls FinishSpawning after the last one.
    /// Enemy HP grows with the wave number: x (1 + hpPerWave x (wave - 1)), as in the prototype.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] LevelFlow level;
        [SerializeField] EnemyPath path;
        [Tooltip("In play order. The count should match Total Waves on LevelFlow.")]
        [SerializeField] WaveData[] waves;
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

        Sprite _circle;
        Texture2D _circleTexture;

        public IReadOnlyList<EnemyUnit> Alive => _alive;

        void OnEnable()
        {
            if (level == null) return;
            level.WaveStarted += OnWaveStarted;
            level.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            if (level == null) return;
            level.WaveStarted -= OnWaveStarted;
            level.StateChanged -= OnStateChanged;
        }

        void Start()
        {
            if (level != null && waves != null && waves.Length != level.TotalWaves)
                Debug.LogWarning($"[{TowerdefenceGame.Id}] WaveSpawner has {waves.Length} waves, LevelFlow expects {level.TotalWaves}", this);
        }

        void OnDestroy()
        {
            if (_circle != null) Destroy(_circle);
            if (_circleTexture != null) Destroy(_circleTexture);
        }

        void OnWaveStarted(int wave)
        {
            _schedule.Clear();
            _next = 0;
            _waveTime = 0f;
            _wave = wave;
            _running = false;

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
            if (debugDamagePerSecond > 0f)
                foreach (var unit in _alive.ToArray())
                    unit.TakeDamage(debugDamagePerSecond * Time.deltaTime);
        }

        void Spawn(EnemyData enemy, float hpMultiplier)
        {
            var unit = EnemyUnit.Spawn(enemy, path, transform, hpMultiplier, Circle());
            level.RegisterEnemy(unit);
            _alive.Add(unit);
        }

        /// <summary>A white circle sprite, 1 unit wide, for enemies that have no prefab yet.</summary>
        Sprite Circle()
        {
            if (_circle != null) return _circle;

            const int size = 64;
            _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
            _circleTexture.SetPixels32(pixels);
            _circleTexture.Apply();
            _circle = Sprite.Create(_circleTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _circle;
        }
    }
}
