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
            public string Name; public int Width, Height, TopInset, BottomInset; public string Alert; public bool ResetDialog, Menu, GameOver, Settings, Russian, Best, Timed, ZeroOver; public int HowTo, Scores, Special;
            public Shot(string name, int w, int h, int top, int bottom, string alert = null, bool resetDialog = false, bool menu = false, int howTo = 0, bool gameOver = false, int scores = 0, bool settings = false, bool russian = false, bool best = false, bool timed = false, bool zeroOver = false, int special = 0) { Special = special; Timed = timed; ZeroOver = zeroOver; Best = best; HowTo = howTo; GameOver = gameOver; Scores = scores; Name = name; Width = w; Height = h; TopInset = top; BottomInset = bottom; Alert = alert; ResetDialog = resetDialog; Menu = menu; Settings = settings; Russian = russian; }
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
            // L04: passing the best score (the crown, "New best!" and the fireworks half a second in)
            new Shot("best_1080x1920", 1080, 1920, 0, 0, best: true),
            new Shot("best_1080x2400_notch", 1080, 2400, 132, 63, best: true),
            new Shot("best-ru_1080x1920", 1080, 1920, 0, 0, best: true, russian: true),
            // L09: Timed mode (the clock at the top, Reset alone), the menu badges, the 0-point pop-up
            new Shot("timed_1080x1920", 1080, 1920, 0, 0, timed: true),
            new Shot("timed_1080x2400_notch", 1080, 2400, 132, 63, timed: true),
            new Shot("timed_iphone-18-pro_1206x2622", 1206, 2622, 177, 102, timed: true),
            new Shot("timed-ru_1080x1920", 1080, 1920, 0, 0, timed: true, russian: true),
            new Shot("gameover-zero_1080x1920", 1080, 1920, 0, 0, zeroOver: true),
            new Shot("gameover-zero-ru_1080x2400_notch", 1080, 2400, 132, 63, zeroOver: true, russian: true),
            // L14–L19: the special mushrooms, their tutorials, Pause (special: 1 board, 2 aiming, 3 the puffball tutorial,
            // 4 the rainbow announcement, 5 the rescue, 6 Pause, 7 the banner and the "not yet" bubble, 8 a frozen tap)
            new Shot("specials_1080x1920", 1080, 1920, 0, 0, special: 1),
            new Shot("specials-aim_1080x2400_notch", 1080, 2400, 132, 63, special: 2),
            new Shot("specials-tutorial-aim_1080x1920", 1080, 1920, 0, 0, special: 3),
            new Shot("specials-tutorial-aim-ru_1080x2400_notch", 1080, 2400, 132, 63, special: 3, russian: true),
            new Shot("specials-rainbow_1080x1920", 1080, 1920, 0, 0, special: 4),
            new Shot("specials-rescue_1080x2400_notch", 1080, 2400, 132, 63, special: 5),
            new Shot("specials-rescue-ru_1080x1920", 1080, 1920, 0, 0, special: 5, russian: true),
            new Shot("specials-pause_1080x1920", 1080, 1920, 0, 0, special: 6),
            new Shot("specials-banner_1080x1920", 1080, 1920, 0, 0, special: 7),
            new Shot("specials-frozen-tap-ru_1080x1920", 1080, 1920, 0, 0, special: 8, russian: true),
            new Shot("howto-5_1080x1920", 1080, 1920, 0, 0, howTo: 5),
            new Shot("howto-6_1080x1920", 1080, 1920, 0, 0, howTo: 6),
            new Shot("howto-7_1080x1920", 1080, 1920, 0, 0, howTo: 7),
            new Shot("howto-7-ru_iphone-se_750x1334", 750, 1334, 0, 0, howTo: 7, russian: true),
            new Shot("menu-timed_1080x1920", 1080, 1920, 0, 0, menu: true, timed: true),
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
                if (shot.Timed) menu.Show(120, PieceSet.Mushrooms(), timed: true, timeLeft: 80f); // L18: Continue's stopwatch
                else menu.Show(120, PieceSet.Mushrooms());
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
            if (shot.Timed)
            {
                hud.SetTimed(true);
                hud.SetClock(24.3f, running: true); // the last 30 s: yellow
                hud.ShowTimeAdded(5f);
            }
            if (shot.ZeroOver)
            {
                hud.SetScore(0);
                hud.GameOver.Show(0, GC26.Lines.UI.Texts.Get("player") + "3");
            }
            if (shot.Best)
            {
                hud.SetScore(1140);
                hud.SetCrown(true, pop: true);
                hud.ShowNewBest();
                var fireworks = Fireworks.Create(camera);
                fireworks.Play();
                for (int i = 0; i < 30; i++) fireworks.Advance(0.02f);
            }
            if (shot.Special > 0) Specials(shot, layout, camera, view, hud);
            Tween.Tick(1f); // pop-ups and messages fully faded in
            Canvas.ForceUpdateCanvases();
            Finish(shot, camera, path);
        }

        // L14–L19: a board with a rainbow, frozen mushrooms (on the board, as a ghost and in NEXT) and a puffball ready.
        static void Specials(Shot shot, BoardLayout layout, Camera camera, BoardView view, GC26.Lines.UI.HudView hud)
        {
            var board = DemoBoard(GameState.NewGame(7).Board);
            foreach (var (cell, color, ice) in new[] { (new Cell(4, 4), BallColor.Rainbow, false), (new Cell(2, 3), BallColor.Blue, true),
                         (new Cell(6, 5), BallColor.Yellow, true), (new Cell(3, 4), BallColor.Green, false), (new Cell(5, 4), BallColor.Green, false) })
                if (board.IsEmpty(cell)) board.Place(cell, color, ice);
            var next = new Preview(new[] { BallColor.Rainbow, BallColor.Cyan, BallColor.Red }, new[] { new Cell(1, 1), new Cell(7, 2), new Cell(6, 7) },
                new[] { false, true, false });
            view.ShowBoard(board);
            view.ShowPreviewDots(next, board);
            hud.SetScore(312);
            hud.SetPreview(next.Colors, next.Frozen, view.Pieces);
            hud.SetPuffballs(shot.Special == 7 ? 0 : 1, shot.Special == 7 ? 0.56f : 1f);
            var coach = GC26.Lines.UI.CoachMarks.Create(camera);
            coach.ApplyLayout(new Vector2(shot.Width, shot.Height), layout.Unit);
            Vector2 CellCentre(Cell c) => layout.WorldToScreen(layout.BoardCenterWorld + (Vector2)BoardView.CellToLocal(c));
            var puffball = hud.Puffball.Rect;
            var puffballRect = new Rect(puffball.anchoredPosition - puffball.sizeDelta / 2f, puffball.sizeDelta);
            string T(string key) => GC26.Lines.UI.Texts.Get(key);
            switch (shot.Special)
            {
                case 2:
                    view.ShowAim(new Cell(4, 4));
                    hud.Puffball.SetArmed(true);
                    hud.Bubble.Show(T("aimTitle"), T("aimText"));
                    break;
                case 3:
                    view.ShowAim(new Cell(4, 4));
                    coach.Show(layout.Board, false, CellCentre(new Cell(4, 4)), T("coachAimTitle"), T("coachSuper2"), false, true, captionAbove: true);
                    break;
                case 4:
                {
                    var rt = hud.NextPieces[0].rectTransform;
                    var hole = new Rect(layout.Hud.position + rt.anchoredPosition, rt.sizeDelta);
                    coach.Show(hole, false, hole.center, T("coachRainbow"), "", true, false);
                    break;
                }
                case 5:
                    coach.Show(puffballRect, true, puffballRect.center, T("rescueNoMoves"), T("rescueSub"), false, true, T("endGame"));
                    hud.Puffball.SetBadgeOnTop(true); // as the game does while the button is lit
                    break;
                case 6:
                    hud.SetTimed(true);
                    hud.SetClock(84f, running: true);
                    view.SetPiecesHidden(true);
                    hud.SetNextHidden(true);
                    hud.Dialog.ShowSingle(T("paused"), T("pausedText"), T("resume"), null);
                    break;
                case 7:
                    hud.Banner.Show();
                    Tween.Tick(0.6f);
                    hud.Bubble.Show(GC26.Lines.UI.Texts.PuffballIn(88), T("superRule"), 0.56f);
                    break;
                case 8:
                    hud.ShowAlert(T("frozen"), T("frozenHint"));
                    break;
            }
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
