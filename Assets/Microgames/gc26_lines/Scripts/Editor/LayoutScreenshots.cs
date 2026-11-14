using System;
using System.IO;
using GC26.Lines.Core;
using GC26.Lines.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26.Lines.EditorTools
{
    /// <summary>
    /// Renders the game screen at given phone sizes to PNG files, without building the app.
    /// Grey bars mark the phone's unsafe areas (notch, home bar).
    /// Run from the command line (no -nographics, it needs the GPU):
    ///   Unity -batchmode -projectPath . -executeMethod GC26.Lines.EditorTools.LayoutScreenshots.Run -quit -shotsDir &lt;folder&gt;
    /// </summary>
    public static class LayoutScreenshots
    {
        struct Shot
        {
            public string Name; public int Width, Height, TopInset, BottomInset; public string Alert; public bool ResetDialog, Menu, GameOver, Settings, Russian; public int HowTo, Scores;
            public Shot(string name, int w, int h, int top, int bottom, string alert = null, bool resetDialog = false, bool menu = false, int howTo = 0, bool gameOver = false, int scores = 0, bool settings = false, bool russian = false) { HowTo = howTo; GameOver = gameOver; Scores = scores; Name = name; Width = w; Height = h; TopInset = top; BottomInset = bottom; Alert = alert; ResetDialog = resetDialog; Menu = menu; Settings = settings; Russian = russian; }
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
            new Shot("howto-1_1080x1920", 1080, 1920, 0, 0, howTo: 1),
            new Shot("howto-2_1080x1920", 1080, 1920, 0, 0, howTo: 2),
            new Shot("howto-3_1080x1920", 1080, 1920, 0, 0, howTo: 3),
            new Shot("howto-3_1080x2400_notch", 1080, 2400, 132, 63, howTo: 3),
            new Shot("howto-1_iphone-se_750x1334", 750, 1334, 0, 0, howTo: 1),
            new Shot("gameover_1080x1920", 1080, 1920, 0, 0, gameOver: true),
            new Shot("gameover_1080x2400_notch", 1080, 2400, 132, 63, gameOver: true),
            new Shot("leaderboard-empty_1080x1920", 1080, 1920, 0, 0, scores: 1),
            new Shot("leaderboard-after-game_1080x1920", 1080, 1920, 0, 0, scores: 2),
            new Shot("leaderboard-after-game_1080x2400_notch", 1080, 2400, 132, 63, scores: 2),
            new Shot("leaderboard-from-menu_1080x2400_notch", 1080, 2400, 132, 63, scores: 3),
            // Settings (sounds switched off, to show both switch states) and the Russian screens.
            new Shot("settings_1080x1920", 1080, 1920, 0, 0, settings: true),
            new Shot("settings-ru_1080x1920", 1080, 1920, 0, 0, settings: true, russian: true),
            new Shot("settings_iphone-se_750x1334", 750, 1334, 0, 0, settings: true),
            new Shot("menu-ru_1080x2400_notch", 1080, 2400, 132, 63, menu: true, russian: true),
            new Shot("16x9-ru_1080x1920", 1080, 1920, 0, 0, russian: true),
            new Shot("alert-no-path-ru_1080x2400", 1080, 2400, 132, 63, "Нет пути", russian: true),
            new Shot("dialog-reset-ru_1080x1920", 1080, 1920, 0, 0, resetDialog: true, russian: true),
            new Shot("howto-3-ru_1080x1920", 1080, 1920, 0, 0, howTo: 3, russian: true),
            new Shot("gameover-ru_1080x1920", 1080, 1920, 0, 0, gameOver: true, russian: true),
            new Shot("leaderboard-after-game-ru_1080x1920", 1080, 1920, 0, 0, scores: 2, russian: true),
            // Task 36: every screen at both target shapes (16:9 without a notch, 20:9 with one).
            new Shot("alert-no-path_1080x1920", 1080, 1920, 0, 0, "No path"),
            new Shot("dialog-reset_1080x2400_notch", 1080, 2400, 132, 63, resetDialog: true),
            new Shot("howto-1_1080x2400_notch", 1080, 2400, 132, 63, howTo: 1),
            new Shot("howto-2_1080x2400_notch", 1080, 2400, 132, 63, howTo: 2),
            new Shot("leaderboard-empty_1080x2400_notch", 1080, 2400, 132, 63, scores: 1),
            new Shot("leaderboard-from-menu_1080x1920", 1080, 1920, 0, 0, scores: 3),
            new Shot("settings_1080x2400_notch", 1080, 2400, 132, 63, settings: true),
            new Shot("settings-ru_1080x2400_notch", 1080, 2400, 132, 63, settings: true, russian: true),
            new Shot("20x9-ru_1080x2400_notch", 1080, 2400, 132, 63, russian: true),
            new Shot("menu-ru_1080x1920", 1080, 1920, 0, 0, menu: true, russian: true),
        };

        [MenuItem("Lines/Render Layout Screenshots")]
        public static void Run()
        {
            string folder = ArgumentAfter("-shotsDir") ?? Path.Combine("Temp", "LayoutScreenshots");
            Directory.CreateDirectory(folder);
            foreach (var shot in Shots)
            {
                var path = Path.Combine(folder, shot.Name + ".png");
                GC26.Lines.UI.Texts.Set(shot.Russian ? GC26.Lines.UI.Language.Russian : GC26.Lines.UI.Language.English);
                Render(shot, path);
                Debug.Log("Lines: wrote " + path);
            }
            GC26.Lines.UI.Texts.Set(GC26.Lines.UI.Language.English);
        }

        static void Render(Shot shot, string path)
        {
            Tween.KillAll(); // nothing left running from the previous shot
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var safe = new Rect(0, shot.BottomInset, shot.Width, shot.Height - shot.TopInset - shot.BottomInset);
            var layout = BoardLayout.Compute(new Vector2(shot.Width, shot.Height), safe);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            CameraSetup.Apply(camera, layout);
            // The forest behind every screen: the menu's version has the tall mushrooms.
            var forest = ForestBackground.Create(camera);
            forest.ApplyLayout(layout);
            forest.Show(menu: shot.Menu);

            if (shot.Scores > 0)
            {
                var screen = GC26.Lines.UI.LeaderboardView.Create(camera);
                screen.ApplyLayout(layout);
                var scores = new Leaderboard();
                long day = 86400000L, start = new System.DateTimeOffset(2026, 10, 20, 12, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();
                if (shot.Scores == 1) screen.Show(scores);
                else
                {
                    string[] names = { "Anna", "Mark", "Player1", "Olga", "Anna", "Tom", "Player2", "Mark", "Lena", "Anna", "Max" };
                    int[] points = { 412, 368, 300, 264, 240, 198, 176, 150, 122, 96, 70 };
                    for (int i = 0; i < names.Length - 1; i++) scores.Add(points[i], names[i], start + i * day);
                    long latest = start + 16 * day;
                    int place = scores.Add(244, "Anna", latest);
                    if (shot.Scores == 2) screen.Show(scores, latest, GC26.Lines.UI.Texts.ResultMessage(place, 244, "Anna"), showPlayAgain: true);
                    else screen.Show(scores);
                }
                Canvas.ForceUpdateCanvases();
                Finish(shot, camera, path);
                return;
            }

            if (shot.HowTo > 0)
            {
                var howTo = GC26.Lines.UI.HowToPlayView.Create(camera);
                howTo.ApplyLayout(layout);
                howTo.Show(PieceSet.Mushrooms());
                howTo.GoTo(shot.HowTo - 1);
                Canvas.ForceUpdateCanvases();
                Finish(shot, camera, path);
                return;
            }

            if (shot.Menu)
            {
                var menu = GC26.Lines.UI.MenuView.Create(camera);
                menu.ApplyLayout(layout);
                menu.Show(120, PieceSet.Mushrooms());
                menu.SetSecondaryScreensAvailable(true, true);
                Canvas.ForceUpdateCanvases();
                Finish(shot, camera, path);
                return;
            }

            var view = BoardView.Create();
            view.ApplyLayout(layout);
            view.SetPieces(PieceSet.Mushrooms());
            var game = GameState.NewGame(7);
            var board = DemoBoard(game.Board);
            view.ShowBoard(board);
            view.ShowPreviewDots(game.Preview, board);

            var hud = GC26.Lines.UI.HudView.Create(camera);
            hud.ApplyLayout(layout);
            hud.SetScore(120);
            hud.SetPreview(game.Preview.Colors, view.Pieces);
            if (shot.GameOver)
                hud.GameOver.Show(1284, GC26.Lines.UI.Texts.Get("player") + "3");
            if (shot.ResetDialog)
                hud.Dialog.Show(GC26.Lines.Game.GameBootstrap.ResetTitle, GC26.Lines.Game.GameBootstrap.ResetMessage, GC26.Lines.UI.Texts.Get("yes"), null);
            if (shot.Settings)
                hud.Settings.Show(new GameSettings { sounds = false });
            if (shot.Alert != null) hud.ShowAlert(shot.Alert);
            Tween.Tick(1f); // pop-ups and messages fully faded in
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
