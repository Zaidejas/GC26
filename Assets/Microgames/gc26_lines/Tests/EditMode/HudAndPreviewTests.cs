using System.Collections.Generic;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class HudAndPreviewTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        Camera camera;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            created.Add(camera.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        HudView NewHud() { var hud = HudView.Create(camera); created.Add(hud.gameObject); return hud; }
        BoardView NewBoard() { var view = BoardView.Create(); created.Add(view.gameObject); return view; }

        static BoardLayout Layout() => BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920));

        // ---- HUD ----

        [Test]
        public void Score_ShowsNumber_AndGainFloatsThenFades()
        {
            var hud = NewHud();
            hud.ApplyLayout(Layout());
            hud.SetScore(20);
            Assert.AreEqual("20", hud.ScoreText);

            hud.AddScore(40);
            Assert.AreEqual("60", hud.ScoreText);
            for (int i = 0; i < 20; i++) Tween.Tick(0.01f);
            var gain = hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>().First(t => t.name == "Gain");
            Assert.AreEqual("+40", gain.text);
            Assert.Greater(gain.alpha, 0.9f, "Visible at 0.2 s");
            for (int i = 0; i < 80; i++) Tween.Tick(0.01f);
            Assert.AreEqual(0f, gain.alpha, 0.01f, "Faded out by 0.9 s");
        }

        [Test]
        public void Next_ShowsThePreviewPieces()
        {
            var hud = NewHud();
            var pieces = PieceSet.Balls();
            var colors = new[] { BallColor.Green, BallColor.Red, BallColor.Green };

            hud.SetPreview(colors, pieces);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(hud.NextPieces[i].enabled);
                Assert.AreSame(pieces.SpriteFor(colors[i]), hud.NextPieces[i].sprite);
            }
        }

        [Test]
        public void Hud_SitsInTheLayoutsHudArea()
        {
            var hud = NewHud();
            var layout = Layout();
            hud.ApplyLayout(layout);
            var area = (RectTransform)hud.transform.Find("Area");
            Assert.AreEqual(layout.Hud.position, area.anchoredPosition);
            Assert.AreEqual(layout.Hud.size, area.sizeDelta);
        }

        // ---- Preview dots ----

        [Test]
        public void Dots_InEmptyPreviewCells_SkipOccupiedOnes()
        {
            var view = NewBoard();
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "....R....",
                ".........",
                ".........",
                ".........",
                ".........");
            var preview = new Preview(new[] { BallColor.Blue, BallColor.Green, BallColor.Red },
                new[] { new Cell(0, 0), new Cell(4, 4), new Cell(8, 8) });

            view.ShowPreviewDots(preview, board);

            CollectionAssert.AreEquivalent(new[] { new Cell(0, 0), new Cell(8, 8) }, view.DotCells.ToList());
            view.HideDotAt(new Cell(0, 0));
            CollectionAssert.AreEquivalent(new[] { new Cell(8, 8) }, view.DotCells.ToList());
        }

        // ---- The task's "done when": the preview always matches what spawns ----

        [Test]
        public void Preview_AlwaysMatchesTheBallsThatSpawn()
        {
            var view = NewBoard();
            var hud = NewHud();
            var presenter = new BoardPresenter(view, hud);
            var controller = new LinesGameController(GameState.NewGame(2026), presenter);
            var game = controller.Game;
            var random = new GameRandom(5);
            int spawnsChecked = 0;

            for (int turn = 0; turn < 40 && controller.State != TurnState.GameOver; turn++)
            {
                // What the player sees before the move.
                var shownSprites = hud.NextPieces.Select(i => i.sprite).ToList();
                var shownDots = view.DotCells.ToList();
                var preview = game.Preview;
                var emptyBefore = new HashSet<Cell>(game.Board.EmptyCells());

                var spawned = new List<(Cell cell, BallColor color)>();
                System.Action<IReadOnlyList<Cell>> onSpawn = cells => spawned.AddRange(cells.Select(c => (c, game.Board.Get(c))));
                game.OnBallsSpawned += onSpawn;

                var (from, to) = RandomLegalMove(game.Board, random);
                controller.Tap(from);
                controller.Tap(to);
                for (int i = 0; i < 200 && !controller.AcceptsInput && controller.State != TurnState.GameOver; i++) Tween.Tick(0.02f);
                game.OnBallsSpawned -= onSpawn;

                // The HUD showed exactly the preview's pieces, and the dots its free cells.
                CollectionAssert.AreEqual(preview.Colors.Select(c => view.Pieces.SpriteFor(c)), shownSprites);
                CollectionAssert.AreEquivalent(preview.Cells.Where(emptyBefore.Contains), shownDots);

                if (spawned.Count == 0) continue; // the move cleared a line: no spawn this turn
                spawnsChecked++;
                for (int k = 0; k < spawned.Count; k++)
                {
                    Assert.AreEqual(preview.Colors[k], spawned[k].color, $"Turn {turn}: ball {k} has the previewed color");
                    if (preview.Cells[k] != to)
                        Assert.AreEqual(preview.Cells[k], spawned[k].cell, $"Turn {turn}: ball {k} lands on its dot");
                }
            }
            Assert.Greater(spawnsChecked, 5);
        }

        static (Cell from, Cell to) RandomLegalMove(Board board, GameRandom random)
        {
            var balls = Enumerable.Range(0, 81).Select(i => new Cell(i % 9, i / 9)).Where(c => !board.IsEmpty(c)).ToList();
            var empty = board.EmptyCells();
            for (int attempt = 0; attempt < 500; attempt++)
            {
                var from = balls[random.Next(balls.Count)];
                var to = empty[random.Next(empty.Count)];
                if (Pathfinder.HasPath(board, from, to)) return (from, to);
            }
            foreach (var from in balls)
            foreach (var to in empty)
                if (Pathfinder.HasPath(board, from, to)) return (from, to);
            throw new AssertionException("No legal move");
        }
    }
}
