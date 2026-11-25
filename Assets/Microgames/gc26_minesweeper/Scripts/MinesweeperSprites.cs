using UnityEngine;

namespace GC26.Minesweeper
{
    // Simple graphics drawn in code (tiles, flags, faces, icons), so the game needs no image files.
    // Flags are a dark pole with a rectangular cloth: red = the hazard, yellow = the player's marker.
    public sealed class MinesweeperSprites
    {
        public Sprite Rounded, HiddenTile, OpenTile, HitTile, Frame;
        public Sprite RedFlag, YellowFlag, WrongFlag, MenuIcon;
        public Sprite FaceSmile, FaceOh, FaceCool, FaceDead;
        public Sprite Dot, Ring;   // How to play demo: the "finger" and the long-press ring

        const int N = 64;   // every texture is 64x64 pixels

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Dark = new Color32(0x22, 0x22, 0x22, 0xFF);
        static readonly Color32 Pole = new Color32(0x33, 0x33, 0x33, 0xFF);
        static readonly Color32 Red = new Color32(0xE0, 0x30, 0x2B, 0xFF);
        static readonly Color32 Yellow = new Color32(0xF2, 0xC4, 0x18, 0xFF);
        static readonly Color32 White = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        public static MinesweeperSprites Create()
        {
            var s = new MinesweeperSprites();

            // White rounded rectangle, tinted by Image.color (buttons, panels). Sliced so corners keep their size.
            s.Rounded = Make(Paint(p => p.RoundedRect(0, 0, N - 1, N - 1, 16, White)), 20);

            // Closed tile: grey with a light top-left edge and a dark bottom-right edge.
            s.HiddenTile = Make(Paint(p =>
            {
                p.Rect(0, 0, N - 1, N - 1, new Color32(0xB9, 0xBF, 0xCC, 0xFF));
                p.Rect(0, N - 6, N - 1, N - 1, new Color32(0xDC, 0xDF, 0xE6, 0xFF));   // top
                p.Rect(0, 0, 5, N - 1, new Color32(0xDC, 0xDF, 0xE6, 0xFF));           // left
                p.Rect(0, 0, N - 1, 5, new Color32(0x8E, 0x94, 0xA3, 0xFF));           // bottom
                p.Rect(N - 6, 0, N - 1, N - 1, new Color32(0x8E, 0x94, 0xA3, 0xFF));   // right
            }), 8);

            // Open tile: flat and light with a thin border.
            s.OpenTile = Make(Paint(p =>
            {
                p.Rect(0, 0, N - 1, N - 1, new Color32(0xC5, 0xC9, 0xD3, 0xFF));
                p.Rect(2, 2, N - 3, N - 3, new Color32(0xE6, 0xE8, 0xEE, 0xFF));
            }), 4);

            // The red flag the player tapped: dark tile with a bright yellow border.
            s.HitTile = Make(Paint(p =>
            {
                p.Rect(0, 0, N - 1, N - 1, Yellow);
                p.Rect(5, 5, N - 6, N - 6, Dark);
            }), 8);

            // Hollow yellow frame for the tutorial highlight.
            s.Frame = Make(Paint(p =>
            {
                p.Rect(0, 0, N - 1, N - 1, Yellow);
                p.Rect(7, 7, N - 8, N - 8, Clear);
            }), 10);

            s.RedFlag = Make(Paint(p => p.Flag(Red)), 0);
            s.YellowFlag = Make(Paint(p => p.Flag(Yellow)), 0);
            s.WrongFlag = Make(Paint(p =>
            {
                p.Flag(Yellow);
                p.Line(10, 10, 54, 54, 4, Dark);
                p.Line(10, 54, 54, 10, 4, Dark);
            }), 0);

            s.MenuIcon = Make(Paint(p =>
            {
                p.Rect(10, 14, 54, 20, White);
                p.Rect(10, 29, 54, 35, White);
                p.Rect(10, 44, 54, 50, White);
            }), 0);

            s.FaceSmile = Make(Paint(p => { p.FaceBase(); p.Eyes(); p.Smile(); }), 0);
            s.FaceOh = Make(Paint(p => { p.FaceBase(); p.Eyes(); p.Circle(32, 21, 6, Dark); }), 0);
            s.FaceCool = Make(Paint(p =>
            {
                p.FaceBase();
                p.Rect(13, 36, 51, 39, Dark);                 // sunglasses bridge
                p.Circle(23, 38, 7, Dark);
                p.Circle(41, 38, 7, Dark);
                p.Smile();
            }), 0);
            s.FaceDead = Make(Paint(p =>
            {
                p.FaceBase();
                p.Line(18, 34, 27, 43, 2, Dark); p.Line(18, 43, 27, 34, 2, Dark);   // X eyes
                p.Line(37, 34, 46, 43, 2, Dark); p.Line(37, 43, 46, 34, 2, Dark);
                p.Rect(22, 19, 42, 22, Dark);                                     // flat mouth
            }), 0);

            s.Dot = Make(Paint(p => p.Circle(32, 32, 30, White)), 0);
            s.Ring = Make(Paint(p =>
            {
                p.Circle(32, 32, 31, White);
                p.Circle(32, 32, 24, Clear);
            }), 0);

            return s;
        }

        // Turns pixels into a sprite. border > 0 makes it 9-sliced (corners and edges keep their size when stretched).
        static Sprite Make(Color32[] pixels, int border)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "MinesweeperGenerated"
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Color32[] Paint(System.Action<Painter> draw)
        {
            var p = new Painter();
            draw(p);
            return p.Pixels;
        }

        // Tiny drawing helper. (0, 0) is the bottom-left pixel, like Texture2D.
        sealed class Painter
        {
            public readonly Color32[] Pixels = new Color32[N * N];

            void Set(int x, int y, Color32 c)
            {
                if (x >= 0 && y >= 0 && x < N && y < N) Pixels[y * N + x] = c;
            }

            public void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        Set(x, y, c);
            }

            public void Circle(int cx, int cy, float r, Color32 c)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(x, y, c);
            }

            public void RoundedRect(int x0, int y0, int x1, int y1, int radius, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        // Distance from the nearest inner corner point; inside the straight parts it is 0.
                        int dx = Mathf.Max(x0 + radius - x, 0, x - (x1 - radius));
                        int dy = Mathf.Max(y0 + radius - y, 0, y - (y1 - radius));
                        if (dx * dx + dy * dy <= radius * radius) Set(x, y, c);
                    }
            }

            // A line made of small filled circles, "half" pixels thick on each side.
            public void Line(int x0, int y0, int x1, int y1, float half, Color32 c)
            {
                int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
                for (int i = 0; i <= steps; i++)
                {
                    float t = steps == 0 ? 0 : (float)i / steps;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                    int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                    for (int oy = -(int)half; oy <= half; oy++)
                        for (int ox = -(int)half; ox <= half; ox++)
                            if (ox * ox + oy * oy <= half * half) Set(x + ox, y + oy, c);
                }
            }

            // Pole + base + rectangular cloth.
            public void Flag(Color32 cloth)
            {
                Rect(12, 6, 32, 10, Pole);      // base
                Rect(18, 8, 23, 57, Pole);      // pole
                Rect(24, 33, 54, 57, cloth);    // cloth
            }

            public void FaceBase()
            {
                Circle(32, 32, 29, Dark);
                Circle(32, 32, 26, Yellow);
            }

            public void Eyes()
            {
                Circle(23, 39, 4, Dark);
                Circle(41, 39, 4, Dark);
            }

            // Lower half of a ring = smile.
            public void Smile()
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x - 32) * (x - 32) + (y - 30) * (y - 30));
                        if (d >= 10 && d <= 14 && y < 27) Set(x, y, Dark);
                    }
            }
        }
    }
}
