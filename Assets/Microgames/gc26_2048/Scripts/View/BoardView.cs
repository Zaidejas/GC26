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
    /// The mouse hole and the mouse live on their own layer above the tiles: the hole is a darker cell with a bright
    /// frame around it (an item in it stays visible), the mouse is shown only while it runs out of the hole across its
    /// line; the cheese it passes vanishes, then the hole moves, or closes if the mouse got cheese.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        const float SlideTime = 0.1f;
        const float MouseStepTime = 0.13f, MouseInset = 0.08f, MouseHop = 0.18f, PopTime = 0.12f;
        const float HoleShade = 0.34f, HoleFadeTime = 0.25f, FrameOutset = 0.06f;

        CheeseArt art;
        RectTransform pan;
        RectTransform mouse;
        Image hole, holeFrame;
        Coroutine holeFade;
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

            // same rect as the pan, above it and all its tiles: the hole (a dark veil over its cell), then the mouse
            var layer = CheeseUi.Rect("MouseLayer", crate.transform).Place(0.035f, 0.035f, 0.965f, 0.965f);
            hole = CheeseUi.Box(layer, "Hole", art.Soft, HoleColor(0f));
            hole.gameObject.SetActive(false);
            // the frame sits just outside the cell, in the gap between cells, so it shows around an item too
            holeFrame = CheeseUi.Box(hole.transform, "Frame", art.Ring, FrameColor(0f));
            var frt = holeFrame.rectTransform;
            frt.anchorMin = new Vector2(-FrameOutset, -FrameOutset);
            frt.anchorMax = new Vector2(1f + FrameOutset, 1f + FrameOutset);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var mouseImage = CheeseUi.Picture(layer, "Mouse", art.MouseRun);
            CheeseUi.AddShadow(mouseImage.gameObject, 6f);
            mouse = mouseImage.rectTransform;
            mouse.gameObject.SetActive(false);
        }

        static Color HoleColor(float alpha)
        {
            Color c = CheeseArt.Ink;
            c.a = alpha;
            return c;
        }

        /// <summary>Warm red frame, fully opaque when the veil is at full shade.</summary>
        static Color FrameColor(float shade)
        {
            Color c = CheeseArt.Warn;
            c.a = Mathf.Clamp01(shade / HoleShade);
            return c;
        }

        /// <summary>Veil and frame together; 0 = invisible, <see cref="HoleShade"/> = fully shown.</summary>
        void ShadeHole(float shade)
        {
            hole.color = HoleColor(shade);
            holeFrame.color = FrameColor(shade);
        }

        /// <summary>Shows the hole where the game says it is (after a new or a loaded game); the mouse stays hidden.</summary>
        public void ShowMouse(CheeseMouse m)
        {
            mouse.gameObject.SetActive(false);
            bool here = m.State == MouseState.Here;
            hole.gameObject.SetActive(here);
            if (here)
            {
                SetCellRect(hole.rectTransform, TileView.CellMin(m.Row, m.Col));
                ShadeHole(HoleShade);
            }
        }

        static void SetCellRect(RectTransform rt, Vector2 cellMin)
        {
            rt.anchorMin = cellMin;
            rt.anchorMax = cellMin + TileView.CellSize;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The hole fades out of its old cell (if it had one), then into its new one; with <paramref name="close"/>
        /// it only fades out and is gone.
        /// </summary>
        IEnumerator MoveHole(int row, int col, bool fromNowhere, bool close)
        {
            if (!fromNowhere && hole.gameObject.activeSelf)
                for (float t = 0; t < HoleFadeTime; t += Time.unscaledDeltaTime)
                {
                    ShadeHole(HoleShade * (1f - t / HoleFadeTime));
                    yield return null;
                }
            if (close)
            {
                hole.gameObject.SetActive(false);
                holeFade = null;
                yield break;
            }
            hole.gameObject.SetActive(true);
            SetCellRect(hole.rectTransform, TileView.CellMin(row, col));
            for (float t = 0; t < HoleFadeTime; t += Time.unscaledDeltaTime)
            {
                ShadeHole(HoleShade * t / HoleFadeTime);
                yield return null;
            }
            ShadeHole(HoleShade);
            holeFade = null;
        }

        void StartHole(int row, int col, bool fromNowhere, bool close = false)
        {
            if (holeFade != null) StopCoroutine(holeFade);
            holeFade = StartCoroutine(MoveHole(row, col, fromNowhere, close));
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

        /// <summary>
        /// Pops out of the hole, hops to the other end of the line (each cheese on the way vanishes as the mouse
        /// reaches it), dives out of sight; then the hole moves.
        /// </summary>
        IEnumerator MouseRun(MoveResult r)
        {
            var from = TileView.CellMin(r.MouseFromRow, r.MouseFromCol);
            var to = TileView.CellMin(r.MouseToRow, r.MouseToCol);
            int steps = Mathf.Max(Mathf.Abs(r.MouseToRow - r.MouseFromRow), Mathf.Abs(r.MouseToCol - r.MouseFromCol), 1);
            float lean = r.MouseToCol > r.MouseFromCol ? -12f : r.MouseToCol < r.MouseFromCol ? 12f : 0f;
            var eaten = new List<(int step, TileEvent e)>();
            foreach (var e in r.Eaten)
                eaten.Add((Mathf.Max(Mathf.Abs(e.Row - r.MouseFromRow), Mathf.Abs(e.Col - r.MouseFromCol)), e));
            int next = 0;

            mouse.gameObject.SetActive(true);
            mouse.SetAsLastSibling();
            for (float t = 0; t < PopTime; t += Time.unscaledDeltaTime)
            {
                PlaceMouse(from, Vector2.zero, 0f, EaseOut(t / PopTime));
                yield return null;
            }

            float duration = MouseStepTime * steps;
            for (float t = 0; ; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                float along = k * steps;
                while (next < eaten.Count && eaten[next].step <= along + 0.35f) Eat(eaten[next++].e);
                float hop = Mathf.Abs(Mathf.Sin(along * Mathf.PI)) * MouseHop;
                PlaceMouse(Vector2.Lerp(from, to, k), new Vector2(0f, hop), k < 1f ? lean : 0f, 1f);
                if (k >= 1f) break;
                yield return null;
            }
            while (next < eaten.Count) Eat(eaten[next++].e);

            for (float t = 0; t < PopTime; t += Time.unscaledDeltaTime)
            {
                PlaceMouse(to, Vector2.zero, 0f, 1f - EaseOut(t / PopTime));
                yield return null;
            }
            mouse.gameObject.SetActive(false);
            StartHole(r.HoleRow, r.HoleCol, false, r.HoleClosed);
        }

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

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
            holeFade = null;
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

        /// <summary>An item put on the field outside a move (debug cheats): grows in and glows.</summary>
        public void AddTile(TileEvent e)
        {
            var view = Take(e.Level);
            view.SetCell(e.Row, e.Col);
            tiles[e.Id] = view;
            view.PlayMerge();
        }

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

            // 3. the hole opens, or the mouse runs out of it (input waits until it has finished eating)
            if (result.HoleOpened) StartHole(result.HoleRow, result.HoleCol, true);
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
