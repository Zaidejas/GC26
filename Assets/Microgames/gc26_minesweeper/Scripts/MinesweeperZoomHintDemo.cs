using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Minesweeper
{
    // The animation in the "Zoom in and out" hint (GDD 3.5, shown the first time Expert opens).
    // A small board in a window: two "fingers" spread apart and the board grows (zoom in), both fingers
    // move and the board moves with them, then they pinch together and it shrinks back (zoom out).
    // On a computer one pointer shows "scroll ▲", "wheel + drag", "scroll ▼" instead of fingers.
    // Loops on unscaled time while the pop-up is open.
    public class MinesweeperZoomHintDemo : MonoBehaviour
    {
        const int W = 7, H = 5;              // demo board size
        const float Cell = 80f;              // cell size in canvas units
        const float ZoomIn = 2f;

        // Opened cells (1) with their numbers; 0 = still hidden.
        static readonly int[,] Numbers =
        {   // x = 0..6 across, y = 0..4 down (written as rows here, read as [y, x])
            { -1, -1, -1, -1, -1, -1, -1 },
            { -1,  1,  1,  2, -1, -1, -1 },
            { -1,  1,  0,  1, -1, -1, -1 },
            { -1,  2,  1,  1, -1, -1, -1 },
            { -1, -1, -1, -1, -1, -1, -1 },
        };

        bool touch;
        RectTransform board, fingerA, fingerB;
        Text pointerLabel;

        public void Init(MinesweeperSprites sprites, Font font, bool touchScreen)
        {
            touch = touchScreen;
            var root = (RectTransform)transform;
            gameObject.AddComponent<LayoutElement>().preferredHeight = H * Cell + 60f;

            // The window the board zooms inside (anything outside it is cut off).
            RectTransform window = NewRect("Window", root);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(W * Cell + 20f, H * Cell + 20f);
            var bg = window.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.25f);
            bg.raycastTarget = false;
            window.gameObject.AddComponent<RectMask2D>();

            board = NewRect("DemoBoard", window);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(W * Cell, H * Cell);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    RectTransform c = NewRect($"Cell {x},{y}", board);
                    c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f);
                    c.sizeDelta = new Vector2(Cell - 3f, Cell - 3f);
                    c.anchoredPosition = new Vector2((x + 0.5f) * Cell - W * Cell / 2f, H * Cell / 2f - (y + 0.5f) * Cell);
                    var tile = c.gameObject.AddComponent<Image>();
                    tile.type = Image.Type.Sliced;
                    tile.raycastTarget = false;
                    int n = Numbers[y, x];
                    tile.sprite = n < 0 ? sprites.HiddenTile : sprites.OpenTile;
                    if (n <= 0) continue;
                    RectTransform t = NewRect("Number", c);
                    t.anchorMin = Vector2.zero;
                    t.anchorMax = Vector2.one;
                    t.offsetMin = t.offsetMax = Vector2.zero;
                    var label = t.gameObject.AddComponent<Text>();
                    label.font = font;
                    label.fontSize = 52;
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = MinesweeperCellView.NumberColors[n - 1];
                    label.raycastTarget = false;
                }

            fingerA = MakeFinger(root, sprites, "FingerA");
            fingerB = MakeFinger(root, sprites, "FingerB");
            fingerB.gameObject.SetActive(touch);

            // Computer: a small label next to the pointer says what the mouse is doing.
            RectTransform lr = NewRect("PointerLabel", fingerA);
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.sizeDelta = new Vector2(300, 60);
            lr.anchoredPosition = new Vector2(50, 0);
            pointerLabel = lr.gameObject.AddComponent<Text>();
            pointerLabel.font = font;
            pointerLabel.fontSize = 40;
            pointerLabel.fontStyle = FontStyle.Bold;
            pointerLabel.color = Color.white;
            pointerLabel.alignment = TextAnchor.MiddleLeft;
            pointerLabel.raycastTarget = false;
            pointerLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            pointerLabel.gameObject.SetActive(!touch);
        }

        void OnEnable()
        {
            if (board != null) StartCoroutine(touch ? PinchLoop() : MouseLoop());
        }

        void OnDisable() => StopAllCoroutines();

        // ---- the two loops

        IEnumerator PinchLoop()
        {
            Vector2 move = new Vector2(-110f, 70f);
            while (true)
            {
                SetBoard(1f, Vector2.zero);
                ShowFingers(true);
                // Zoom in: fingers spread apart.
                yield return Animate(1.1f, t => { SetFingers(Vector2.zero, Mathf.Lerp(45f, 175f, t)); SetBoard(Mathf.Lerp(1f, ZoomIn, t), Vector2.zero); });
                yield return Wait(0.5f);
                // Move: both fingers drag, the board follows.
                yield return Animate(0.9f, t => { SetFingers(move * t, 175f); SetBoard(ZoomIn, move * t); });
                yield return Wait(0.5f);
                // Zoom out: fingers pinch together.
                yield return Animate(1.1f, t => { SetFingers(move * (1f - t), Mathf.Lerp(175f, 45f, t)); SetBoard(Mathf.Lerp(ZoomIn, 1f, t), move * (1f - t)); });
                ShowFingers(false);
                yield return Wait(1.0f);
            }
        }

        IEnumerator MouseLoop()
        {
            Vector2 move = new Vector2(-110f, 70f);
            while (true)
            {
                SetBoard(1f, Vector2.zero);
                ShowFingers(true);
                fingerA.anchoredPosition = Vector2.zero;
                // Three wheel clicks up: zoom in step by step.
                pointerLabel.text = "scroll ▲";
                for (int i = 1; i <= 3; i++)
                {
                    float from = Mathf.Lerp(1f, ZoomIn, (i - 1) / 3f), to = Mathf.Lerp(1f, ZoomIn, i / 3f);
                    yield return Animate(0.25f, t => SetBoard(Mathf.Lerp(from, to, t), Vector2.zero));
                    yield return Wait(0.2f);
                }
                yield return Wait(0.3f);
                // Hold the wheel and drag: the board moves with the pointer.
                pointerLabel.text = "wheel + drag";
                yield return Animate(0.9f, t => { fingerA.anchoredPosition = move * t; SetBoard(ZoomIn, move * t); });
                yield return Wait(0.5f);
                // Wheel down: back out.
                pointerLabel.text = "scroll ▼";
                for (int i = 1; i <= 3; i++)
                {
                    float from = Mathf.Lerp(ZoomIn, 1f, (i - 1) / 3f), to = Mathf.Lerp(ZoomIn, 1f, i / 3f);
                    Vector2 p0 = move * (1f - (i - 1) / 3f), p1 = move * (1f - i / 3f);
                    yield return Animate(0.25f, t => SetBoard(Mathf.Lerp(from, to, t), Vector2.Lerp(p0, p1, t)));
                    yield return Wait(0.2f);
                }
                ShowFingers(false);
                yield return Wait(1.0f);
            }
        }

        // ---- helpers

        void SetBoard(float scale, Vector2 offset)
        {
            board.localScale = new Vector3(scale, scale, 1f);
            board.anchoredPosition = offset;
        }

        // Two fingers on a line through "centre", "half" apart from it on each side.
        void SetFingers(Vector2 centre, float half)
        {
            Vector2 dir = new Vector2(0.8f, 0.6f);
            fingerA.anchoredPosition = centre - dir * half;
            fingerB.anchoredPosition = centre + dir * half;
        }

        void ShowFingers(bool on)
        {
            fingerA.gameObject.SetActive(on);
            fingerB.gameObject.SetActive(on && touch);
        }

        static IEnumerator Animate(float seconds, System.Action<float> step)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                step(Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            step(1f);
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        static RectTransform MakeFinger(RectTransform parent, MinesweeperSprites sprites, string name)
        {
            RectTransform f = NewRect(name, parent);
            f.anchorMin = f.anchorMax = new Vector2(0.5f, 0.5f);
            f.sizeDelta = new Vector2(70, 70);
            var dot = f.gameObject.AddComponent<Image>();
            dot.sprite = sprites.Dot;
            dot.color = new Color(1f, 1f, 1f, 0.85f);
            dot.raycastTarget = false;
            f.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            return f;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
