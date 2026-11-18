using System;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// A live enemy on the field: walks the path, takes damage, dies or reaches the castle.
    /// It only reports what happened (Died / ReachedCastle); gold and lives are handled by
    /// whoever listens (EconomyManager and LevelFlow, later tasks).
    /// </summary>
    public class EnemyUnit : MonoBehaviour
    {
        /// <summary>Killed by towers. Give Data.gold to the player.</summary>
        public event Action<EnemyUnit> Died;

        /// <summary>Got to the castle. Take Data.livesLost lives.</summary>
        public event Action<EnemyUnit> ReachedCastle;

        public EnemyData Data { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Shield { get; private set; }

        /// <summary>Distance walked along the path, in tiles. Larger = closer to the castle ("First" targeting).</summary>
        public float Distance { get; private set; }

        /// <summary>Distance still to go before the castle.</summary>
        public float DistanceLeft => _line == null ? 0f : _line.Length - Distance;

        /// <summary>Share of its own path already walked, 0..1. Towers target the highest ("First").</summary>
        public float Progress => _line == null || _line.Length <= 0f ? 0f : Distance / _line.Length;

        public bool IsAlive { get; private set; }

        /// <summary>Multiplies speed; 1 = normal. Set by ApplySlow (Frost).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        PathLine _line;
        EnemyPath _path;
        float _slowUntil;
        float _healGlowUntil;
        SpriteRenderer _body;
        static readonly Color HealGlow = new(0.65f, 1f, 0.7f);

        /// <summary>
        /// Slows the enemy by `amount` (0.4 = 40% slower) for `seconds`. A new slow replaces the old one,
        /// as in the prototype. The StatusEffect task (W3) can grow this into burn/poison later.
        /// </summary>
        public void ApplySlow(float amount, float seconds)
        {
            if (!IsAlive) return;
            SpeedMultiplier = 1f - Mathf.Clamp01(amount);
            _slowUntil = Time.time + seconds;
        }

        /// <summary>
        /// Creates an enemy at the start of its path. Uses data.prefab if set, otherwise a colored circle.
        /// hpMultiplier covers per-wave scaling and the boss's reduced HP. Adds the HP (and shield) bar.
        /// </summary>
        public static EnemyUnit Spawn(EnemyData data, EnemyPath path, Transform parent, float hpMultiplier, PlaceholderArt art)
        {
            GameObject go;
            if (data.prefab != null)
            {
                go = Instantiate(data.prefab, parent);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art.Circle;
                sr.color = data.color;
                sr.sortingOrder = data.flying ? 20 : 10;
                // The placeholder sprite is 1 unit wide, size is a radius.
                go.transform.localScale = Vector3.one * (data.size * 2f);
            }

            go.name = data.displayName;
            var unit = go.GetComponent<EnemyUnit>();
            if (unit == null) unit = go.AddComponent<EnemyUnit>();
            unit.Init(data, path, hpMultiplier);
            unit._body = go.GetComponent<SpriteRenderer>(); // placeholder circle; null for a prefab
            EnemyHealthBar.Attach(unit, art);
            return unit;
        }

        public void Init(EnemyData data, EnemyPath path, float hpMultiplier = 1f)
        {
            Data = data;
            MaxHp = data.maxHp * hpMultiplier;
            Hp = MaxHp;
            Shield = data.shield;
            Distance = 0f;
            SpeedMultiplier = 1f;
            _slowUntil = 0f;
            _healGlowUntil = 0f;
            if (_path != null) _path.LayoutChanged -= OnLayoutChanged;
            _path = path;
            _path.LayoutChanged += OnLayoutChanged;
            _line = path.For(data.flying);
            transform.position = _line.Start;
            IsAlive = true;
        }

        void Update()
        {
            if (!IsAlive) return;

            if (_slowUntil > 0f && Time.time >= _slowUntil)
            {
                _slowUntil = 0f;
                SpeedMultiplier = 1f;
            }

            Distance += Data.speed * SpeedMultiplier * Time.deltaTime;
            if (Distance >= _line.Length)
            {
                transform.position = _line.End;
                Finish();
                ReachedCastle?.Invoke(this);
                return;
            }
            transform.position = _line.PositionAt(Distance);
        }

        /// <summary>Shield absorbs first, then armor reduces what is left (unless ignoreArmor, e.g. Sniper).</summary>
        public void TakeDamage(float amount, bool ignoreArmor = false)
        {
            if (!IsAlive || amount <= 0f) return;

            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
            }

            if (!ignoreArmor) amount *= 1f - Data.armor;

            Hp -= amount;
            if (Hp <= 0f)
            {
                Hp = 0f;
                Finish();
                Died?.Invoke(this);
            }
        }

        /// <summary>Restores HP up to the maximum (Shaman). A wounded enemy being healed glows green.</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f || Hp >= MaxHp) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            _healGlowUntil = Time.time + 0.15f;
        }

        /// <summary>True while a healer is restoring this enemy's HP (for the green glow).</summary>
        public bool IsBeingHealed => IsAlive && Time.time < _healGlowUntil;

        void LateUpdate()
        {
            if (_body == null) return;
            // Pulse towards light green while healed, back to the normal color otherwise.
            float pulse = IsBeingHealed ? 0.55f + 0.25f * Mathf.Sin(Time.time * 18f) : 0f;
            _body.color = Color.Lerp(Data.color, HealGlow, pulse);
        }

        /// <summary>The screen turned: same distance walked, on the turned line.</summary>
        void OnLayoutChanged()
        {
            _line = _path.For(Data.flying);
            transform.position = _line.PositionAt(Distance);
        }

        void OnDestroy()
        {
            if (_path != null) _path.LayoutChanged -= OnLayoutChanged;
        }

        void Finish()
        {
            IsAlive = false;
            // The PoolManager task will return it to a pool instead.
            Destroy(gameObject);
        }
    }
}
