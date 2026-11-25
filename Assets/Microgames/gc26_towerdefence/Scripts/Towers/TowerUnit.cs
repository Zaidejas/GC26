using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>Which enemy in range a tower shoots.</summary>
    public enum TargetMode
    {
        /// <summary>Furthest along its path (closest to the castle). Default.</summary>
        First,
        /// <summary>Least far along its path.</summary>
        Last,
        /// <summary>Most HP + shield.</summary>
        Strongest,
        /// <summary>Nearest to the tower.</summary>
        Closest,
    }

    /// <summary>
    /// A built tower: picks a target in range by its TargetMode and attacks it according to TowerData.attack.
    /// Three levels: each upgrade gives +40% damage and +10% range and costs 75%, then 100% of the base price.
    /// Selling refunds 70% of all gold spent on it. Numbers from the prototype CONFIG and GDD section 5.
    /// </summary>
    public class TowerUnit : MonoBehaviour
    {
        public const int MaxLevel = 2; // 0 = base, then 2 upgrades
        const float UpgradeDamage = 1.4f;
        const float UpgradeRange = 1.1f;
        static readonly float[] UpgradeCostShare = { 0.75f, 1f };
        const float SellRefund = 0.7f;

        public TowerData Data { get; private set; }
        public int SlotIndex { get; private set; }
        public int Level { get; private set; }
        /// <summary>All gold paid for this tower (build + upgrades).</summary>
        public int Spent { get; private set; }
        public TargetMode Mode { get; private set; } = TargetMode.First;

        public float Damage => DamageAt(Level);
        public float Range => RangeAt(Level);
        public float DamageAt(int level) => Data.damage * Mathf.Pow(UpgradeDamage, level);
        public float RangeAt(int level) => Data.range * Mathf.Pow(UpgradeRange, level);

        public bool CanUpgrade => Level < MaxLevel;
        /// <summary>Price of the next upgrade, or 0 at max level.</summary>
        public int UpgradeCost => CanUpgrade ? Mathf.RoundToInt(Data.cost * UpgradeCostShare[Level]) : 0;
        public int SellValue => Mathf.FloorToInt(Spent * SellRefund);

        WaveSpawner _enemies;
        PlaceholderArt _art;
        Transform _effects;
        PoolManager _pool;
        TextMeshPro _label;
        float _cooldown;

        public static TowerUnit Build(TowerData data, int slot, Vector3 position, Transform parent,
            WaveSpawner enemies, PlaceholderArt art, Transform effects, PoolManager pool)
        {
            var body = art.NewShape(data.displayName, art.Square, parent, position, 0.8f, data.color, 5);
            var tower = body.gameObject.AddComponent<TowerUnit>();
            tower.Data = data;
            tower.SlotIndex = slot;
            tower.Spent = data.cost;
            tower._enemies = enemies;
            tower._art = art;
            tower._effects = effects;
            tower._pool = pool;

            // Letter on top, so towers can be told apart before there is art. "+" marks each upgrade.
            var label = new GameObject("Letter").AddComponent<TextMeshPro>();
            label.transform.SetParent(body.transform, false);
            label.enableAutoSizing = true; // fit the text inside the square whatever the font
            label.fontSizeMin = 0.5f;
            label.fontSizeMax = 8f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(0.75f, 0.75f);
            label.GetComponent<MeshRenderer>().sortingOrder = 6;
            tower._label = label;
            tower.RefreshLabel();
            return tower;
        }

        /// <summary>Raises the level. The caller has already taken UpgradeCost from the player.</summary>
        public void Upgrade(int pricePaid)
        {
            if (!CanUpgrade) return;
            Level++;
            Spent += pricePaid;
            RefreshLabel();
        }

        public void CycleMode()
        {
            Mode = (TargetMode)(((int)Mode + 1) % System.Enum.GetValues(typeof(TargetMode)).Length);
        }

        void RefreshLabel() => _label.text = Data.letter + new string('+', Level);

        void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            var target = PickTarget();
            if (target == null) return;
            _cooldown = 1f / Data.fireRate;
            Fire(target);
        }

        EnemyUnit PickTarget()
        {
            EnemyUnit best = null;
            float bestScore = float.MinValue;
            float range2 = Range * Range;
            foreach (var e in _enemies.Alive)
            {
                if (e == null || !e.IsAlive) continue;
                if (e.Data.flying && !Data.hitsAir) continue;
                float d2 = (e.transform.position - transform.position).sqrMagnitude;
                if (d2 > range2) continue;

                float score = Mode switch
                {
                    TargetMode.First => e.Progress,
                    TargetMode.Last => -e.Progress,
                    TargetMode.Strongest => e.Hp + e.Shield,
                    _ => -d2, // Closest
                };
                if (score > bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        void Fire(EnemyUnit target)
        {
            switch (Data.attack)
            {
                case TowerAttack.Projectile:
                    TowerProjectile.Launch(Data, Damage, transform.position, target, _enemies, _art, _effects, _pool);
                    break;

                case TowerAttack.Hitscan:
                    _art.FlashLine(transform.position, target.transform.position, new Color(0.88f, 0.75f, 0.9f), 0.06f, 0.1f, _effects);
                    target.TakeDamage(Damage, Data.ignoresArmor);
                    break;

                case TowerAttack.Chain:
                    var hit = ChainTargets(target);
                    var from = transform.position;
                    foreach (var e in hit)
                    {
                        _art.FlashLine(from, e.transform.position, new Color(1f, 0.95f, 0.46f), 0.07f, 0.12f, _effects);
                        from = e.transform.position;
                    }
                    foreach (var e in hit)
                        e.TakeDamage(Damage, Data.ignoresArmor);
                    break;
            }
        }

        /// <summary>The target, then each time the nearest not-yet-hit enemy within the jump range.</summary>
        List<EnemyUnit> ChainTargets(EnemyUnit first)
        {
            var hit = new List<EnemyUnit> { first };
            var last = first;
            float jump2 = Data.chainJumpRange * Data.chainJumpRange;
            while (hit.Count < Data.chainCount)
            {
                EnemyUnit next = null;
                float nearest = jump2;
                foreach (var e in _enemies.Alive)
                {
                    if (e == null || !e.IsAlive || hit.Contains(e)) continue;
                    float d2 = (e.transform.position - last.transform.position).sqrMagnitude;
                    if (d2 <= nearest)
                    {
                        nearest = d2;
                        next = e;
                    }
                }
                if (next == null) break;
                hit.Add(next);
                last = next;
            }
            return hit;
        }
    }
}
