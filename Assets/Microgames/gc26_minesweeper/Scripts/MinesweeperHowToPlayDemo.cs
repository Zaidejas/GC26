using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Minesweeper
{
    // "How to play" (GDD 6.6): four pages, one per tap, each a short looping animation on a 4×3 demo
    // board with a one-line caption:
    //   0. a "finger" taps a cell and an area opens        – Tap a cell to open it
    //   1. a number pulses, the red flags it counts appear  – A number = red flags touching it
    //   2. the finger taps a red flag: the round is lost    – Tap a red flag and you lose!
    //   3. a long press fills a ring and puts a yellow flag – Long press / Right-click: yellow flag
    // Runs on unscaled time, only while the pop-up is open.
    public class MinesweeperHowToPlayDemo : MonoBehaviour
    {
        public const int PageCount = 4;

        const int W = 4, H = 3;          // demo board size
        const float Cell = 130f;         // cell size in canvas units

        // Red flags at (3,0) and (3,2) (x right, y down). The numbers are worked out from that.
        static readonly Vector2Int[] MinePositions = { new Vector2Int(3, 0), new Vector2Int(3, 2) };
        static readonly Vector2Int TapCell = new Vector2Int(0, 1);       // opens columns 0–2
        static readonly Vector2Int NumberCell = new Vector2Int(2, 1);    // shows "2"
        static readonly Vector2Int HitCell = new Vector2Int(3, 2);       // the red flag that is hit
        static readonly Vector2Int FlagCell = new Vector2Int(3, 0);      // gets the yellow flag

        public int Page { get; private set; }

        MinesweeperSprites sprites;
        Text caption, dots;
        string redHex, yellowHex;
        readonly bool[,] mines = new bool[W, H];
        readonly int[,] numbers = new int[W, H];
        Image[,] tiles, icons;
        Text[,] labels;
        RectTransform board, frame, pointer;
        Image ring;

        public void Init(MinesweeperSprites s, Font font, Text captionText, Text pageDots, Color red, Color yellow)
        {
            sprites = s;
            caption = captionText;
            dots = pageDots;
            redHex = ColorUtility.ToHtmlStringRGB(red);
            yellowHex = ColorUtility.ToHtmlStringRGB(yellow);
            foreach (var m in MinePositions) mines[m.x, m.y] = true;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    foreach (var n in Around(x, y))
                        if (mines[n.x, n.y]) numbers[x, y]++;

            var root = (RectTransform)transform;
            gameObject.AddComponent<LayoutElement>().preferredHeight = H * Cell + 40f;

            board = NewRect("DemoBoard", root);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(W * Cell, H * Cell);

            tiles = new Image[W, H];
            icons = new Image[W, H];
            labels = new Text[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    RectTransform c = NewRect($"Cell {x},{y}", board);
                    c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f);
                    c.sizeDelta = new Vector2(Cell - 4f, Cell - 4f);
                    c.anchoredPosition = CellCenter(x, y);
                    tiles[x, y] = c.gameObject.AddComponent<Image>();
                    tiles[x, y].type = Image.Type.Sliced;
                    tiles[x, y].raycastTarget = false;

                    RectTransform i = NewRect("Icon", c);
                    i.anchorMin = new Vector2(0.12f, 0.12f);
                    i.anchorMax = new Vector2(0.88f, 0.88f);
                    i.offsetMin = i.offsetMax = Vector2.zero;
                    icons[x, y] = i.gameObject.AddComponent<Image>();
                    icons[x, y].preserveAspect = true;
                    icons[x, y].raycastTarget = false;

                    RectTransform t = NewRect("Number", c);
                    t.anchorMin = Vector2.zero;
                    t.anchorMax = Vector2.one;
                    t.offsetMin = t.offsetMax = Vector2.zero;
                    labels[x, y] = t.gameObject.AddComponent<Text>();
                    labels[x, y].font = font;
                    labels[x, y].fontSize = 80;
                    labels[x, y].fontStyle = FontStyle.Bold;
                    labels[x, y].alignment = TextAnchor.MiddleCenter;
                    labels[x, y].raycastTarget = false;
                }

            frame = NewRect("Highlight", board);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = sprites.Frame;
            frameImage.type = Image.Type.Sliced;
            frameImage.raycastTarget = false;

            pointer = NewRect("Finger", board);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.sizeDelta = new Vector2(70, 70);
            var dot = pointer.gameObject.AddComponent<Image>();
            dot.sprite = sprites.Dot;
            dot.color = new Color(1f, 1f, 1f, 0.85f);
            dot.raycastTarget = false;
            pointer.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);

            RectTransform r = NewRect("Ring", pointer);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(120, 120);
            ring = r.gameObject.AddComponent<Image>();
            ring.sprite = sprites.Ring;
            ring.color = yellow;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillAmount = 0f;
            ring.raycastTarget = false;

            ResetBoard(false);
        }

        // Every time the pop-up opens it starts at the first page.
        void OnEnable()
        {
            if (tiles != null) ShowPage(0);
        }

        void OnDisable() => StopAllCoroutines();

        // Goes to the next page. Returns false after the last page (the pop-up should close).
        public bool Next()
        {
            if (Page >= PageCount - 1) return false;
            ShowPage(Page + 1);
            return true;
        }

        public void ShowPage(int page)
        {
            Page = page;
            StopAllCoroutines();
            dots.text = $"{page + 1} / {PageCount}";

            switch (page)
            {
                case 0:
                    caption.text = "Tap a cell to open it";
                    StartCoroutine(TapPage());
                    break;
                case 1:
                    caption.text = "A number = how many red flags touch it";
                    StartCoroutine(NumberPage());
                    break;
                case 2:
                    caption.text = $"Tap a <color=#{redHex}>red flag</color> and you lose!";
                    StartCoroutine(RedFlagPage());
                    break;
                default:
                    string how = Application.isMobilePlatform ? "Long press" : "Right-click";
                    caption.text = $"{how} to mark it with a <color=#{yellowHex}>yellow flag</color>";
                    StartCoroutine(YellowFlagPage());
                    break;
            }
        }

        // ---- the four looping animations

        IEnumerator TapPage()
        {
            while (true)
            {
                ResetBoard(false);
                pointer.anchoredPosition = CellCenter(0, H - 1) + new Vector2(-40, -160);
                pointer.gameObject.SetActive(true);
                yield return Move(CellCenter(TapCell.x, TapCell.y), 0.6f);
                yield return Tap();
                yield return OpenArea(TapCell);
                pointer.gameObject.SetActive(false);
                yield return Wait(1.4f);
            }
        }

        IEnumerator NumberPage()
        {
            ResetBoard(true);
            frame.gameObject.SetActive(true);
            frame.anchoredPosition = CellCenter(NumberCell.x, NumberCell.y);
            while (true)
            {
                // Pulse the number, then show (faded) the red flags it counts.
                for (float t = 0; t < 3.2f; t += Time.unscaledDeltaTime)
                {
                    float pad = 8f + 6f * Mathf.PingPong(t * 2f, 1f);
                    frame.sizeDelta = Vector2.one * (Cell + pad * 2f);
                    bool showFlags = t > 0.9f && t < 2.8f;
                    foreach (var m in MinePositions) ShowIcon(m, showFlags ? sprites.RedFlag : null, 0.55f);
                    yield return null;
                }
            }
        }

        IEnumerator RedFlagPage()
        {
            while (true)
            {
                ResetBoard(true);
                pointer.anchoredPosition = CellCenter(NumberCell.x, NumberCell.y) + new Vector2(0, -60);
                pointer.gameObject.SetActive(true);
                yield return Move(CellCenter(HitCell.x, HitCell.y), 0.6f);
                yield return Tap();
                tiles[HitCell.x, HitCell.y].sprite = sprites.HitTile;
                ShowIcon(HitCell, sprites.RedFlag, 1f);
                pointer.gameObject.SetActive(false);
                yield return Shake(0.35f);
                yield return Wait(1.4f);
            }
        }

        IEnumerator YellowFlagPage()
        {
            while (true)
            {
                ResetBoard(true);
                pointer.anchoredPosition = CellCenter(NumberCell.x, NumberCell.y) + new Vector2(0, 60);
                pointer.gameObject.SetActive(true);
                yield return Move(CellCenter(FlagCell.x, FlagCell.y), 0.6f);
                for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime)
                {
                    ring.fillAmount = t / 0.8f;
                    yield return null;
                }
                ring.fillAmount = 0f;
                ShowIcon(FlagCell, sprites.YellowFlag, 1f);
                pointer.gameObject.SetActive(false);
                yield return Wait(1.6f);
            }
        }

        // ---- helpers

        // opened = the area from page 0 already open (pages 1–3 start from there).
        void ResetBoard(bool opened)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    tiles[x, y].sprite = sprites.HiddenTile;
                    icons[x, y].enabled = false;
                    labels[x, y].text = "";
                }
            if (opened)
                foreach (var p in AreaFrom(TapCell)) OpenCell(p);
            frame.gameObject.SetActive(false);
            pointer.gameObject.SetActive(false);
            pointer.localScale = Vector3.one;
            ring.fillAmount = 0f;
            board.anchoredPosition = Vector2.zero;
        }

        // Opens the area cell by cell, like the game's flood fill.
        IEnumerator OpenArea(Vector2Int start)
        {
            foreach (var p in AreaFrom(start))
            {
                OpenCell(p);
                yield return Wait(0.06f);
            }
        }

        // The cells a tap on "start" opens, in flood-fill order.
        List<Vector2Int> AreaFrom(Vector2Int start)
        {
            var order = new List<Vector2Int>();
            var seen = new bool[W, H];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            seen[start.x, start.y] = true;
            while (queue.Count > 0)
            {
                Vector2Int p = queue.Dequeue();
                order.Add(p);
                if (numbers[p.x, p.y] != 0) continue;
                foreach (var n in Around(p.x, p.y))
                    if (!seen[n.x, n.y] && !mines[n.x, n.y]) { seen[n.x, n.y] = true; queue.Enqueue(n); }
            }
            return order;
        }

        void OpenCell(Vector2Int p)
        {
            tiles[p.x, p.y].sprite = sprites.OpenTile;
            if (numbers[p.x, p.y] > 0)
            {
                labels[p.x, p.y].text = numbers[p.x, p.y].ToString();
                labels[p.x, p.y].color = MinesweeperCellView.NumberColors[numbers[p.x, p.y] - 1];
            }
        }

        // sprite null = hide the icon. alpha < 1 = faded (a hint, not a real flag).
        void ShowIcon(Vector2Int c, Sprite s, float alpha)
        {
            icons[c.x, c.y].enabled = s != null;
            if (s == null) return;
            icons[c.x, c.y].sprite = s;
            icons[c.x, c.y].color = new Color(1f, 1f, 1f, alpha);
        }

        IEnumerator Move(Vector2 to, float seconds)
        {
            Vector2 from = pointer.anchoredPosition;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                pointer.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            pointer.anchoredPosition = to;
        }

        // The finger presses down and comes back up.
        IEnumerator Tap()
        {
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float s = 1f - 0.35f * Mathf.Sin(Mathf.PI * t / 0.25f);
                pointer.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            pointer.localScale = Vector3.one;
        }

        // A small shake of the demo board, like the game's loss.
        IEnumerator Shake(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                board.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * 10f * (1f - t / seconds), 0f);
                yield return null;
            }
            board.anchoredPosition = Vector2.zero;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        static IEnumerable<Vector2Int> Around(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < W && ny < H) yield return new Vector2Int(nx, ny);
                }
        }

        static Vector2 CellCenter(int x, int y) =>
            new Vector2((x + 0.5f) * Cell - W * Cell / 2f, H * Cell / 2f - (y + 0.5f) * Cell);

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
