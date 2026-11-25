using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The road enemies walk along, on the 16x9 tile field.
    /// Points are typed in prototype map coordinates (x right, y down, tile centers at .5),
    /// exactly as PATH in Docs~/towerdefence_prototype/game.js, and turned into world space here:
    /// one tile = one unit, y pointing up.
    /// Horizontal screen: the field covers x 0..16, y 0..9, the road comes in on the left.
    /// Upright screen (as in the prototype): the map is turned 90 degrees clockwise onto a 9x16 field,
    /// the road comes in at the top and the castle is at the bottom. Distances stay the same, so balance does too.
    /// Only positions turn, not sprites, so letters and HP bars stay level. When the screen turns,
    /// LayoutChanged fires and everything that stores a position moves to the new one.
    /// LayoutChanged also fires when the level's Path or Slots are edited in the Inspector during Play,
    /// so a level can be tuned live. The road and the castle are drawn as placeholder shapes.
    /// Ground enemies follow every point; flying ones go straight from the first point to the last (the castle).
    /// </summary>
    public class EnemyPath : MonoBehaviour
    {
        public const int Columns = 16;
        public const int Rows = 9;

        [Tooltip("The road comes from the level being played (LevelData.path).")]
        [SerializeField] LevelFlow level;

        /// <summary>Waypoints in prototype map coordinates (x right, y down). The last one is the castle.</summary>
        Vector2[] mapPoints => level != null && level.Data != null ? level.Data.path : null;

        [Tooltip("Turn the map for upright screens. Off = always horizontal.")]
        [SerializeField] bool allowVertical = true;

        [Tooltip("Draws the road and the castle. Empty = nothing drawn.")]
        [SerializeField] PlaceholderArt art;

        static readonly Color RoadColor = new(0.55f, 0.44f, 0.29f);
        static readonly Color CastleColor = new(0.62f, 0.62f, 0.66f);
        const float RoadWidth = 0.72f; // as in the prototype

        int _layoutHash;
        readonly List<GameObject> _roadView = new();

        PathLine _ground;
        PathLine _air;

        /// <summary>True when the map is turned for an upright screen (9 wide, 16 tall).</summary>
        public bool IsVertical { get; private set; }

        /// <summary>Raised after the map turned (screen rotated). Positions from before are out of date.</summary>
        public event Action LayoutChanged;

        public float FieldWidth => IsVertical ? Rows : Columns;
        public float FieldHeight => IsVertical ? Columns : Rows;

        void Awake()
        {
            IsVertical = WantVertical();
            _layoutHash = LayoutHash();
        }

        void Start() => DrawRoad();

        void Update()
        {
            // Rebuild when the screen turns or the level's points were edited.
            bool vertical = WantVertical();
            int hash = LayoutHash();
            if (vertical == IsVertical && hash == _layoutHash) return;
            IsVertical = vertical;
            _layoutHash = hash;
            _ground = null;
            _air = null;
            DrawRoad();
            LayoutChanged?.Invoke();
        }

        /// <summary>Changes whenever a road or slot point of the current level changes.</summary>
        int LayoutHash()
        {
            if (level == null || level.Data == null) return 0;
            int h = 17;
            if (level.Data.path != null) foreach (var p in level.Data.path) h = h * 31 + p.GetHashCode();
            h = h * 31 + 7;
            if (level.Data.slots != null) foreach (var s in level.Data.slots) h = h * 31 + s.GetHashCode();
            return h;
        }

        /// <summary>Brown bands along the road and a grey square for the castle (placeholder art).</summary>
        void DrawRoad()
        {
            foreach (var go in _roadView) if (go != null) Destroy(go);
            _roadView.Clear();
            if (art == null || mapPoints == null || mapPoints.Length < 2) return;

            for (int i = 0; i < mapPoints.Length - 1; i++)
            {
                Vector3 a = MapToWorld(mapPoints[i]), b = MapToWorld(mapPoints[i + 1]);
                Vector3 d = b - a;
                var band = art.NewShape("Road", art.Rect, transform, (a + b) / 2f, 1f, RoadColor, 0);
                // + width so the bands overlap into clean corners.
                band.transform.localScale = new Vector3(d.magnitude + RoadWidth, RoadWidth, 1f);
                band.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                _roadView.Add(band.gameObject);
            }
            var castle = art.NewShape("Castle", art.Square, transform, MapToWorld(mapPoints[^1]), 1.1f, CastleColor, 3);
            _roadView.Add(castle.gameObject);
        }

        bool WantVertical() => allowVertical && Screen.height > Screen.width;

        public PathLine Ground => _ground ??= Build(false);
        public PathLine Air => _air ??= Build(true);

        /// <summary>The line an enemy follows: straight for flyers, the road for everyone else.</summary>
        public PathLine For(bool flying) => flying ? Air : Ground;

        /// <summary>Castle position in world space.</summary>
        public Vector3 CastlePosition => Ground.End;

        /// <summary>Prototype map coordinates (y down) to world space (y up), relative to this object.</summary>
        public Vector3 MapToWorld(Vector2 map) => transform.position + (IsVertical
            ? new Vector3(Rows - map.y, Columns - map.x, 0f)  // turned: map x runs down the screen
            : new Vector3(map.x, Rows - map.y, 0f));

        PathLine Build(bool straight)
        {
            var world = new List<Vector3>();
            if (mapPoints == null || mapPoints.Length < 2)
            {
                Debug.LogError($"[{TowerdefenceGame.Id}] EnemyPath needs at least 2 points", this);
                world.Add(transform.position);
                world.Add(transform.position);
                return new PathLine(world);
            }

            if (straight)
            {
                world.Add(MapToWorld(mapPoints[0]));
                world.Add(MapToWorld(mapPoints[^1]));
            }
            else
            {
                foreach (var p in mapPoints)
                    world.Add(MapToWorld(p));
            }
            return new PathLine(world);
        }

        void OnValidate()
        {
            // Rebuild after the points are edited in the Inspector.
            _ground = null;
            _air = null;
        }

        void OnDrawGizmos()
        {
            if (mapPoints == null || mapPoints.Length < 2) return;

            Gizmos.color = new Color(0.85f, 0.7f, 0.4f);
            for (int i = 0; i < mapPoints.Length - 1; i++)
                Gizmos.DrawLine(MapToWorld(mapPoints[i]), MapToWorld(mapPoints[i + 1]));

            Gizmos.color = new Color(0.6f, 0.5f, 1f, 0.5f);
            Gizmos.DrawLine(MapToWorld(mapPoints[0]), MapToWorld(mapPoints[^1]));

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(MapToWorld(mapPoints[^1]), 0.4f);

            // Field border, so the play area is visible in the Scene view.
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            var origin = transform.position;
            Gizmos.DrawWireCube(origin + new Vector3(FieldWidth / 2f, FieldHeight / 2f), new Vector3(FieldWidth, FieldHeight));
        }
    }

    /// <summary>A polyline measured by distance: PositionAt(d) is the point d units from the start.</summary>
    public class PathLine
    {
        readonly Vector3[] _points;
        readonly float[] _startDistance;

        public float Length { get; }
        public Vector3 Start => _points[0];
        public Vector3 End => _points[^1];

        public PathLine(IReadOnlyList<Vector3> points)
        {
            _points = new Vector3[points.Count];
            _startDistance = new float[points.Count];
            float total = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                _points[i] = points[i];
                if (i > 0) total += Vector3.Distance(points[i - 1], points[i]);
                _startDistance[i] = total;
            }
            Length = total;
        }

        public Vector3 PositionAt(float distance)
        {
            if (distance <= 0f) return Start;
            for (int i = 1; i < _points.Length; i++)
            {
                if (distance <= _startDistance[i])
                {
                    float segment = _startDistance[i] - _startDistance[i - 1];
                    float k = segment > 0f ? (distance - _startDistance[i - 1]) / segment : 1f;
                    return Vector3.Lerp(_points[i - 1], _points[i], k);
                }
            }
            return End;
        }
    }
}
