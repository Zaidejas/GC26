using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The road enemies walk along, on the 16x9 tile field.
    /// Points are typed in prototype map coordinates (x right, y down, tile centers at .5),
    /// exactly as PATH in Docs~/towerdefence_prototype/game.js, and turned into world space here:
    /// one tile = one unit, the field covers x 0..16 and y 0..9 with y pointing up.
    /// Ground enemies follow every point; flying ones go straight from the first point to the last (the castle).
    /// </summary>
    public class EnemyPath : MonoBehaviour
    {
        public const int Columns = 16;
        public const int Rows = 9;

        [Tooltip("Waypoints in prototype map coordinates (x right, y down). The last one is the castle.")]
        [SerializeField]
        Vector2[] mapPoints =
        {
            new(-0.5f, 1.5f), new(3.5f, 1.5f), new(3.5f, 6.5f), new(7.5f, 6.5f),
            new(7.5f, 2.5f), new(11.5f, 2.5f), new(11.5f, 6.5f), new(15f, 6.5f),
        };

        PathLine _ground;
        PathLine _air;

        public PathLine Ground => _ground ??= Build(false);
        public PathLine Air => _air ??= Build(true);

        /// <summary>The line an enemy follows: straight for flyers, the road for everyone else.</summary>
        public PathLine For(bool flying) => flying ? Air : Ground;

        /// <summary>Castle position in world space.</summary>
        public Vector3 CastlePosition => Ground.End;

        /// <summary>Prototype map coordinates (y down) to world space (y up), relative to this object.</summary>
        public Vector3 MapToWorld(Vector2 map) => transform.position + new Vector3(map.x, Rows - map.y, 0f);

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

            // Field border, so the 16x9 area is visible in the Scene view.
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            var origin = transform.position;
            Gizmos.DrawWireCube(origin + new Vector3(Columns / 2f, Rows / 2f), new Vector3(Columns, Rows));
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
