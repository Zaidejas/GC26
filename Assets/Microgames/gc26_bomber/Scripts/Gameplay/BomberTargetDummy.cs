using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>Practice target for the first playable step: takes damage, flashes, breaks and respawns.</summary>
    public sealed class BomberTargetDummy : MonoBehaviour
    {
        const float MaxHp = 60f, RespawnTime = 3f;

        public float Hp { get; private set; } = MaxHp;
        BomberContext _ctx;
        SpriteRenderer _view;
        Collider2D _col;
        float _flash, _respawn;

        public static BomberTargetDummy Create(BomberContext ctx, Vector2 pos)
        {
            var go = ctx.Spawn("gc26_bomber_Dummy");
            go.transform.position = pos;
            var d = go.AddComponent<BomberTargetDummy>();
            d._ctx = ctx;
            d._view = go.AddComponent<SpriteRenderer>(); d._view.sprite = ctx.Art.Dummy; d._view.sortingOrder = 5;
            var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.4f; d._col = c;
            return d;
        }

        public void Hit(float damage)
        {
            if (Hp <= 0f) return;
            Hp -= damage; _flash = 0.08f;
            if (Hp <= 0f)
            {
                _ctx.Score += 10; _ctx.Shake(0.4f);
                _view.enabled = false; _col.enabled = false; _respawn = RespawnTime;
            }
        }

        void Update()
        {
            if (_ctx.Paused) return;
            if (_respawn > 0f)
            {
                _respawn -= Time.deltaTime;
                if (_respawn <= 0f) { Hp = MaxHp; _view.enabled = true; _col.enabled = true; }
                return;
            }
            _flash -= Time.deltaTime;
            _view.color = _flash > 0f ? new Color(1f, 0.45f, 0.45f) : Color.white;
        }
    }
}
