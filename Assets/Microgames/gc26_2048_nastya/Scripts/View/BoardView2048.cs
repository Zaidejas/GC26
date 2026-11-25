using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Draws and animates the board (TDD 6). Cell positions are calculated from the board size,
    /// padding and gap (no GridLayoutGroup), so tiles can move freely between cells.
    /// Put it on the square board RectTransform (AspectRatioFitter, Fit In Parent).
    /// The view never decides anything: the model is the single source of truth, and every animation
    /// ends by snapping the views to the model board (<see cref="FinishNow"/>).
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

        [Header("Feedback")]
        [Tooltip("Moved by the invalid-move bump. Empty = the parent of the board (the AspectRatioFitter owns the board itself).")]
        [SerializeField] RectTransform bumpTarget;
        [SerializeField] float bumpDistance = 6f;
        [SerializeField] float mergePopScale = 1.2f;

        struct Slide
        {
            public TileView2048 View;
            public Vector2 From, To;
        }

        Theme2048 _theme;
        TilePool2048 _pool;
        Board2048 _board;
        int _size;
        readonly List<Image> _cells = new List<Image>(36);
        readonly Dictionary<int, TileView2048> _views = new Dictionary<int, TileView2048>(36);
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _stale = new List<int>(16);

        // Current move animation.
        Coroutine _anim;
        Action _onDone;
        readonly List<Slide> _slides = new List<Slide>(16);
        readonly List<int> _popIds = new List<int>(8);
        int _spawnId; // 0 = no spawn

        // Invalid-move bump.
        Coroutine _bump;
        Vector2 _bumpBase;

        public float CellSize { get; private set; }

        /// <summary>A move animation is running; the board on screen is not final yet.</summary>
        public bool IsAnimating => _anim != null;

        /// <summary>The invalid-move bump is running.</summary>
        public bool IsBumping => _bump != null;

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

        /// <summary>
        /// Shows exactly the tiles of <paramref name="board"/> without animation (new game, undo, loading).
        /// A running move animation is dropped and its completion callback is not called.
        /// </summary>
        public void Render(Board2048 board)
        {
            StopMoveAnimation();
            Sync(board);
        }

        /// <summary>
        /// Animates one valid move (TDD 6.3): every tile slides in parallel, then merged tiles pop and the
        /// new tile grows, also in parallel. <paramref name="board"/> is the model board after the move and
        /// the spawn. <paramref name="onDone"/> runs when the animation ends, naturally or via FinishNow.
        /// </summary>
        public void Play(MoveResult2048 result, TileSpawn2048? spawned, Board2048 board, Action onDone)
        {
            FinishNow(); // the caller should have done it; never run two animations at once
            _board = board;
            _onDone = onDone;

            _slides.Clear();
            foreach (var move in result.Moves)
            {
                if (!_views.TryGetValue(move.TileId, out var view)) continue;
                _slides.Add(new Slide
                {
                    View = view,
                    From = view.Rect.anchoredPosition,
                    To = CellPosition(move.ToX, move.ToY),
                });
            }
            _popIds.Clear();
            foreach (var merge in result.Merges) _popIds.Add(merge.NewTile.Id);
            _spawnId = spawned?.Tile.Id ?? 0;

            if (!isActiveAndEnabled || _theme == null)
            {
                FinishNow();
                return;
            }
            _anim = StartCoroutine(MoveRoutine());
        }

        /// <summary>
        /// Completes the running move animation instantly: every tile snaps to the model board.
        /// Used for buffered input, pausing and exiting. Calls the completion callback last.
        /// </summary>
        public void FinishNow()
        {
            if (_anim == null && _onDone == null) return;
            var done = _onDone;
            StopMoveAnimation();
            if (_board != null) Sync(_board);
            done?.Invoke();
        }

        /// <summary>Invalid move: the board shifts a few pixels toward <paramref name="dir"/> and back.</summary>
        public void Bump(Direction2048 dir)
        {
            var target = BumpTarget();
            if (target == null || !isActiveAndEnabled || _theme == null) return;
            if (_bump != null) StopCoroutine(_bump);
            else _bumpBase = target.anchoredPosition;
            _bump = StartCoroutine(BumpRoutine(target, Offset(dir) * bumpDistance, _theme.bump));
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

        // ---------- animation ----------

        IEnumerator MoveRoutine()
        {
            // 1. Slides, all in parallel.
            float duration = _theme.slide;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float k = Tween2048.EaseOut(Tween2048.Progress(t, duration));
                foreach (var s in _slides)
                    s.View.Rect.anchoredPosition = Vector2.LerpUnclamped(s.From, s.To, k);
                if (k >= 1f) break;
                yield return null;
            }

            // 2. The merge sources go back to the pool, the merged and spawned tiles appear.
            Sync(_board);
            ApplyAppear(0f);
            if (_popIds.Count > 0 || _spawnId != 0)
            {
                duration = Mathf.Max(_theme.pop, _theme.spawn);
                for (float t = 0f; ; )
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                    ApplyAppear(t);
                    if (t >= duration) break;
                }
            }

            var done = _onDone;
            _anim = null;
            _onDone = null;
            Sync(_board); // exact final positions and scale 1
            done?.Invoke();
        }

        /// <summary>Scales of the merged tiles (1 → 1.2 → 1) and the spawned tile (0 → 1) at time t.</summary>
        void ApplyAppear(float t)
        {
            float pop = 1f + (mergePopScale - 1f) * Tween2048.Hump(Tween2048.Progress(t, _theme.pop));
            foreach (int id in _popIds)
                if (_views.TryGetValue(id, out var view)) view.Rect.localScale = new Vector3(pop, pop, 1f);

            if (_spawnId != 0 && _views.TryGetValue(_spawnId, out var spawn))
            {
                float s = Tween2048.EaseOut(Tween2048.Progress(t, _theme.spawn));
                spawn.Rect.localScale = new Vector3(s, s, 1f);
            }
        }

        IEnumerator BumpRoutine(RectTransform target, Vector2 offset, float duration)
        {
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float p = Tween2048.Progress(t, duration);
                target.anchoredPosition = _bumpBase + offset * Tween2048.Hump(p);
                if (p >= 1f) break;
                yield return null;
            }
            target.anchoredPosition = _bumpBase;
            _bump = null;
        }

        void StopMoveAnimation()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
            _onDone = null;
        }

        void StopBump()
        {
            if (_bump == null) return;
            StopCoroutine(_bump);
            _bump = null;
            var target = BumpTarget();
            if (target != null) target.anchoredPosition = _bumpBase;
        }

        RectTransform BumpTarget() => bumpTarget != null ? bumpTarget : transform.parent as RectTransform;

        static Vector2 Offset(Direction2048 dir) => dir switch
        {
            Direction2048.Up => Vector2.up,
            Direction2048.Down => Vector2.down,
            Direction2048.Left => Vector2.left,
            _ => Vector2.right,
        };

        // ---------- drawing ----------

        /// <summary>Makes the views match <paramref name="board"/> exactly, reusing views by tile Id.</summary>
        void Sync(Board2048 board)
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
            // The cell positions changed, so an animation in flight would aim at the old ones.
            if (IsAnimating) FinishNow();
            else if (_board != null) Sync(_board);
        }

        void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && _theme != null) Layout();
        }

        void OnDisable()
        {
            // Coroutines die with the object; leave the board in its final state.
            StopBump();
            FinishNow();
        }
    }
}
