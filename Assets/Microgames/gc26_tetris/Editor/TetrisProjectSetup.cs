using System;
using System.Collections.Generic;
using System.Linq;
using GC26.Tetris.Logic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GC26.Tetris.EditorTools
{
    /// <summary>Generates the scene, checks the rules and builds a standalone player, from the menu or batch mode.</summary>
    public static class TetrisProjectSetup
    {
        const string ScenePath = "Assets/Microgames/gc26_tetris/Scenes/gc26_tetris_Main.unity";
        const string BuildPath = "Build/BlockDrop.exe";

        [MenuItem("GC26 Tetris/Create Scene")]
        public static void CreateScene()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // No MainCamera tag: inside the GC26 shell, Camera.main belongs to the shell.
            var cameraObject = new GameObject("gc26_tetris Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 11f;
            camera.depth = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x12, 0x0F, 0x1A, 0xFF);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var controllerObject = new GameObject("TetrisGame");
            var controller = controllerObject.AddComponent<TetrisGame>();
            var serialized = new SerializedObject(controller);
            // A serialized reference guarantees the sprite shader ships in the build.
            serialized.FindProperty("spriteMaterial").objectReferenceValue =
                AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            serialized.FindProperty("gameCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Build Settings belong to main (GC26 -> Sync Microgames), so they are not touched here.
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>
        /// Standalone test build of this game only. Player settings are set for the build and put back afterwards,
        /// because ProjectSettings belong to main. Company and product stay "BlockDrop" so records keep their place.
        /// </summary>
        [MenuItem("GC26 Tetris/Build Windows")]
        public static void BuildWindows()
        {
            string company = PlayerSettings.companyName;
            string product = PlayerSettings.productName;
            FullScreenMode screenMode = PlayerSettings.fullScreenMode;
            int width = PlayerSettings.defaultScreenWidth;
            int height = PlayerSettings.defaultScreenHeight;
            bool resizable = PlayerSettings.resizableWindow;
            bool background = PlayerSettings.runInBackground;
            try
            {
                PlayerSettings.companyName = "BlockDrop";
                PlayerSettings.productName = "BlockDrop";
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1000;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = BuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log("[BlockDrop] Build result: " + report.summary.result);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("Build failed: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                PlayerSettings.fullScreenMode = screenMode;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.resizableWindow = resizable;
                PlayerSettings.runInBackground = background;
            }
        }

        /// <summary>Batch-mode entry: exit code 0 only if tests and build both pass.</summary>
        public static void RunAll()
        {
            try
            {
                TetrisRulesTests.RunOrThrow();
                CreateScene();
                BuildWindows();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[BlockDrop] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }
    }

    public static class TetrisRulesTests
    {
        [MenuItem("GC26 Tetris/Run Rules Tests")]
        public static void RunOrThrow()
        {
            var tests = new Dictionary<string, Action>
            {
                { "Bag deals all seven pieces", BagDealsAllPieces },
                { "Rotations keep four cells inside the box", RotationsStayInBox },
                { "Four rotations return to the start", FourRotationsReturn },
                { "Full rows clear and upper rows fall", FullRowsClear },
                { "Walls stop movement", WallsStopMovement },
                { "Hard drop lands on the floor and scores", HardDropScores },
                { "Rotation kicks off a wall", RotationKicksOffWall },
                { "Hold works once per piece", HoldOncePerPiece },
                { "Gravity locks a piece after the lock delay", GravityLocks },
                { "Pause stops gravity", PauseStopsGravity },
                { "Soft drop falls two rows per step", SoftDropStepsTwoRows },
                { "Clearing a line through play scores it", LineClearScores },
                { "Stacking to the top ends the game", StackingEndsGame },
                { "Combo multiplier grows and caps", ComboMultiplierSteps },
                { "Clears in a row score with the combo", ClearsInARowScore },
                { "A lock without a clear resets the combo", NonClearResetsCombo },
                { "Hold keeps the combo", HoldKeepsCombo },
                { "Clear event reports points and rows", ClearEventReports },
                { "Reaching ten lines raises the level once", LevelChangedFires },
                { "Hard drop event gives start and landing rows", HardDropEvent },
                { "Soft drop is still two rows every 0.25 s", SoftDropRateKept },
                { "Every sound exists and does not clip", SoundsAreValid },
                { "Special deck: late start, steady gaps, all kinds", SpecialDeckRhythm },
                { "Same seed deals the same specials", SpecialDeckDeterministic },
                { "Special pieces can be turned off", SpecialsCanBeOff },
                { "Bomb removes 5x5 and the stack falls in", BombExplodes },
                { "Bomb at the wall stays on the board", BombAtWall },
                { "Lightning empties one column only", LightningRemovesColumn },
                { "A special piece can clear lines too", SpecialClearsLines },
                { "A special piece keeps the combo", SpecialKeepsCombo },
                { "Hold keeps the special on the piece", HoldKeepsSpecial },
                { "Marathon has no line limit", MarathonKeepsGoing },
                { "Table can keep the five lowest", TableKeepsLowestTimes },
                { "Score table keeps the five highest", TableKeepsHighestScores },
                { "A tie goes below the older result", TableTies },
                { "Table refuses impossible results", TableRefusesBadValues },
                { "Table survives saving and reloading", TableRoundTrip },
                { "Damaged saved table does not break", TableIgnoresGarbage },
                { "More lines and a higher combo hit harder", BattleDamageScales },
                { "A longer combo drops more coins, up to a limit", BattleCoinsScale },
                { "A hit lowers the bot's health and adds coins", BattleHitApplies },
                { "A defeated bot pays out and a stronger one comes", BattleDefeatAdvances },
                { "No lines means no damage and no coins", BattleEmptyHit },
                { "Battle games play on like a marathon", BattleModeKeepsGoing },
                { "Hits and defeated bots give bonus points", BattlePoints },
                { "Bonus score adds up, but not after game over", BonusScore },
                { "Buying a booster costs coins and counts it", ShopBuysBooster },
                { "No coins, no purchase", ShopNeedsCoins },
                { "Boosters stop at the limit", ShopBoosterLimit },
                { "A skin is bought once, then put on for free", ShopSkinOnce },
                { "A battle uses one of each booster", ShopTakesBoosters },
                { "Shop survives saving, and damaged saves don't break it", ShopRoundTrip },
                { "Boosters raise damage and coins", BattleBoostsApply },
            };

            int failures = 0;
            foreach (var test in tests)
            {
                try
                {
                    test.Value();
                    Debug.Log("[BlockDrop] PASS " + test.Key);
                }
                catch (Exception e)
                {
                    failures++;
                    Debug.LogError("[BlockDrop] FAIL " + test.Key + ": " + e.Message);
                }
            }
            Debug.Log("[BlockDrop] Tests: " + (tests.Count - failures) + "/" + tests.Count + " passed");
            if (failures > 0)
                throw new Exception(failures + " rules test(s) failed");
        }

        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        static void BagDealsAllPieces()
        {
            var bag = new PieceBag(1);
            for (int round = 0; round < 3; round++)
            {
                var seen = new HashSet<PieceType>();
                for (int i = 0; i < PieceShapes.PieceCount; i++)
                    seen.Add(bag.Take());
                Check(seen.Count == PieceShapes.PieceCount, "round " + round + " had " + seen.Count + " kinds");
            }
        }

        static void RotationsStayInBox()
        {
            foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
            {
                int size = PieceShapes.BoxSize(type);
                for (int r = 0; r < PieceShapes.RotationCount; r++)
                {
                    Cell[] cells = PieceShapes.Get(type, r);
                    Check(cells.Length == 4, type + " has " + cells.Length + " cells");
                    Check(cells.All(c => c.X >= 0 && c.X < size && c.Y >= 0 && c.Y < size), type + " left its box");
                    Check(cells.Select(c => c.X * 10 + c.Y).Distinct().Count() == 4, type + " has duplicate cells");
                }
            }
        }

        static void FourRotationsReturn()
        {
            var game = new TetrisMatch(3);
            PieceType type = game.CurrentType;
            int x = game.CurrentX;
            int y = game.CurrentY;
            for (int i = 0; i < 4; i++)
                game.Rotate(1);
            Check(game.CurrentType == type && game.CurrentRotation == 0, "rotation did not return to 0");
            Check(game.CurrentX == x && game.CurrentY == y, "open-field rotation moved the piece");
        }

        static void FullRowsClear()
        {
            var board = new Board();
            for (int x = 0; x < Board.Width; x++)
                board.Set(x, 0, 0);
            board.Set(3, 1, 2);
            List<int> cleared = board.ClearFullRows();
            Check(cleared.Count == 1 && cleared[0] == 0, "wrong rows cleared");
            Check(board.Get(3, 0) == 2, "upper row did not fall");
            Check(board.Get(3, 1) == Board.Empty, "old row was not emptied");
        }

        static void WallsStopMovement()
        {
            var game = new TetrisMatch(4);
            for (int i = 0; i < 20; i++)
                game.Move(-1);
            foreach (Cell c in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
                Check(game.CurrentX + c.X >= 0, "piece passed the left wall");
            Check(!game.Move(-1), "move past the wall reported success");
        }

        static void HardDropScores()
        {
            var game = new TetrisMatch(5);
            PieceType type = game.CurrentType;
            int distance = game.CurrentY - game.GhostY;
            Cell[] cells = PieceShapes.Get(type, 0);
            int x = game.CurrentX;
            int y = game.GhostY;
            game.HardDrop();
            Check(game.Score == distance * 2, "score " + game.Score + ", expected " + distance * 2);
            Check(cells.Min(c => c.Y) + y == 0, "piece did not reach the floor");
            foreach (Cell c in cells)
                Check(game.Board.Get(x + c.X, y + c.Y) == (int)type, "cell was not written to the board");
        }

        static void RotationKicksOffWall()
        {
            // An upright bar against the wall has to shift sideways to lie flat.
            TetrisMatch game = GameWith(PieceType.I);
            game.Rotate(1);
            for (int i = 0; i < 20; i++)
                game.Move(1);
            Check(game.Rotate(1), "rotation against the wall failed");
            foreach (Cell c in PieceShapes.Get(game.CurrentType, game.CurrentRotation))
            {
                int x = game.CurrentX + c.X;
                Check(x >= 0 && x < Board.Width, "kick left the piece outside the board");
            }
        }

        static void HoldOncePerPiece()
        {
            var game = new TetrisMatch(6);
            PieceType first = game.CurrentType;
            Check(game.Hold(), "first hold refused");
            Check(game.HeldType == first, "held the wrong piece");
            Check(!game.Hold(), "second hold allowed for the same piece");
            game.HardDrop();
            Check(game.CanHold, "hold not restored after locking");
        }

        static void GravityLocks()
        {
            var game = new TetrisMatch(7);
            PieceType first = game.CurrentType;
            int locks = 0;
            game.PieceLocked += _ => locks++;
            for (int i = 0; i < 2000 && locks == 0; i++)
                game.Tick(0.02f, false);
            Check(locks == 1, "piece never locked");
            Check(game.Board.Get(0, 0) != Board.Empty || HasBlocks(game.Board), "nothing on the board, was " + first);
        }

        static void PauseStopsGravity()
        {
            var game = new TetrisMatch(8);
            int y = game.CurrentY;
            game.TogglePause();
            for (int i = 0; i < 100; i++)
                game.Tick(0.1f, false);
            Check(game.CurrentY == y, "piece moved during pause");
            Check(!game.Move(1) && !game.Rotate(1), "commands accepted during pause");
            game.TogglePause();
            Check(game.State == GameState.Playing, "did not resume");
        }

        static void SoftDropStepsTwoRows()
        {
            var game = new TetrisMatch(10);
            int y = game.CurrentY;

            game.Tick(0.1f, true);
            Check(game.CurrentY == y, "moved before the step was due");
            game.Tick(0.16f, true);
            Check(game.CurrentY == y - 2, "first step moved to " + (y - game.CurrentY) + " rows");
            Check(game.Score == 2, "score " + game.Score);

            // Pressing the key after a long wait must still give a single step.
            game = new TetrisMatch(10);
            y = game.CurrentY;
            game.Tick(0.8f, false);
            game.Tick(0.02f, true);
            Check(game.CurrentY == y - 2, "saved-up time gave " + (y - game.CurrentY) + " rows");
        }

        static void LineClearScores()
        {
            TetrisMatch game = GameWith(PieceType.O);
            for (int x = 0; x < Board.Width; x++)
            {
                if (x != 4 && x != 5)
                {
                    game.Board.Set(x, 0, 0);
                    game.Board.Set(x, 1, 0);
                }
            }
            int cleared = 0;
            game.RowsCleared += rows => cleared = rows.Count;
            Check(game.CurrentType == PieceType.O && game.CurrentX == 4, "O did not spawn over the gap");
            int dropScore = (game.CurrentY - game.GhostY) * 2;
            game.HardDrop();
            Check(cleared == 2, "cleared " + cleared + " rows");
            Check(game.Lines == 2, "line counter " + game.Lines);
            Check(game.Score == dropScore + 300, "score " + game.Score);
            Check(!HasBlocks(game.Board), "board should be empty after the clear");
        }

        static void StackingEndsGame()
        {
            var game = new TetrisMatch(9);
            bool ended = false;
            game.GameEnded += () => ended = true;
            for (int i = 0; i < 200 && game.State == GameState.Playing; i++)
                game.HardDrop();
            Check(ended && game.State == GameState.GameOver, "game never ended");
            int score = game.Score;
            game.HardDrop();
            Check(game.Score == score, "commands accepted after game over");
        }

        static void ComboMultiplierSteps()
        {
            Check(TetrisMatch.ComboMultiplier(0) == 1f, "no combo");
            Check(TetrisMatch.ComboMultiplier(1) == 1f, "first clear must score x1");
            Check(TetrisMatch.ComboMultiplier(2) == 1.5f, "second clear");
            Check(TetrisMatch.ComboMultiplier(3) == 2f, "third clear");
            Check(TetrisMatch.ComboMultiplier(7) == 4f, "reaches the cap");
            Check(TetrisMatch.ComboMultiplier(50) == 4f, "stays at the cap");
        }

        // Fills the rows the current piece lands in, leaving its own cells empty, so a hard drop clears them.
        static int PrepareClear(TetrisMatch game)
        {
            Cell[] shape = PieceShapes.Get(game.CurrentType, game.CurrentRotation);
            int landing = game.GhostY;
            // A piece can only pass through a filled row whose gap covers every part of it below that row.
            var below = new HashSet<int>();
            int count = 0;
            foreach (int relativeY in shape.Select(c => c.Y).Distinct().OrderBy(y => y))
            {
                var mask = new HashSet<int>(shape.Where(c => c.Y == relativeY).Select(c => c.X));
                if (!below.IsSubsetOf(mask))
                    break;
                below.UnionWith(mask);
                int row = landing + relativeY;
                for (int x = 0; x < Board.Width; x++)
                    game.Board.Set(x, row, mask.Contains(x - game.CurrentX) ? Board.Empty : 0);
                count++;
            }
            return count;
        }

        static int LinePoints(int rows)
        {
            return new[] { 0, 100, 300, 500, 800 }[rows];
        }

        static void ClearsInARowScore()
        {
            var game = new TetrisMatch(21);
            for (int i = 1; i <= 3; i++)
            {
                int rows = PrepareClear(game);
                int level = game.Level;
                int drop = (game.CurrentY - game.GhostY) * 2;
                int before = game.Score;
                game.HardDrop();
                int expected = drop + (int)Math.Round(LinePoints(rows) * level * TetrisMatch.ComboMultiplier(i));
                Check(game.Combo == i, "combo " + game.Combo + " after clear " + i);
                Check(game.Score - before == expected, "clear " + i + " gave " + (game.Score - before) + ", expected " + expected);
            }
        }

        static void NonClearResetsCombo()
        {
            var game = new TetrisMatch(22);
            PrepareClear(game);
            game.HardDrop();
            Check(game.Combo == 1, "combo did not start");
            game.HardDrop();
            Check(game.Combo == 0, "combo survived a lock without a clear");
        }

        static void HoldKeepsCombo()
        {
            var game = new TetrisMatch(23);
            PrepareClear(game);
            game.HardDrop();
            Check(game.Hold(), "hold refused");
            Check(game.Combo == 1, "hold changed the combo");
        }

        static void ClearEventReports()
        {
            var game = new TetrisMatch(24);
            LineClear seen = default;
            int events = 0;
            game.LinesCleared += clear =>
            {
                seen = clear;
                events++;
            };
            int rows = PrepareClear(game);
            game.HardDrop();
            Check(events == 1, "events " + events);
            Check(seen.Rows.Count == rows, "rows " + seen.Rows.Count);
            Check(seen.Points == LinePoints(rows), "points " + seen.Points);
            Check(seen.Combo == 1 && seen.Multiplier == 1f, "first clear combo");
        }

        static void LevelChangedFires()
        {
            var game = new TetrisMatch(25, false);
            var levels = new List<int>();
            game.LevelChanged += levels.Add;
            for (int i = 0; i < 30 && game.Lines < 12; i++)
            {
                PrepareClear(game);
                game.HardDrop();
            }
            Check(game.Lines >= 10, "lines " + game.Lines);
            Check(levels.Count == 1 && levels[0] == 2, "level events: " + string.Join(",", levels));
        }

        static void HardDropEvent()
        {
            var game = new TetrisMatch(26);
            int startY = game.CurrentY, ghostY = game.GhostY;
            int from = -1, to = -1;
            game.HardDropped += (a, b) =>
            {
                from = a;
                to = b;
            };
            game.HardDrop();
            Check(from == startY && to == ghostY, "got " + from + "->" + to + ", expected " + startY + "->" + ghostY);
        }

        static void SoftDropRateKept()
        {
            var game = new TetrisMatch(27);
            int y = game.CurrentY;
            for (int i = 0; i < 40; i++)
                game.Tick(0.025f, true);
            Check(y - game.CurrentY == 8, "1 s of soft drop moved " + (y - game.CurrentY) + " rows");
        }

        static void SoundsAreValid()
        {
            Dictionary<string, float[]> sounds = GC26.Tetris.View.GameAudio.BuildAllSounds();
            string[] required = { "move", "rotate", "hold", "lock", "harddrop", "clear1", "clear2", "clear3", "clear4", "levelup", "gameover", "music", "bomb", "zap", "bothit", "botdown", "coin" };
            foreach (string name in required)
            {
                Check(sounds.ContainsKey(name), "missing " + name);
                float peak = sounds[name].Max(Mathf.Abs);
                Check(peak > 0.05f, name + " is silent, peak " + peak);
                Check(peak <= 1f, name + " clips, peak " + peak);
            }
        }

        static void SpecialDeckRhythm()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var deck = new SpecialDeck(seed);
                int last = -1;
                var kinds = new List<SpecialKind>();
                for (int piece = 0; piece < 400; piece++)
                {
                    SpecialKind kind = deck.Next();
                    if (kind == SpecialKind.None)
                        continue;
                    if (last < 0)
                        Check(piece >= SpecialDeck.FirstAfter, "seed " + seed + ": first special at piece " + piece);
                    else
                        Check(piece - last >= SpecialDeck.MinGap && piece - last <= SpecialDeck.MaxGap, "seed " + seed + ": gap " + (piece - last));
                    last = piece;
                    kinds.Add(kind);
                }
                Check(kinds.Count >= 30, "seed " + seed + ": only " + kinds.Count + " specials");
                for (int i = 0; i + 2 <= kinds.Count; i += 2)
                    Check(kinds.Skip(i).Take(2).Distinct().Count() == 2, "seed " + seed + ": kind repeated before the bag ran out");
            }
        }

        static void SpecialDeckDeterministic()
        {
            var a = new SpecialDeck(77);
            var b = new SpecialDeck(77);
            for (int i = 0; i < 200; i++)
                Check(a.Next() == b.Next(), "differs at piece " + i);
        }

        static void SpecialsCanBeOff()
        {
            var game = new TetrisMatch(5, false);
            for (int i = 0; i < 100 && game.State == GameState.Playing; i++)
            {
                Check(game.CurrentSpecial == SpecialKind.None, "special piece dealt");
                game.HardDrop();
            }
        }

        // A stack four rows high with one free column, so no row is full.
        static void FillStack(TetrisMatch game, int height, int freeColumn)
        {
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < height; y++)
                    if (x != freeColumn)
                        game.Board.Set(x, y, 0);
        }

        static int ColumnCount(Board board, int x)
        {
            int count = 0;
            for (int y = 0; y < Board.Height; y++)
                if (board.Get(x, y) != Board.Empty)
                    count++;
            return count;
        }

        static void BombExplodes()
        {
            var game = new TetrisMatch(31, false);
            FillStack(game, 4, 9);
            game.ForceCurrentSpecial(SpecialKind.Bomb);
            Cell[] shape = PieceShapes.Get(game.CurrentType, game.CurrentRotation);
            int landing = game.GhostY;
            int centerX = game.CurrentX + shape[TetrisMatch.SpecialCellIndex].X;
            int centerY = landing + shape[TetrisMatch.SpecialCellIndex].Y;

            var before = new Board();
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++)
                    before.Set(x, y, game.Board.Get(x, y));
            foreach (Cell c in shape)
                before.Set(game.CurrentX + c.X, landing + c.Y, (int)game.CurrentType);

            int expected = 0;
            var expectedInColumn = new int[Board.Width];
            for (int x = centerX - 2; x <= centerX + 2; x++)
            {
                for (int y = Math.Max(0, centerY - 2); y <= centerY + 2; y++)
                {
                    if (before.Get(x, y) == Board.Empty)
                        continue;
                    expected++;
                    expectedInColumn[x]++;
                }
            }

            SpecialResult seen = default;
            game.SpecialTriggered += result => seen = result;
            int scoreBefore = game.Score;
            int drop = (game.CurrentY - landing) * 2;
            game.HardDrop();

            Check(seen.Kind == SpecialKind.Bomb, "no bomb event");
            Check(seen.X == centerX && seen.Y == centerY, "wrong centre");
            Check(seen.Removed.Count == expected, "removed " + seen.Removed.Count + ", expected " + expected);
            Check(game.Score - scoreBefore == drop + expected * 10, "score " + (game.Score - scoreBefore));
            for (int x = 0; x < Board.Width; x++)
            {
                Check(ColumnCount(game.Board, x) == ColumnCount(before, x) - expectedInColumn[x], "column " + x + " count");
                if (expectedInColumn[x] == 0)
                    continue;
                bool gap = false;
                for (int y = Math.Max(0, centerY - 2); y < Board.Height; y++)
                {
                    bool empty = game.Board.Get(x, y) == Board.Empty;
                    Check(empty || !gap, "column " + x + " has a floating cell at row " + y);
                    gap |= empty;
                }
            }
        }

        static void BombAtWall()
        {
            var game = new TetrisMatch(32, false);
            game.ForceCurrentSpecial(SpecialKind.Bomb);
            for (int i = 0; i < 12; i++)
                game.Move(-1);
            int removed = -1;
            game.SpecialTriggered += result => removed = result.Removed.Count;
            game.HardDrop();
            Check(removed == 4, "removed " + removed + " cells, expected the 4 of the piece");
        }

        static void LightningRemovesColumn()
        {
            var game = new TetrisMatch(33, false);
            FillStack(game, 6, 9);
            game.ForceCurrentSpecial(SpecialKind.Lightning);
            Cell[] shape = PieceShapes.Get(game.CurrentType, game.CurrentRotation);
            int column = game.CurrentX + shape[TetrisMatch.SpecialCellIndex].X;
            var expected = new int[Board.Width];
            for (int x = 0; x < Board.Width; x++)
                expected[x] = ColumnCount(game.Board, x);
            foreach (Cell c in shape)
                expected[game.CurrentX + c.X]++;

            SpecialResult seen = default;
            game.SpecialTriggered += result => seen = result;
            game.HardDrop();

            Check(seen.Kind == SpecialKind.Lightning && seen.X == column, "no lightning event");
            Check(seen.Removed.Count == expected[column], "removed " + seen.Removed.Count);
            for (int x = 0; x < Board.Width; x++)
                Check(ColumnCount(game.Board, x) == (x == column ? 0 : expected[x]), "column " + x);
        }

        static void SpecialClearsLines()
        {
            var game = new TetrisMatch(35, false);
            int rows = PrepareClear(game);
            game.ForceCurrentSpecial(SpecialKind.Bomb);
            int lines = 0, bombs = 0;
            game.LinesCleared += clear => lines = clear.Rows.Count;
            game.SpecialTriggered += result => bombs++;
            game.HardDrop();
            Check(lines == rows, "cleared " + lines + ", expected " + rows);
            Check(bombs == 1, "bomb events " + bombs);
            Check(game.Combo == 1, "combo " + game.Combo);
        }

        static void SpecialKeepsCombo()
        {
            var game = new TetrisMatch(36, false);
            PrepareClear(game);
            game.HardDrop();
            Check(game.Combo == 1, "combo did not start");
            game.ForceCurrentSpecial(SpecialKind.Lightning);
            game.HardDrop();
            Check(game.Combo == 1, "special reset the combo");
            game.HardDrop();
            Check(game.Combo == 0, "ordinary lock kept the combo");
        }

        static void HoldKeepsSpecial()
        {
            var game = new TetrisMatch(37, false);
            game.ForceCurrentSpecial(SpecialKind.Bomb);
            PieceType held = game.CurrentType;
            Check(game.Hold(), "hold refused");
            Check(game.HeldSpecial == SpecialKind.Bomb, "held special lost");
            Check(game.CurrentSpecial == SpecialKind.None, "next piece became special");
            game.HardDrop();
            Check(game.Hold(), "second hold refused");
            Check(game.CurrentType == held && game.CurrentSpecial == SpecialKind.Bomb, "special did not come back");
            Check(game.HeldSpecial == SpecialKind.None, "ordinary piece held as special");
        }

        static void MarathonKeepsGoing()
        {
            var game = new TetrisMatch(42, false);
            for (int i = 0; i < 80 && game.Lines < 45; i++)
            {
                game.Board.Clear();
                PrepareClear(game);
                game.HardDrop();
            }
            Check(game.Lines >= 45, "lines " + game.Lines);
            Check(game.State == GameState.Playing, "state " + game.State);
        }

        static void TableKeepsLowestTimes()
        {
            var table = new Leaderboard(true);
            Check(table.Add(50) == 1, "first entry");
            Check(table.Add(40) == 1, "better time takes first place");
            Check(table.Add(45) == 2, "middle time");
            table.Add(60);
            table.Add(70);
            Check(table.Entries.Count == 5, "count " + table.Entries.Count);
            Check(table.Add(80) == 0, "too slow must not be placed");
            Check(table.Entries.Count == 5 && table.Entries[4] == 70, "slow time changed the table");
            Check(table.Add(55) == 4, "placed time");
            Check(table.Entries.Count == 5 && table.Entries[4] == 60, "worst entry was not dropped");
            Check(table.Entries.SequenceEqual(new double[] { 40, 45, 50, 55, 60 }), "wrong order");
        }

        static void TableKeepsHighestScores()
        {
            var table = new Leaderboard(false);
            foreach (double score in new double[] { 100, 500, 300, 200, 400 })
                table.Add(score);
            Check(table.Entries.SequenceEqual(new double[] { 500, 400, 300, 200, 100 }), "wrong order");
            Check(table.Add(50) == 0, "too low must not be placed");
            Check(table.Add(600) == 1, "new best");
            Check(table.Entries[4] == 200, "lowest was not dropped");
        }

        static void TableTies()
        {
            var table = new Leaderboard(false);
            table.Add(100);
            Check(table.Add(100) == 2, "tie took the older place");
            for (int i = 0; i < 3; i++)
                table.Add(100);
            Check(table.Add(100) == 0, "tie with a full table must not be placed");
        }

        static void TableRefusesBadValues()
        {
            var table = new Leaderboard(true);
            Check(table.Add(0) == 0 && table.Add(-5) == 0, "zero or negative accepted");
            Check(table.Add(double.NaN) == 0 && table.Add(double.PositiveInfinity) == 0, "NaN or infinity accepted");
            Check(table.Entries.Count == 0, "bad value stored");
        }

        static void TableRoundTrip()
        {
            var table = new Leaderboard(true);
            foreach (double time in new[] { 83.456789012, 71.25, 95.1 })
                table.Add(time);
            string text = table.Serialize();
            Check(!text.Contains(","), "saved text depends on the system language: " + text);
            Leaderboard loaded = Leaderboard.Parse(text, true);
            Check(loaded.Entries.SequenceEqual(table.Entries), "changed after reload: " + text);
            Check(Leaderboard.Parse("", true).Entries.Count == 0 && Leaderboard.Parse(null, true).Entries.Count == 0, "empty text");
        }

        static void TableIgnoresGarbage()
        {
            Leaderboard loaded = Leaderboard.Parse("abc;50.5;;-3;NaN;40;1e999;x;60;70;80;90;100", true);
            Check(loaded.Entries.SequenceEqual(new double[] { 40, 50.5, 60, 70, 80 }), "got " + loaded.Serialize());
        }

        static void BattleDamageScales()
        {
            Check(Battle.DamageFor(1, 1f) == 10, "one line");
            Check(Battle.DamageFor(4, 1f) == 80, "four lines");
            Check(Battle.DamageFor(1, 2f) == 20, "combo x2 doubles");
            Check(Battle.DamageFor(4, 4f) == 320, "four lines at x4");
            Check(Battle.DamageFor(2, 1.5f) > Battle.DamageFor(2, 1f), "combo must add damage");
            for (int rows = 1; rows < 4; rows++)
                Check(Battle.DamageFor(rows + 1, 1f) > Battle.DamageFor(rows, 1f), "more lines must hurt more");
        }

        static void BattleCoinsScale()
        {
            Check(Battle.CoinsFor(1, 1) == 1, "one line, no combo");
            Check(Battle.CoinsFor(4, 1) == 4, "four lines, no combo");
            Check(Battle.CoinsFor(1, 3) == 3, "combo adds coins");
            Check(Battle.CoinsFor(1, 100) == 1 + Battle.MaxComboCoins, "combo coins must be capped");
            Check(Battle.CoinsFor(0, 5) == 0, "no lines, no coins");
        }

        static void BattleHitApplies()
        {
            var battle = new Battle();
            Check(battle.Hp == Battle.BaseHp && battle.Level == 1, "starting state");
            BattleHit hit = battle.Hit(2, 2, 1.5f);
            Check(hit.Damage == Battle.DamageFor(2, 1.5f) && !hit.Defeated && hit.BotLevel == 1, "hit result");
            Check(battle.Hp == Battle.BaseHp - hit.Damage, "health " + battle.Hp);
            Check(hit.Coins == Battle.CoinsFor(2, 2) && battle.CoinsEarned == hit.Coins, "coins");
        }

        static void BattleDefeatAdvances()
        {
            var battle = new Battle();
            int guard = 0;
            BattleHit hit = default;
            while (!hit.Defeated && guard++ < 100)
                hit = battle.Hit(1, 1, 1f);
            Check(hit.Defeated && hit.BotLevel == 1, "bot was not defeated");
            Check(guard == 12, "120 health at 10 damage took " + guard + " hits");
            Check(hit.Coins == 1 + Battle.DefeatCoinsPerLevel, "defeat coins " + hit.Coins);
            Check(battle.Level == 2 && battle.BotsDefeated == 1, "did not advance");
            Check(battle.MaxHp == Battle.BaseHp + Battle.HpPerLevel, "next bot is not stronger");
            Check(battle.Hp == battle.MaxHp, "next bot must start at full health");

            var overkill = new Battle();
            overkill.Hit(4, 7, 4f);
            Check(overkill.Level == 2 && overkill.Hp == overkill.MaxHp, "leftover damage carried over");
            Check(Battle.MaxHpFor(3) > Battle.MaxHpFor(2), "bots must keep getting stronger");
        }

        static void BattleEmptyHit()
        {
            var battle = new Battle();
            BattleHit hit = battle.Hit(0, 0, 1f);
            Check(hit.Damage == 0 && hit.Coins == 0 && !hit.Defeated, "empty hit did something");
            Check(battle.Hp == battle.MaxHp && battle.CoinsEarned == 0, "state changed");
        }

        static void BattlePoints()
        {
            var battle = new Battle();
            BattleHit hit = battle.Hit(2, 2, 1.5f);
            Check(hit.Points == hit.Damage * Battle.PointsPerDamage, "hit points " + hit.Points);

            BattleHit finisher = battle.Hit(4, 7, 4f);
            Check(finisher.Defeated, "bot survived 320 damage");
            Check(finisher.Points == finisher.Damage * Battle.PointsPerDamage + Battle.DefeatPointsPerLevel, "defeat points " + finisher.Points);

            BattleHit second = battle.Hit(4, 7, 4f);
            Check(second.Defeated && second.BotLevel == 2, "second bot");
            Check(second.Points == second.Damage * Battle.PointsPerDamage + 2 * Battle.DefeatPointsPerLevel, "stronger bots must pay more");
            Check(battle.Hit(0, 0, 1f).Points == 0, "no lines, no points");
        }

        static void BonusScore()
        {
            var game = new TetrisMatch(52, false, GameMode.Battle);
            game.AddBonusScore(150);
            game.AddBonusScore(-40);
            game.AddBonusScore(0);
            Check(game.Score == 150, "score " + game.Score);
            for (int i = 0; i < 200 && game.State == GameState.Playing; i++)
                game.HardDrop();
            Check(game.State == GameState.GameOver, "game did not end");
            int final = game.Score;
            game.AddBonusScore(1000);
            Check(game.Score == final, "score changed after game over");
        }

        static void BattleModeKeepsGoing()
        {
            var game = new TetrisMatch(51, false, GameMode.Battle);
            for (int i = 0; i < 80 && game.Lines < 45; i++)
            {
                game.Board.Clear();
                PrepareClear(game);
                game.HardDrop();
            }
            Check(game.Lines >= 45 && game.State == GameState.Playing, "battle ended early: " + game.State);
        }

        static ShopItem Booster(BoosterKind kind)
        {
            foreach (ShopItem item in TetrisShop.Items)
                if (item.Kind == ShopItemKind.Booster && item.Booster == kind)
                    return item;
            throw new Exception("no booster " + kind);
        }

        static ShopItem SkinItem(int skin)
        {
            foreach (ShopItem item in TetrisShop.Items)
                if (item.Kind == ShopItemKind.Skin && item.Skin == skin)
                    return item;
            throw new Exception("no skin " + skin);
        }

        static void ShopBuysBooster()
        {
            var shop = new TetrisShop();
            ShopItem bomb = Booster(BoosterKind.StartBomb);
            int coins = bomb.Price + 7;
            Check(shop.Buy(bomb, ref coins) == BuyResult.Bought, "should buy");
            Check(coins == 7, "price taken: " + coins);
            Check(shop.Count(BoosterKind.StartBomb) == 1, "booster counted");
        }

        static void ShopNeedsCoins()
        {
            var shop = new TetrisShop();
            ShopItem heavy = Booster(BoosterKind.HeavyHit);
            int coins = heavy.Price - 1;
            Check(shop.Buy(heavy, ref coins) == BuyResult.NotEnoughCoins, "should refuse");
            Check(coins == heavy.Price - 1 && shop.Count(BoosterKind.HeavyHit) == 0, "nothing changes");
            int few = SkinItem(3).Price - 1;
            Check(shop.Buy(SkinItem(3), ref few) == BuyResult.NotEnoughCoins && shop.Skin == 0, "skin refused too");
        }

        static void ShopBoosterLimit()
        {
            var shop = new TetrisShop();
            ShopItem magnet = Booster(BoosterKind.CoinMagnet);
            int coins = 100000;
            for (int i = 0; i < TetrisShop.MaxBoosters; i++)
                shop.Buy(magnet, ref coins);
            int before = coins;
            Check(shop.Buy(magnet, ref coins) == BuyResult.Full, "limit");
            Check(coins == before && shop.Count(BoosterKind.CoinMagnet) == TetrisShop.MaxBoosters, "no charge at the limit");
        }

        static void ShopSkinOnce()
        {
            var shop = new TetrisShop();
            ShopItem neon = SkinItem(2);
            int coins = neon.Price;
            Check(shop.Buy(neon, ref coins) == BuyResult.Bought && coins == 0 && shop.Skin == 2, "bought and worn");
            Check(shop.Buy(SkinItem(0), ref coins) == BuyResult.Equipped && shop.Skin == 0, "classic is free");
            Check(shop.Buy(neon, ref coins) == BuyResult.Equipped && coins == 0 && shop.Skin == 2, "owned skin costs nothing");
            Check(shop.Buy(neon, ref coins) == BuyResult.AlreadyEquipped, "already worn");
        }

        static void ShopTakesBoosters()
        {
            var shop = new TetrisShop();
            int coins = 100000;
            shop.Buy(Booster(BoosterKind.StartBomb), ref coins);
            shop.Buy(Booster(BoosterKind.StartBomb), ref coins);
            shop.Buy(Booster(BoosterKind.CoinMagnet), ref coins);
            BattleBoosts first = shop.TakeBoosters();
            Check(first.StartBomb && !first.HeavyHit && first.CoinMagnet, "one of each owned");
            Check(shop.Count(BoosterKind.StartBomb) == 1 && shop.Count(BoosterKind.CoinMagnet) == 0, "used up");
            BattleBoosts second = shop.TakeBoosters();
            Check(second.StartBomb && !second.CoinMagnet, "the second bomb next time");
            Check(!shop.TakeBoosters().Any, "nothing left");
        }

        static void ShopRoundTrip()
        {
            var shop = new TetrisShop();
            int coins = 100000;
            shop.Buy(Booster(BoosterKind.HeavyHit), ref coins);
            shop.Buy(Booster(BoosterKind.HeavyHit), ref coins);
            shop.Buy(SkinItem(1), ref coins);
            shop.Buy(SkinItem(3), ref coins);
            TetrisShop copy = TetrisShop.Parse(shop.Serialize());
            Check(copy.Serialize() == shop.Serialize(), "round trip: " + copy.Serialize());
            Check(copy.Skin == 3 && copy.OwnsSkin(1) && !copy.OwnsSkin(2) && copy.Count(BoosterKind.HeavyHit) == 2, "same content");

            TetrisShop broken = TetrisShop.Parse("b=x,99,-4;s=1,0;k=7;junk");
            Check(broken.Count(BoosterKind.StartBomb) == 0 && broken.Count(BoosterKind.HeavyHit) == TetrisShop.MaxBoosters, "counts clamped");
            Check(broken.Skin == 0 && broken.OwnsSkin(0), "unknown skin falls back to classic");
            Check(TetrisShop.Parse("k=2;s=1,0,1,0").Skin == 2, "order of parts doesn't matter");
            Check(TetrisShop.Parse("k=2").Skin == 0, "a skin not owned can't be worn");
        }

        static void BattleBoostsApply()
        {
            var plain = new Battle();
            var boosted = new Battle(1.5f, 2);
            BattleHit a = plain.Hit(2, 1, 1f);
            BattleHit b = boosted.Hit(2, 1, 1f);
            Check(b.Damage == (int)Math.Round(a.Damage * 1.5f), "damage x1.5: " + a.Damage + " -> " + b.Damage);
            Check(b.Coins == a.Coins * 2, "coins x2: " + a.Coins + " -> " + b.Coins);
            Check(boosted.CoinsEarned == b.Coins, "earned counts the boost");
        }

        // Finds a seed that spawns the wanted piece first.
        static TetrisMatch GameWith(PieceType type)
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                var game = new TetrisMatch(seed);
                if (game.CurrentType == type)
                    return game;
            }
            throw new Exception("no seed starts with " + type);
        }

        static bool HasBlocks(Board board)
        {
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++)
                    if (board.Get(x, y) != Board.Empty)
                        return true;
            return false;
        }
    }
}
