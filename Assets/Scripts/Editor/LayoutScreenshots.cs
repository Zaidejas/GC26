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
    /// Grey bars mark the phone's unsafe areas (notch, home bar).
    /// Run from the command line (no -nographics, it needs the GPU):
    ///   Unity -batchmode -projectPath . -executeMethod Lines.EditorTools.LayoutScreenshots.Run -quit -shotsDir &lt;folder&gt;
    /// </summary>
    public static class LayoutScreenshots
    {
        struct Shot
        {
            public string Name; public int Width, Height, TopInset, BottomInset; public string Alert; public bool ResetDialog, Menu;
            public Shot(string name, int w, int h, int top, int bottom, string alert = null, bool resetDialog = false, bool menu = false) { Name = name; Width = w; Height = h; TopInset = top; BottomInset = bottom; Alert = alert; ResetDialog = resetDialog; Menu = menu; }
        }

        static readonly Shot[] Shots =
        {
            new Shot("16x9_1080x1920", 1080, 1920, 0, 0),
            new Shot("20x9_1080x2400_notch", 1080, 2400, 132, 63),
            new Shot("iphone-se_750x1334", 750, 1334, 0, 0),
            new Shot("iphone-18-pro_1206x2622", 1206, 2622, 177, 102),
            new Shot("alert-no-path_1080x2400", 1080, 2400, 132, 63, "No path"),
            new Shot("dialog-reset_1080x1920", 1080, 1920, 0, 0, resetDialog: true),
            new Shot("menu_1080x1920", 1080, 1920, 0, 0, menu: true),
            new Shot("menu_1080x2400_notch", 1080, 2400, 132, 63, menu: true),
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

            if (shot.Menu)
            {
                var menu = Lines.UI.MenuView.Create(camera);
                menu.ApplyLayout(layout);
                menu.Show(120, PieceSet.RandomCreatures(7));
                menu.SetSecondaryScreensAvailable(false, false);
                Canvas.ForceUpdateCanvases();
                Finish(shot, camera, path);
                return;
            }

            var view = BoardView.Create();
            view.ApplyLayout(layout);
            view.SetPieces(PieceSet.RandomCreatures(7));
            var game = GameState.NewGame(7);
            var board = DemoBoard(game.Board);
            view.ShowBoard(board);
            view.ShowPreviewDots(game.Preview, board);

            var hud = Lines.UI.HudView.Create(camera);
            hud.ApplyLayout(layout);
            hud.SetScore(120);
            hud.SetPreview(game.Preview.Colors, view.Pieces);
            if (shot.ResetDialog)
                hud.Dialog.Show(Lines.Game.GameBootstrap.ResetTitle, Lines.Game.GameBootstrap.ResetMessage, "Yes", null);
            if (shot.Alert != null)
            {
                hud.ShowAlert(shot.Alert);
                Tween.Tick(0.3f); // fully faded in
            }
            Canvas.ForceUpdateCanvases();
            Finish(shot, camera, path);
        }

        static void Finish(Shot shot, Camera camera, string path)
        {
            var layout = BoardLayout.Compute(new Vector2(shot.Width, shot.Height), new Rect(0, shot.BottomInset, shot.Width, shot.Height - shot.TopInset - shot.BottomInset));
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
        static Board DemoBoard(Board board)
        {
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
