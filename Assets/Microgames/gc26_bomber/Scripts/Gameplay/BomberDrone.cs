using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Bomber
{
    /// <summary>Weapon numbers (prototype values converted from px to units: 32 px = 1 unit).</summary>
    [System.Serializable]
    public struct BomberWeaponStats
    {
        public float Damage, Rate, Heat, Speed, Spread;
        public static BomberWeaponStats Blaster => new BomberWeaponStats { Damage = 10f, Rate = 4f, Heat = 4f, Speed = 22.5f, Spread = 0.02f };
    }

    /// <summary>
    /// The player's repair drone: WASD movement, dash (Space), mouse aim, blaster on LMB,
    /// charged shot on RMB, Vent on R. Heat comes from the shared HeatModel in the context.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BomberDrone : MonoBehaviour
    {
        const float Speed = 6f, DashSpeed = 26.7f, DashTime = 0.15f, DashCooldown = 0.8f;
        const float VentAmount = 50f, VentCooldown = 8f, Radius = 0.28f;

        public BomberWeaponStats Weapon = BomberWeaponStats.Blaster;
        public float Aim { get; private set; }
        public float VentCd { get; private set; }
        public float DashCd { get; private set; }
        public Collider2D Body { get; private set; }

        BomberContext _ctx;
        Rigidbody2D _rb;
        SpriteRenderer _hull;
        Transform _gun;
        readonly Transform[] _rotors = new Transform[4];
        Vector2 _move, _dashDir;
        float _dashT, _fireCd, _altCd, _time;

        public static BomberDrone Create(BomberContext ctx, Vector2 pos)
        {
            var go = ctx.Spawn("gc26_bomber_Drone");
            go.transform.position = pos;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Radius;
            var d = go.AddComponent<BomberDrone>();
            d._ctx = ctx; d._rb = rb; d.Body = col;
            d.BuildVisual();
            return d;
        }

        void BuildVisual()
        {
            _gun = _ctx.Spawn("Gun", transform).transform;
            var g = _gun.gameObject.AddComponent<SpriteRenderer>(); g.sprite = _ctx.Art.Gun; g.sortingOrder = 9;
            _hull = _ctx.Spawn("Hull", transform).AddComponent<SpriteRenderer>(); _hull.sprite = _ctx.Art.Drone; _hull.sortingOrder = 10;
            var hub = 5.5f / BomberArt.PixelsPerUnit;
            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
            for (int i = 0; i < 4; i++)
            {
                var r = _ctx.Spawn("Rotor", transform).AddComponent<SpriteRenderer>();
                r.sprite = _ctx.Art.Rotor; r.sortingOrder = 11; r.color = new Color(1f, 1f, 1f, 0.85f);
                r.transform.localPosition = corners[i] * hub;
                _rotors[i] = r.transform;
            }
        }

        void Update()
        {
            if (_ctx.Paused) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            float dt = Time.deltaTime; _time += dt;
            var core = _ctx.Core;
            core.Tick(dt);

            // input
            _move = Vector2.zero;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) _move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) _move.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) _move.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) _move.y += 1f;
            }
            if (_move.sqrMagnitude > 1f) _move.Normalize();
            if (mouse != null)
            {
                var m = _ctx.MouseWorld(mouse.position.ReadValue());
                Aim = Mathf.Atan2(m.y - transform.position.y, m.x - transform.position.x);
            }

            DashCd -= dt; VentCd -= dt; _fireCd -= dt; _altCd -= dt;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && DashCd <= 0f && core.CanFire)
            {
                _dashDir = _move.sqrMagnitude > 0.01f ? _move : new Vector2(Mathf.Cos(Aim), Mathf.Sin(Aim));
                _dashT = DashTime; DashCd = DashCooldown;
            }
            if (kb != null && kb.rKey.wasPressedThisFrame && VentCd <= 0f && core.CanFire)
            {
                core.Vent(VentAmount); VentCd = VentCooldown; _ctx.Shake(0.6f);
            }

            // fire
            if (mouse != null && core.CanFire)
            {
                if (mouse.leftButton.isPressed && _fireCd <= 0f) { Shoot(1f, false); core.AddHeat(Weapon.Heat); _fireCd = 1f / Weapon.Rate; }
                if (mouse.rightButton.wasPressedThisFrame && _altCd <= 0f) { Shoot(3f, true); core.AddHeat(Weapon.Heat * 3f); _altCd = 0.8f; _ctx.Shake(0.5f); }
            }

            UpdateVisual();
        }

        void FixedUpdate()
        {
            if (_dashT > 0f) { _dashT -= Time.fixedDeltaTime; _rb.linearVelocity = _dashDir * DashSpeed; return; }
            var speed = Speed * (_ctx.Core.CanFire ? 1f : 0.55f);
            var k = 1f - Mathf.Exp(-18f * Time.fixedDeltaTime);
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, _move * speed, k);
        }

        void Shoot(float damageMul, bool heavy)
        {
            var dir = Aim + Random.Range(-Weapon.Spread, Weapon.Spread);
            var muzzle = (Vector2)transform.position + new Vector2(Mathf.Cos(Aim), Mathf.Sin(Aim)) * 0.45f;
            _ctx.Projectiles.Fire(muzzle, dir, Weapon.Speed * (heavy ? 1.1f : 1f), Weapon.Damage * damageMul * _ctx.Core.DamageMultiplier, heavy);
        }

        void UpdateVisual()
        {
            var core = _ctx.Core;
            var step = Mathf.PI / 8f; // 16 directions keep pixel art crisp
            _gun.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(Aim / step) * step * Mathf.Rad2Deg);
            var spin = core.CanFire ? _time * 38f : 0f;
            for (int i = 0; i < 4; i++) _rotors[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Round((spin + i * 1.3f) / (Mathf.PI / 4f)) * 45f);
            _hull.color = core.State == CoreState.Overheated ? new Color(0.55f, 0.6f, 0.7f) : core.State == CoreState.Overdrive ? new Color(1f, 0.92f, 0.75f) : Color.white;
        }
    }
}
