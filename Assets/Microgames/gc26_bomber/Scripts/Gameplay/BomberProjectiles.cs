using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// All projectiles in one system (TDD 6.5): no rigidbodies or colliders on bullets.
    /// Each frame a bullet is moved by a linecast from its previous position, so it cannot skip thin walls.
    /// </summary>
    public sealed class BomberProjectiles : MonoBehaviour
    {
        const float Life = 1.4f;

        struct Shot { public Vector2 Pos, Vel; public float Damage, Life; public SpriteRenderer View; }

        BomberContext _ctx;
        readonly List<Shot> _shots = new List<Shot>();
        readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        readonly Dictionary<Collider2D, BomberTargetDummy> _dummyCache = new Dictionary<Collider2D, BomberTargetDummy>();
        ContactFilter2D _filter;

        public int Count => _shots.Count;

        public void Init(BomberContext ctx)
        {
            _ctx = ctx;
            _filter = new ContactFilter2D { useTriggers = false };
        }

        public void Fire(Vector2 from, float angle, float speed, float damage, bool heavy)
        {
            var view = _pool.Count > 0 ? _pool.Pop() : CreateView();
            view.gameObject.SetActive(true);
            view.sprite = heavy ? _ctx.Art.BulletHeavy : _ctx.Art.Bullet;
            view.transform.position = from;
            _shots.Add(new Shot { Pos = from, Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Damage = damage, Life = Life, View = view });
        }

        SpriteRenderer CreateView()
        {
            var sr = _ctx.Spawn("Shot", transform).AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;
            return sr;
        }

        void Update()
        {
            if (_ctx.Paused) return;
            float dt = Time.deltaTime;
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.Life -= dt;
                var next = s.Pos + s.Vel * dt;
                bool dead = s.Life <= 0f;
                if (!dead)
                {
                    int n = Physics2D.Linecast(s.Pos, next, _filter, _hits);
                    float best = float.MaxValue; Collider2D hitCol = null; Vector2 hitPoint = next;
                    for (int k = 0; k < n; k++)
                    {
                        var c = _hits[k].collider;
                        if (c == _ctx.Drone.Body) continue;
                        if (_hits[k].distance < best) { best = _hits[k].distance; hitCol = c; hitPoint = _hits[k].point; }
                    }
                    if (hitCol)
                    {
                        dead = true; next = hitPoint;
                        var dummy = DummyOf(hitCol);
                        if (dummy) dummy.Hit(s.Damage);
                    }
                }
                if (dead) { s.View.gameObject.SetActive(false); _pool.Push(s.View); _shots.RemoveAt(i); continue; }
                s.Pos = next; s.View.transform.position = next;
                _shots[i] = s;
            }
        }

        BomberTargetDummy DummyOf(Collider2D c)
        {
            if (!_dummyCache.TryGetValue(c, out var d)) { d = c.GetComponent<BomberTargetDummy>(); _dummyCache[c] = d; }
            return d;
        }
    }
}
