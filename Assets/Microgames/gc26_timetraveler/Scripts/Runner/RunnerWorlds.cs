using System;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>Which timeline a track piece exists in.</summary>
    public enum RunnerWorld
    {
        Both,
        Past,
        Future,
    }

    /// <summary>
    /// The active timeline plus the switch charges. Switching costs one charge;
    /// charges refill over time. Plain class owned by the session (no static state).
    /// </summary>
    public class RunnerWorldState
    {
        public const int MaxCharges = 3;
        /// <summary>Seconds (game time) to refill one charge.</summary>
        public const float SecondsPerCharge = 1.5f;

        public RunnerWorld Current { get; private set; } = RunnerWorld.Past;
        public float Charges { get; private set; } = MaxCharges;
        public event Action<RunnerWorld> Changed;

        public RunnerWorld Other => Current == RunnerWorld.Past ? RunnerWorld.Future : RunnerWorld.Past;

        public void Reset()
        {
            Charges = MaxCharges;
            Set(RunnerWorld.Past);
        }

        public void Tick(float deltaTime)
        {
            Charges = Mathf.Min(MaxCharges, Charges + deltaTime / SecondsPerCharge);
        }

        public bool CanSwitch => Charges >= 1f;

        public void Switch()
        {
            Charges -= 1f;
            Set(Other);
        }

        void Set(RunnerWorld world)
        {
            Current = world;
            Changed?.Invoke(world);
        }

        public static bool IsActive(RunnerWorld pieceWorld, RunnerWorld current) =>
            pieceWorld == RunnerWorld.Both || pieceWorld == current;
    }

    /// <summary>
    /// A track piece that only exists in one timeline. In the other timeline it is a faint ghost
    /// with its collider off, so the player can see what is waiting after a switch.
    /// </summary>
    public class RunnerWorldMember : MonoBehaviour
    {
        const float GhostAlpha = 0.18f;

        public RunnerWorld World { get; private set; }

        RunnerWorldState _state;
        Collider2D _collider;
        SpriteRenderer _renderer;
        Color _baseColor;

        public void Init(RunnerWorld world, RunnerWorldState state)
        {
            World = world;
            _state = state;
            _collider = GetComponent<Collider2D>();
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;
            _state.Changed += Apply;
            Apply(_state.Current);
        }

        void OnDestroy()
        {
            if (_state != null) _state.Changed -= Apply;
        }

        void Apply(RunnerWorld current)
        {
            var active = RunnerWorldState.IsActive(World, current);
            if (_collider != null) _collider.enabled = active;
            if (_renderer != null)
            {
                var c = _baseColor;
                c.a = active ? _baseColor.a : GhostAlpha;
                _renderer.color = c;
            }
        }
    }
}
