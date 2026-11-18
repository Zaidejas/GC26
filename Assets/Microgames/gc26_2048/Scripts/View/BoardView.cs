using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048
{
    /// <summary>
    /// Draws the 4×4 field in its wooden crate and animates moves: slide 0.1 s, then pulse for merges,
    /// cheese flies to the hole, the new item grows in. Tiles come from a small pool.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        const float SlideTime = 0.1f;

        CheeseArt art;
        RectTransform pan;
        readonly Dictionary<int, TileView> tiles = new Dictionary<int, TileView>();
        readonly Stack<TileView> pool = new Stack<TileView>();

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
        }

        public void ShowBoard(Board board)
        {
            StopAllCoroutines();
            Busy = false;
            foreach (var t in tiles.Values) Release(t);
            tiles.Clear();
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
                if ((e.Kind == TileEventKind.Merged || e.Kind == TileEventKind.ToHole) && tiles.TryGetValue(e.OtherId, out var gone))
                    gone.transform.SetSiblingIndex(CheeseRules.Size * CheeseRules.Size); // just above the slots

            for (float t = 0; t < SlideTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / SlideTime);
                foreach (var m in moving) m.view.SetAnchor(Vector2.Lerp(m.from, m.to, k));
                yield return null;
            }
            foreach (var m in moving) m.view.SetAnchor(m.to);

            // 2. merges, cheese, new item
            foreach (var e in result.Events)
            {
                if (e.Kind == TileEventKind.Merged)
                {
                    if (tiles.TryGetValue(e.OtherId, out var gone)) { Release(gone); tiles.Remove(e.OtherId); }
                    if (tiles.TryGetValue(e.Id, out var keep))
                    {
                        keep.Show(e.Level, art);
                        StartCoroutine(keep.Pulse());
                    }
                }
                else if (e.Kind == TileEventKind.ToHole)
                {
                    foreach (int id in new[] { e.Id, e.OtherId })
                    {
                        if (!tiles.TryGetValue(id, out var cheese)) continue;
                        tiles.Remove(id);
                        cheese.Show(CheeseRules.CheeseLevel, art);
                        StartCoroutine(FlyAndRelease(cheese));
                    }
                }
            }

            if (result.Spawned.HasValue)
            {
                var s = result.Spawned.Value;
                var view = Take(s.Level);
                view.SetCell(s.Row, s.Col);
                tiles[s.Id] = view;
                StartCoroutine(view.Appear());
            }

            Busy = false;
            onDone?.Invoke();
        }

        IEnumerator FlyAndRelease(TileView view)
        {
            view.transform.SetAsLastSibling();
            yield return view.FlyToHole();
            Release(view);
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
