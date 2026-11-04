using System;
using System.IO;
using Lines.Core;
using Lines.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lines.EditorTools
{
    /// <summary>
    /// Renders the game screen at given phone sizes to PNG files, without building the app.
    /// Coloured boxes mark the areas reserved for parts not built yet (score, buttons, back button)
    /// and the phone's unsafe areas (notch, home bar).
    /// Run from the command line (no -nographics, it needs the GPU):
    ///   Unity -batchmode -projectPath . -executeMethod Lines.EditorTools.LayoutScreenshots.Run -quit -shotsDir &lt;folder&gt;
    /// </summary>
    public static class LayoutScreenshots
    {
        struct Shot
        {
            public string Name; public int Width, Height, TopInset, BottomInset;
            public Shot(string name, int w, int h, int top, int bottom) { Name = name; Width = w; Height = h; TopInset = top; BottomInset = bottom; }
        }

        static readonly Shot[] Shots =
        {
            new Shot("16x9_1080x1920", 1080, 1920, 0, 0),
            new Shot("20x9_1080x2400_notch", 1080, 2400, 132, 63),
            new Shot("iphone-se_750x1334", 750, 1334, 0, 0),
            new Shot("iphone-18-pro_1206x2622", 1206, 2622, 177, 102),
        };

        [MenuItem("Lines/Render Layout Screenshots")]
        public static void Run()
        {
            string folder = ArgumentAfter("-shotsDir") ?? Path.Combine("Temp", "LayoutScreenshots");
            Directory.CreateDirectory(folder);
            foreach (var shot in Shots)
            {
                var path = Path.Combine(folder, shot.Name + ".png");
                Render(shot, path);
                Debug.Log("Lines: wrote " + path);
            }
        }

        static void Render(Shot shot, string path)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var safe = new Rect(0, shot.BottomInset, shot.Width, shot.Height - shot.TopInset - shot.BottomInset);
            var layout = BoardLayout.Compute(new Vector2(shot.Width, shot.Height), safe);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            CameraSetup.Apply(camera, layout);

            var view = BoardView.Create();
            view.ApplyLayout(layout);
            view.SetPieces(PieceSet.RandomCreatures(7));
            view.ShowBoard(DemoBoard());

            Zone(layout, layout.Hud, new Color(0.23f, 0.43f, 0.88f, 0.22f));
            Zone(layout, layout.Actions, new Color(0.23f, 0.65f, 0.34f, 0.25f));
            Zone(layout, layout.BackButton, new Color(0.95f, 0.52f, 0.12f, 0.35f));
            if (shot.TopInset > 0) Zone(layout, new Rect(0, shot.Height - shot.TopInset, shot.Width, shot.TopInset), new Color(0, 0, 0, 0.18f));
            if (shot.BottomInset > 0) Zone(layout, new Rect(0, 0, shot.Width, shot.BottomInset), new Color(0, 0, 0, 0.18f));

            var target = new RenderTexture(shot.Width, shot.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            // The first render after creating new sprite textures can show stale textures; render twice.
            camera.Render();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(shot.Width, shot.Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, shot.Width, shot.Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }

        /// <summary>A started game plus one ball of each color, so all 7 colors show.</summary>
        static Board DemoBoard()
        {
            var board = GameState.NewGame(7).Board;
            for (int i = 0; i < BallColors.All.Length; i++)
            {
                var cell = new Cell(i + 1, 8);
                if (board.IsEmpty(cell)) board.Place(cell, BallColors.All[i]);
            }
            return board;
        }

        static void Zone(BoardLayout layout, Rect pixels, Color color)
        {
            var sr = new GameObject("Zone").AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Pixel();
            sr.color = color;
            sr.sortingOrder = 10;
            sr.transform.position = layout.ScreenToWorld(pixels.center);
            sr.transform.localScale = new Vector3(pixels.width / layout.CellPitch, pixels.height / layout.CellPitch, 1f);
        }

        static string ArgumentAfter(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
