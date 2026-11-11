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
    /// The active timeline. Switching is free, with a short cooldown so a double swipe does not
    /// switch twice. Plain class owned by the session (no static state).
    /// Colour rule shown to the player: the traveler is ORANGE in the Past and BLUE in the Future;
    /// pieces of your own colour are solid, pieces of the other colour are faint ghosts.
    /// </summary>
    public class RunnerWorldState
    {
        /// <summary>Seconds (real time) between two switches.</summary>
        public const float Cooldown = 0.25f;

        public static readonly Color PastColor = new Color(1f, 0.6f, 0.2f);
        public static readonly Color FutureColor = new Color(0.25f, 0.8f, 1f);

        public RunnerWorld Current { get; private set; } = RunnerWorld.Past;
        public event Action<RunnerWorld> Changed;

        float _lastSwitch = -10f;

        public RunnerWorld Other => Current == RunnerWorld.Past ? RunnerWorld.Future : RunnerWorld.Past;
        public bool CanSwitch => Time.unscaledTime - _lastSwitch >= Cooldown;

        public void Reset()
        {
            _lastSwitch = -10f;
            Set(RunnerWorld.Past);
        }

        public void Switch()
        {
            _lastSwitch = Time.unscaledTime;
            Set(Other);
        }

        void Set(RunnerWorld world)
        {
            Current = world;
            Changed?.Invoke(world);
        }

        public static Color ColorOf(RunnerWorld world) => world == RunnerWorld.Future ? FutureColor : PastColor;
        public static string NameOf(RunnerWorld world) => world == RunnerWorld.Future ? "FUTURE" : "PAST";

        public static bool IsActive(RunnerWorld pieceWorld, RunnerWorld current) =>
            pieceWorld == RunnerWorld.Both || pieceWorld == current;
    }

    /// <summary>
    /// A track piece that only exists in one timeline. In the other timeline it is a faint ghost
    /// with its collider off, so the player can see what is waiting after a switch.
    /// </summary>
    public class RunnerWorldMember : MonoBehaviour
    {
        const float GhostAlpha = 0.22f;

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
