using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Moves a hazard so timing matters (and time slow helps). Uses game time, so it slows down too.
    /// Bob: up and down between two heights (pendulum-like blade).
    /// Fly: waits until the traveler is close, then flies left toward him (pterodactyl).
    /// Drop: waits until the traveler is close, flashes, then falls (boulder).
    /// Crush: a block that slams down to the ground and slowly rises again, on a loop.
    /// Swoop: waits high up, then flies left and dives down to head height in <c>period</c> seconds (pterodactyl).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class RunnerMover : MonoBehaviour
    {
        public enum Mode { Bob, Fly, Drop, Crush, Swoop }

        public Mode mode;
        public float lowY;
        public float highY;
        public float period = 1.6f;
        public float speed = 6f;
        public float triggerDistance = 14f;
        public float phase;

        Rigidbody2D _body;
        Transform _target;
        SpriteRenderer _renderer;
        Color _baseColor;
        float _time;
        bool _triggered;
        float _fallSpeed;
        float _warnTimer;
        float _swoopTime;

        const float DropWarnTime = 0.35f;
        const float DropGravity = 60f;

        public void Init(Transform target)
        {
            _target = target;
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _renderer = TimetravelerArt.ShownRenderer(gameObject);
            if (_renderer != null) _baseColor = _renderer.color;
            _time = phase * period;
        }

        void FixedUpdate()
        {
            if (_body == null) return;
            var dt = Time.fixedDeltaTime;
            var pos = _body.position;
            _time += dt;

            switch (mode)
            {
                case Mode.Bob:
                {
                    var t = 0.5f - 0.5f * Mathf.Cos(_time / period * Mathf.PI * 2f);
                    pos.y = Mathf.Lerp(lowY, highY, t);
                    break;
                }
                case Mode.Crush:
                {
                    // 25% of the loop slamming down, 15% resting, 60% rising.
                    var t = Mathf.Repeat(_time / period, 1f);
                    float k;
                    if (t < 0.25f) k = 1f - (t / 0.25f) * (t / 0.25f);
                    else if (t < 0.4f) k = 0f;
                    else k = (t - 0.4f) / 0.6f;
                    pos.y = Mathf.Lerp(lowY, highY, k);
                    break;
                }
                case Mode.Fly:
                    if (!_triggered && Near()) _triggered = true;
                    if (_triggered) pos.x -= speed * dt;
                    break;
                case Mode.Swoop:
                    if (!_triggered && Near())
                    {
                        _triggered = true;
                        _swoopTime = 0f;
                    }
                    if (_triggered)
                    {
                        _swoopTime += dt;
                        pos.x -= speed * dt;
                        var k = Mathf.Clamp01(_swoopTime / period);
                        pos.y = Mathf.Lerp(highY, lowY, 1f - (1f - k) * (1f - k));   // fast dive, gentle pull-out
                    }
                    break;
                case Mode.Drop:
                    if (!_triggered && Near())
                    {
                        _triggered = true;
                        _warnTimer = DropWarnTime;
                    }
                    if (_triggered)
                    {
                        if (_warnTimer > 0f)
                        {
                            _warnTimer -= dt;
                            Flash();
                        }
                        else
                        {
                            if (_renderer != null) _renderer.color = _baseColor;
                            _fallSpeed += DropGravity * dt;
                            pos.y = Mathf.Max(lowY, pos.y - _fallSpeed * dt);
                        }
                    }
                    break;
            }

            _body.MovePosition(pos);
        }

        bool Near() => _target != null && transform.position.x - _target.position.x < triggerDistance;

        void Flash()
        {
            if (_renderer == null) return;
            var on = Mathf.Repeat(_warnTimer, 0.1f) < 0.05f;
            var c = on ? Color.white : _baseColor;
            c.a = _renderer.color.a;
            _renderer.color = c;
        }
    }
}
