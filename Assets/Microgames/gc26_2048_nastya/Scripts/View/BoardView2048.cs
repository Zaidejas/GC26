using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Draws the board from the model (TDD 6.1-6.2). Cell positions are calculated from the board size,
    /// padding and gap (no GridLayoutGroup), so tiles can later move freely between cells.
    /// Put it on the square board RectTransform (AspectRatioFitter, Fit In Parent).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardView2048 : MonoBehaviour
    {
        [SerializeField] Image boardImage;
        [SerializeField] RectTransform cellsRoot;
        [SerializeField] RectTransform tilesRoot;
        [SerializeField] Image cellPrefab;
        [SerializeField] TileView2048 tilePrefab;

        [Header("Layout, share of the board side")]
        [SerializeField, Range(0f, 0.1f)] float padding = 0.03f;
        [SerializeField, Range(0f, 0.1f)] float gap = 0.025f;

        Theme2048 _theme;
        TilePool2048 _pool;
        Board2048 _board;
        int _size;
        readonly List<Image> _cells = new List<Image>(36);
        readonly Dictionary<int, TileView2048> _views = new Dictionary<int, TileView2048>(36);
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _stale = new List<int>(16);

        public float CellSize { get; private set; }

        /// <summary>Creates the background cells and warms up the tile pool for a board size.</summary>
        public void Build(int size, Theme2048 theme)
        {
            _theme = theme;
            _pool ??= new TilePool2048(tilePrefab, tilesRoot);
            if (boardImage != null) boardImage.color = theme.board;

            if (_size != size)
            {
                foreach (var cell in _cells) Destroy(cell.gameObject);
                _cells.Clear();
                for (int i = 0; i < size * size; i++)
                {
                    var cell = Instantiate(cellPrefab, cellsRoot, false);
                    cell.gameObject.name = "Cell" + i;
                    _cells.Add(cell);
                }
                _size = size;
                _pool.Prewarm(size * size);
            }
            foreach (var cell in _cells) cell.color = theme.emptyCell;
            Layout();
        }

        /// <summary>Shows exactly the tiles of <paramref name="board"/>, reusing views by tile Id.</summary>
        public void Render(Board2048 board)
        {
            _board = board;
            _seen.Clear();
            for (int y = 0; y < board.Size; y++)
            {
                for (int x = 0; x < board.Size; x++)
                {
                    if (!(board.Get(x, y) is Tile2048 tile)) continue;
                    _seen.Add(tile.Id);
                    if (!_views.TryGetValue(tile.Id, out var view))
                    {
                        view = _pool.Get();
                        _views.Add(tile.Id, view);
                    }
                    view.Set(tile.Value, _theme, CellSize);
                    view.Place(CellPosition(x, y), CellSize);
                }
            }

            _stale.Clear();
            foreach (var pair in _views)
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            foreach (int id in _stale)
            {
                _pool.Release(_views[id]);
                _views.Remove(id);
            }
        }

        /// <summary>Center of cell (x, y) relative to the board center; y = 0 is the top row.</summary>
        public Vector2 CellPosition(int x, int y)
        {
            float side = Side();
            float pad = side * padding;
            float step = CellSize + side * gap;
            return new Vector2(
                -side * 0.5f + pad + CellSize * 0.5f + x * step,
                side * 0.5f - pad - CellSize * 0.5f - y * step);
        }

        float Side()
        {
            var rect = ((RectTransform)transform).rect;
            return Mathf.Min(rect.width, rect.height);
        }

        void Layout()
        {
            if (_size == 0) return;
            float side = Side();
            CellSize = Mathf.Max(0f, (side * (1f - 2f * padding) - side * gap * (_size - 1)) / _size);
            for (int i = 0; i < _cells.Count; i++)
            {
                var rect = _cells[i].rectTransform;
                rect.anchoredPosition = CellPosition(i % _size, i / _size);
                rect.sizeDelta = new Vector2(CellSize, CellSize);
            }
            if (_board != null) Render(_board);
        }

        void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && _theme != null) Layout();
        }
    }
}
