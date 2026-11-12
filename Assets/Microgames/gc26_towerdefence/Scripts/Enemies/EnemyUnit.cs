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

        public bool IsAlive { get; private set; }

        /// <summary>Multiplies speed; the StatusEffect task (Frost slow) will drive it. 1 = normal.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        PathLine _line;

        /// <summary>
        /// Creates an enemy at the start of its path. Uses data.prefab if set, otherwise a colored circle.
        /// hpMultiplier covers per-wave scaling and the boss's reduced HP.
        /// </summary>
        public static EnemyUnit Spawn(EnemyData data, EnemyPath path, Transform parent, float hpMultiplier, Sprite placeholder)
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
                sr.sprite = placeholder;
                sr.color = data.color;
                sr.sortingOrder = data.flying ? 20 : 10;
                // The placeholder sprite is 1 unit wide, size is a radius.
                go.transform.localScale = Vector3.one * (data.size * 2f);
            }

            go.name = data.displayName;
            var unit = go.GetComponent<EnemyUnit>();
            if (unit == null) unit = go.AddComponent<EnemyUnit>();
            unit.Init(data, path, hpMultiplier);
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
            _line = path.For(data.flying);
            transform.position = _line.Start;
            IsAlive = true;
        }

        void Update()
        {
            if (!IsAlive) return;

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

        /// <summary>Restores HP up to the maximum (Shaman).</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        void Finish()
        {
            IsAlive = false;
            // The PoolManager task will return it to a pool instead.
            Destroy(gameObject);
        }
    }
}
