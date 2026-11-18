using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Flies to its target (following it while it lives, otherwise to its last position) and hits on arrival:
    /// single-target damage plus optional slow, or splash damage to every ground enemy around the impact.
    /// The PoolManager task (W2) will reuse these instead of creating and destroying them.
    /// </summary>
    public class TowerProjectile : MonoBehaviour
    {
        /// <summary>Tiles per second (prototype CONFIG.projectileSpeed).</summary>
        const float Speed = 9f;

        TowerData _data;
        EnemyUnit _target;
        Vector3 _aim;
        WaveSpawner _enemies;
        PlaceholderArt _art;
        Transform _effects;

        public static void Launch(TowerData data, Vector3 from, EnemyUnit target, WaveSpawner enemies, PlaceholderArt art, Transform effects)
        {
            float size = data.splashRadius > 0f ? 0.22f : 0.14f;
            var sr = art.NewShape("Projectile", art.Circle, effects, from, size, Color.Lerp(data.color, Color.white, 0.35f), 30);
            var p = sr.gameObject.AddComponent<TowerProjectile>();
            p._data = data;
            p._target = target;
            p._aim = target.transform.position;
            p._enemies = enemies;
            p._art = art;
            p._effects = effects;
        }

        void Update()
        {
            if (_target != null && _target.IsAlive) _aim = _target.transform.position;

            Vector3 d = _aim - transform.position;
            float step = Speed * Time.deltaTime;
            if (d.magnitude <= step)
            {
                transform.position = _aim;
                Impact();
                Destroy(gameObject);
            }
            else
            {
                transform.position += d.normalized * step;
            }
        }

        void Impact()
        {
            if (_data.splashRadius > 0f)
            {
                _art.FlashRing(_aim, _data.splashRadius, new Color(1f, 0.67f, 0.25f, 0.9f), 0.2f, _effects);
                float r2 = _data.splashRadius * _data.splashRadius;
                // Copy: a kill removes the enemy from the spawner's list.
                foreach (var e in new System.Collections.Generic.List<EnemyUnit>(_enemies.Alive))
                {
                    if (e == null || !e.IsAlive || e.Data.flying) continue;
                    if ((e.transform.position - _aim).sqrMagnitude <= r2)
                        e.TakeDamage(_data.damage, _data.ignoresArmor);
                }
                return;
            }

            if (_target == null || !_target.IsAlive) return;
            _target.TakeDamage(_data.damage, _data.ignoresArmor);
            if (_data.slowAmount > 0f && _target != null && _target.IsAlive)
                _target.ApplySlow(_data.slowAmount, _data.slowDuration);
        }
    }
}
