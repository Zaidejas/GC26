using UnityEngine;
using UnityEngine.UI;

namespace GC26.Findepair
{
    /// <summary>Sizes the grid cells so all cards fit: 3 x 4 in portrait, 4 x 3 in landscape, cards 3:4.</summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class BoardFitter : MonoBehaviour
    {
        public const float Aspect = 0.75f;
        public int cardCount = 12;

        GridLayoutGroup _grid;
        Vector2 _lastSize;

        void Awake() => _grid = GetComponent<GridLayoutGroup>();
        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0 || (size - _lastSize).sqrMagnitude < 0.01f) return;
            _lastSize = size;

            var (cols, rows) = FindepairRules.Grid(cardCount, size.y >= size.x);
            var pad = _grid.padding;
            float w = (size.x - pad.horizontal - _grid.spacing.x * (cols - 1)) / cols;
            float h = (size.y - pad.vertical - _grid.spacing.y * (rows - 1)) / rows;
            if (w / h > Aspect) w = h * Aspect; else h = w / Aspect;
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = cols;
            _grid.cellSize = new Vector2(Mathf.Floor(w), Mathf.Floor(h));
        }
    }
}
