using System;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The main character in the runner: auto-run, jump (hold for higher), double jump,
    /// slide, fast fall. Dies on hazards, on running into a wall, or on falling into a pit.
    /// Uses Rigidbody2D.gravityScale, never Physics2D.gravity (rule 6).
    /// Units: 16 px = 1 unit, so the 32x48 px sprite is 2 x 3 units.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class TravelerController : MonoBehaviour
    {
        [Serializable]
        public class Tuning
        {
            [Header("Size (units)")]
            public Vector2 standSize = new Vector2(2f, 3f);
            public Vector2 slideSize = new Vector2(2.6f, 1.4f);

            [Header("Jump")]
            [Tooltip("Height of a full (held) jump, in units")]
            public float jumpHeight = 6.5f;
            [Tooltip("Seconds to reach the top of a full jump")]
            public float jumpTimeToApex = 0.42f;
            public float doubleJumpHeight = 5f;
            [Tooltip("Upward speed is multiplied by this when the jump is released early")]
            public float jumpCutMultiplier = 0.45f;
            [Tooltip("Gravity multiplier while falling, for a snappier landing")]
            public float fallGravityMultiplier = 1.4f;
            public float maxFallSpeed = 35f;
            public float fastFallSpeed = 40f;
            [Tooltip("A jump pressed this long before landing still counts")]
            public float jumpBufferTime = 0.15f;
            [Tooltip("A jump pressed this long after leaving a ledge still counts")]
            public float coyoteTime = 0.12f;

            [Header("Forgiveness")]
            [Tooltip("Hitting a block this close to its top edge steps up onto it instead of dying")]
            public float stepUpAssist = 1.2f;

            [Header("Slide")]
            public float slideDuration = 0.7f;
        }

        public Tuning tuning = new Tuning();

        public event Action Died;

        public float RunSpeed { get; set; }
        public bool IsDead { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding => _slideTimer > 0f;

        Rigidbody2D _body;
        BoxCollider2D _collider;
        Transform _visual;
        TravelerInput _input;
        float _killY;

        float _gravity;      // positive, units/s^2
        float _jumpSpeed;
        float _doubleJumpSpeed;
        bool _canDoubleJump;
        float _jumpBufferTimer;
        float _coyoteTimer;
        float _slideTimer;
        bool _jumpCut;
        bool _fastFalling;

        readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

        public void Init(TravelerInput input, Transform visual, float killY)
        {
            _input = input;
            _visual = visual;
            _killY = killY;

            _body = GetComponent<Rigidbody2D>();
            _body.freezeRotation = true;
            _body.gravityScale = 0f; // gravity is applied in FixedUpdate from the jump tuning
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _collider = GetComponent<BoxCollider2D>();

            RecalculateJump();
            SetSize(tuning.standSize);
        }

        /// <summary>Derive gravity and jump speeds from height and time (easier to tune).</summary>
        public void RecalculateJump()
        {
            _gravity = 2f * tuning.jumpHeight / (tuning.jumpTimeToApex * tuning.jumpTimeToApex);
            _jumpSpeed = _gravity * tuning.jumpTimeToApex;
            _doubleJumpSpeed = Mathf.Sqrt(2f * _gravity * tuning.doubleJumpHeight);
        }

        public void ResetAt(Vector2 position, float runSpeed)
        {
            IsDead = false;
            RunSpeed = runSpeed;
            _slideTimer = 0f;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _canDoubleJump = true;
            SetSize(tuning.standSize);
            _body.simulated = true;
            _body.position = position + new Vector2(0f, tuning.standSize.y / 2f);
            transform.position = _body.position;
            _body.linearVelocity = Vector2.zero;
        }

        void Update()
        {
            if (IsDead || _input == null) return;

            if (_input.JumpPressed) _jumpBufferTimer = tuning.jumpBufferTime;
            if (_input.SlidePressed) StartSlideOrFastFall();
        }

        void FixedUpdate()
        {
            if (IsDead || _body == null) return;
            var dt = Time.fixedDeltaTime;

            IsGrounded = CheckGrounded();
            if (IsGrounded)
            {
                _coyoteTimer = tuning.coyoteTime;
                _canDoubleJump = true;
                _fastFalling = false;
            }
            else
            {
                _coyoteTimer -= dt;
            }

            var v = _body.linearVelocity;
            v.x = RunSpeed;

            // Jump (buffered) or double jump.
            if (_jumpBufferTimer > 0f)
            {
                if (_coyoteTimer > 0f)
                {
                    v.y = _jumpSpeed;
                    _coyoteTimer = 0f;
                    _jumpBufferTimer = 0f;
                    _jumpCut = false;
                    EndSlide();
                }
                else if (_canDoubleJump)
                {
                    v.y = _doubleJumpSpeed;
                    _canDoubleJump = false;
                    _jumpBufferTimer = 0f;
                    _jumpCut = false;
                }
            }
            _jumpBufferTimer -= dt;

            // Variable jump height: releasing early cuts the rise.
            if (!_input.JumpHeld && v.y > 0f && !_jumpCut)
            {
                v.y *= tuning.jumpCutMultiplier;
                _jumpCut = true;
            }

            // Custom gravity through the body's own velocity (Physics2D.gravity stays untouched).
            var g = _gravity * (v.y < 0f ? tuning.fallGravityMultiplier : 1f);
            v.y -= g * dt;
            v.y = Mathf.Max(v.y, -(_fastFalling ? tuning.fastFallSpeed : tuning.maxFallSpeed));

            _body.linearVelocity = v;

            if (_slideTimer > 0f)
            {
                _slideTimer -= dt;
                if (_slideTimer <= 0f) EndSlide();
            }

            if (_body.position.y < _killY) Die();
        }

        void StartSlideOrFastFall()
        {
            if (IsGrounded)
            {
                if (!IsSliding) SetSize(tuning.slideSize);
                _slideTimer = tuning.slideDuration;
            }
            else
            {
                var v = _body.linearVelocity;
                v.y = -tuning.fastFallSpeed;
                _body.linearVelocity = v;
                _fastFalling = true;
            }
        }

        void EndSlide()
        {
            if (!IsSliding && _collider.size == tuning.standSize) return;
            _slideTimer = 0f;
            SetSize(tuning.standSize);
        }

        /// <summary>Resize the collider and visual, keeping the feet on the ground.</summary>
        void SetSize(Vector2 size)
        {
            var oldHeight = _collider.size.y;
            _collider.size = size;
            if (_body != null && oldHeight > 0f)
            {
                var feetY = _body.position.y - oldHeight / 2f;
                _body.position = new Vector2(_body.position.x, feetY + size.y / 2f);
            }
            if (_visual != null) _visual.localScale = new Vector3(size.x, size.y, 1f);
        }

        bool CheckGrounded()
        {
            var filter = new ContactFilter2D { useTriggers = false };
            var count = _collider.Cast(Vector2.down, filter, _hits, 0.06f);
            for (var i = 0; i < count; i++)
                if (_hits[i].normal.y > 0.6f) return true;
            return false;
        }

        void OnCollisionEnter2D(Collision2D collision) => CheckWallHit(collision);
        void OnCollisionStay2D(Collision2D collision) => CheckWallHit(collision);

        /// <summary>
        /// Running face-first into a wall or block ends the run, unless the traveler only clipped
        /// its top edge: then he steps up onto it (much less frustrating).
        /// </summary>
        void CheckWallHit(Collision2D collision)
        {
            if (IsDead) return;
            for (var i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.x >= -0.7f) continue;

                var otherTop = collision.collider.bounds.max.y;
                var feet = _body.position.y - _collider.size.y / 2f;
                if (otherTop - feet <= tuning.stepUpAssist)
                {
                    _body.position = new Vector2(_body.position.x + 0.1f, otherTop + _collider.size.y / 2f + 0.02f);
                    var v = _body.linearVelocity;
                    v.y = Mathf.Max(0f, v.y);
                    _body.linearVelocity = v;
                    return;
                }
                Die();
                return;
            }
        }

        /// <summary>World-space box of the traveler's collider (for the world-switch safety check).</summary>
        public Bounds BodyBounds => new Bounds(_body.position, new Vector3(_collider.size.x, _collider.size.y, 1f));

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsDead && other.GetComponent<RunnerHazard>() != null) Die();
        }

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            Died?.Invoke();
        }
    }
}
