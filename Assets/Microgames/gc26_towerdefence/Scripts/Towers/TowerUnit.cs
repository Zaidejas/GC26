using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// A built tower: finds the enemy furthest along the path within range ("First") and attacks it
    /// according to TowerData.attack. Upgrades, selling and other target modes are later tasks.
    /// </summary>
    public class TowerUnit : MonoBehaviour
    {
        public TowerData Data { get; private set; }
        public int SlotIndex { get; private set; }

        WaveSpawner _enemies;
        PlaceholderArt _art;
        Transform _effects;
        float _cooldown;

        public static TowerUnit Build(TowerData data, int slot, Vector3 position, Transform parent,
            WaveSpawner enemies, PlaceholderArt art, Transform effects)
        {
            var body = art.NewShape(data.displayName, art.Square, parent, position, 0.8f, data.color, 5);
            var tower = body.gameObject.AddComponent<TowerUnit>();
            tower.Data = data;
            tower.SlotIndex = slot;
            tower._enemies = enemies;
            tower._art = art;
            tower._effects = effects;

            // Letter on top, so towers can be told apart before there is art.
            var label = new GameObject("Letter").AddComponent<TextMeshPro>();
            label.transform.SetParent(body.transform, false);
            label.text = data.letter;
            label.enableAutoSizing = true; // fit the letter inside the square whatever the font
            label.fontSizeMin = 0.5f;
            label.fontSizeMax = 8f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(0.75f, 0.75f);
            label.GetComponent<MeshRenderer>().sortingOrder = 6;
            return tower;
        }

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
            float bestProgress = float.MinValue;
            float range2 = Data.range * Data.range;
            foreach (var e in _enemies.Alive)
            {
                if (e == null || !e.IsAlive) continue;
                if (e.Data.flying && !Data.hitsAir) continue;
                if ((e.transform.position - transform.position).sqrMagnitude > range2) continue;
                if (e.Progress > bestProgress)
                {
                    bestProgress = e.Progress;
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
                    TowerProjectile.Launch(Data, transform.position, target, _enemies, _art, _effects);
                    break;

                case TowerAttack.Hitscan:
                    _art.FlashLine(transform.position, target.transform.position, new Color(0.88f, 0.75f, 0.9f), 0.06f, 0.1f, _effects);
                    target.TakeDamage(Data.damage, Data.ignoresArmor);
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
                        e.TakeDamage(Data.damage, Data.ignoresArmor);
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
