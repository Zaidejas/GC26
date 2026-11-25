using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048
{
    /// <summary>
    /// Draws the 4×4 field in its wooden crate and animates moves: slide 0.1 s, then pulse and glow for merges,
    /// jelly squash for items that bumped into each other without merging, the new item grows in. Cheese is an
    /// ordinary merge result: it stays on the field. Tiles come from a small pool.
    /// The mouse lives on its own layer above the tiles: it drops in on an outer cell and hops across its row or
    /// column, and the cheese it passes vanishes.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        const float SlideTime = 0.1f;
        const float MouseStepTime = 0.13f, MouseInset = 0.08f, MouseHop = 0.18f;

        CheeseArt art;
        RectTransform pan;
        RectTransform mouse;
        Coroutine mouseArrive;
        readonly Dictionary<int, TileView> tiles = new Dictionary<int, TileView>();
        readonly Stack<TileView> pool = new Stack<TileView>();
        readonly HashSet<TileView> vanishing = new HashSet<TileView>();

        public bool Busy { get; private set; }

        public void Init(RectTransform field, CheeseArt artSet)
        {
            art = artSet;
            var crate = CheeseUi.Box(field, "Crate", art.Box, CheeseArt.Wood, true);
            crate.rectTransform.Fill();
            pan = CheeseUi.Box(crate.transform, "Pan", art.Soft, CheeseArt.Plank).rectTransform;
            pan.Place(0.035f, 0.035f, 0.965f, 0.965f);
            for (int r = 0; r < CheeseRules.Size; r++)
            for (int c = 0; c < CheeseRules.Size; c++)
            {
                var slot = CheeseUi.Box(pan, "Slot", art.Soft, CheeseArt.Slot);
                var srt = slot.rectTransform;
                var min = TileView.CellMin(r, c);
                srt.anchorMin = min;
                srt.anchorMax = min + TileView.CellSize;
                srt.offsetMin = srt.offsetMax = Vector2.zero;
            }
            for (int i = 0; i < 20; i++)
                Release(TileView.Create(pan, art));

            // same rect as the pan, above it and all its tiles
            var layer = CheeseUi.Rect("MouseLayer", crate.transform).Place(0.035f, 0.035f, 0.965f, 0.965f);
            var mouseImage = CheeseUi.Picture(layer, "Mouse", art.MouseRun);
            CheeseUi.AddShadow(mouseImage.gameObject, 6f);
            mouse = mouseImage.rectTransform;
            mouse.gameObject.SetActive(false);
        }

        /// <summary>Shows the mouse where the game says it is (after a new or a loaded game).</summary>
        public void ShowMouse(CheeseMouse m)
        {
            bool here = m.State == MouseState.Here;
            mouse.gameObject.SetActive(here);
            if (here) PlaceMouse(TileView.CellMin(m.Row, m.Col), Vector2.zero, 0f, 1f);
        }

        /// <summary>Puts the mouse over a cell (given by its lower-left anchor), nudged by a fraction of a cell.</summary>
        void PlaceMouse(Vector2 cellMin, Vector2 nudge, float tilt, float scale)
        {
            var size = TileView.CellSize;
            var min = cellMin + size * MouseInset + Vector2.Scale(nudge, size);
            mouse.anchorMin = min;
            mouse.anchorMax = min + size * (1f - 2f * MouseInset);
            mouse.offsetMin = mouse.offsetMax = Vector2.zero;
            mouse.localRotation = Quaternion.Euler(0, 0, tilt);
            mouse.localScale = Vector3.one * scale;
        }

        IEnumerator MouseArrive(int row, int col, float duration = 0.4f)
        {
            mouse.gameObject.SetActive(true);
            mouse.SetAsLastSibling();
            var cell = TileView.CellMin(row, col);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                // drops in from above, lands with a little squash
                float drop = k < 0.6f ? 1f - k / 0.6f : 0f;
                float land = k < 0.6f ? 1f : 1f + 0.12f * Mathf.Sin((k - 0.6f) / 0.4f * Mathf.PI);
                PlaceMouse(cell, new Vector2(0f, 0.9f * drop * drop), 0f, Mathf.Min(1f, k / 0.3f) * land);
                yield return null;
            }
            PlaceMouse(cell, Vector2.zero, 0f, 1f);
            mouseArrive = null;
        }

        /// <summary>Hops from one end of the line to the other; each cheese on the way vanishes as the mouse reaches it.</summary>
        IEnumerator MouseRun(MoveResult r)
        {
            if (mouseArrive != null) { StopCoroutine(mouseArrive); mouseArrive = null; }
            mouse.gameObject.SetActive(true);
            var from = TileView.CellMin(r.MouseFromRow, r.MouseFromCol);
            var to = TileView.CellMin(r.MouseRow, r.MouseCol);
            int steps = Mathf.Max(Mathf.Abs(r.MouseRow - r.MouseFromRow), Mathf.Abs(r.MouseCol - r.MouseFromCol), 1);
            float lean = r.MouseCol > r.MouseFromCol ? -12f : r.MouseCol < r.MouseFromCol ? 12f : 0f;
            var eaten = new List<(int step, TileEvent e)>();
            foreach (var e in r.Eaten)
                eaten.Add((Mathf.Max(Mathf.Abs(e.Row - r.MouseFromRow), Mathf.Abs(e.Col - r.MouseFromCol)), e));
            int next = 0;
            float duration = MouseStepTime * steps;
            for (float t = 0; ; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                float along = k * steps;
                while (next < eaten.Count && eaten[next].step <= along + 0.35f) Eat(eaten[next++].e);
                float hop = Mathf.Abs(Mathf.Sin(along * Mathf.PI)) * MouseHop;
                PlaceMouse(Vector2.Lerp(from, to, k), new Vector2(0f, hop), lean * (k < 1f ? 1f : 0f), 1f);
                if (k >= 1f) break;
                yield return null;
            }
            while (next < eaten.Count) Eat(eaten[next++].e);
            PlaceMouse(to, Vector2.zero, 0f, 1f);
        }

        void Eat(TileEvent e)
        {
            if (!tiles.TryGetValue(e.Id, out var cheese)) return;
            tiles.Remove(e.Id);
            StartCoroutine(VanishAndRelease(cheese));
        }

        IEnumerator VanishAndRelease(TileView view)
        {
            vanishing.Add(view);
            yield return view.Vanish();
            vanishing.Remove(view);
            Release(view);
        }

        public void ShowBoard(Board board)
        {
            StopAllCoroutines();
            mouseArrive = null;
            Busy = false;
            foreach (var t in tiles.Values) Release(t);
            tiles.Clear();
            foreach (var t in vanishing) Release(t); // eaten cheese caught mid-animation
            vanishing.Clear();
            for (int r = 0; r < board.Size; r++)
            for (int c = 0; c < board.Size; c++)
            {
                int level = board.LevelAt(r, c);
                if (level == 0) continue;
                var view = Take(level);
                view.SetCell(r, c);
                tiles[board.IdAt(r, c)] = view;
            }
        }

        public void Play(MoveResult result, Action onDone) => StartCoroutine(Animate(result, onDone));

        IEnumerator Animate(MoveResult result, Action onDone)
        {
            Busy = true;

            // 1. slide everything that moves (merged and cheese partners included)
            var moving = new List<(TileView view, Vector2 from, Vector2 to)>();
            foreach (var e in result.Events)
                if (e.Kind == TileEventKind.Moved && tiles.TryGetValue(e.Id, out var v))
                    moving.Add((v, v.AnchorMin, TileView.CellMin(e.Row, e.Col)));
            foreach (var e in result.Events)
                if (e.Kind == TileEventKind.Merged && tiles.TryGetValue(e.OtherId, out var gone))
                    gone.transform.SetSiblingIndex(CheeseRules.Size * CheeseRules.Size); // just above the slots

            for (float t = 0; t < SlideTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / SlideTime);
                foreach (var m in moving) m.view.SetAnchor(Vector2.Lerp(m.from, m.to, k));
                yield return null;
            }
            foreach (var m in moving) m.view.SetAnchor(m.to);

            // 2. merges (cheese included), new item
            foreach (var e in result.Events)
            {
                if (e.Kind == TileEventKind.Merged)
                {
                    if (tiles.TryGetValue(e.OtherId, out var gone)) { Release(gone); tiles.Remove(e.OtherId); }
                    if (tiles.TryGetValue(e.Id, out var keep))
                    {
                        keep.Show(e.Level, art);
                        keep.PlayMerge();
                    }
                }
            }

            bool horizontal = result.Direction == Direction.Left || result.Direction == Direction.Right;
            foreach (int id in result.Bumped)
                if (tiles.TryGetValue(id, out var bumped)) bumped.PlayBump(horizontal);

            if (result.Spawned.HasValue)
            {
                var s = result.Spawned.Value;
                var view = Take(s.Level);
                view.SetCell(s.Row, s.Col);
                tiles[s.Id] = view;
                StartCoroutine(view.Appear());
            }

            // 3. the mouse: comes, or runs across its line (input waits until it has finished eating)
            if (result.MouseArrived) mouseArrive = StartCoroutine(MouseArrive(result.MouseRow, result.MouseCol));
            if (result.MouseRan) yield return MouseRun(result);

            Busy = false;
            onDone?.Invoke();
        }

        TileView Take(int level)
        {
            var view = pool.Count > 0 ? pool.Pop() : TileView.Create(pan, art);
            view.Show(level, art);
            view.transform.SetAsLastSibling();
            return view;
        }

        void Release(TileView view)
        {
            view.gameObject.SetActive(false);
            if (!pool.Contains(view)) pool.Push(view);
        }
    }
}
