using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// Task 39 (bug hunt): a robot plays whole games in the real scene by pressing the screen, like a
    /// careless player: random moves, moves with no path, presses while a mushroom rolls, Undo, leaving
    /// for the menu and coming back, then the game-over pop-up, the leaderboard and Play again. After every
    /// turn the screen must show exactly the real board and score, and nothing may log an error (an error or
    /// an exception in the log fails the test by itself). For its first 50 turns of a game it mostly builds
    /// lines (so lines get cleared too), then it plays at random until the board fills up.
    /// </summary>
    public class RobotPlayModeTests : PlayModeTestBase
    {
        const float Speed = 6f; // the animations run 6× faster, so whole games fit in a short test

        [TearDown]
        public void NormalSpeed() => Time.timeScale = 1f;

        [UnityTest, Timeout(600000)]
        public IEnumerator Robot_PlaysWholeGames_AndTheScreenAlwaysMatchesTheBoard()
        {
            yield return LoadGameScene();
            yield return null;
            Time.timeScale = Speed;
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            var random = new System.Random(39);
            int cleared = 0;
            System.Action<IReadOnlyList<Line>, IReadOnlyList<Cell>, int, bool> countLines = (lines, cells, points, byMove) => cleared += lines.Count;
            game.Menu.NewGameButton.Click();
            var watched = game.Controller.Game;
            watched.OnLinesCleared += countLines;
            yield return WaitUntilSettled(game);

            int finished = 0, turns = 0, lastGameEnd = 0;
            while (finished < 2 && turns < 400)
            {
                turns++;
                if (game.Controller.Game != watched) { watched = game.Controller.Game; watched.OnLinesCleared += countLines; }
                yield return PlayOneTurn(game, random, turns, smart: turns - lastGameEnd < 50);
                yield return WaitUntilSettled(game);
                CheckScreenMatchesBoard(game, "turn " + turns);

                if (turns % 17 == 0 && !game.Controller.Game.IsOver)
                {
                    // Off to the menu and back: the same game continues, exactly as it was.
                    var before = Snapshot(game.Controller.Game.Board);
                    int score = game.Controller.Game.Score;
                    game.Hud.BackButton.Click();
                    yield return new WaitForSeconds(0.5f);
                    Assert.IsTrue(game.Menu.ContinueButton.Interactable, "Continue is offered");
                    game.Menu.ContinueButton.Click();
                    yield return WaitUntilSettled(game);
                    CollectionAssert.AreEqual(before, Snapshot(game.Controller.Game.Board), "The same board after Continue");
                    Assert.AreEqual(score, game.Controller.Game.Score);
                    CheckScreenMatchesBoard(game, "after Continue, turn " + turns);
                }

                if (game.Controller.Game.IsOver)
                {
                    float waited = 0f;
                    while (!game.Hud.GameOver.IsOpen && waited < 5f) { waited += Time.unscaledDeltaTime; yield return null; }
                    Assert.IsTrue(game.Hud.GameOver.IsOpen, "The game-over pop-up opens");
                    Assert.AreEqual(game.Controller.Game.Score, game.Hud.GameOver.Score);
                    int count = game.Scores.Count;
                    game.Hud.GameOver.SaveButton.Click();
                    yield return new WaitForSeconds(0.5f);
                    Assert.IsTrue(game.LeaderboardScreen.IsShowing, "Save opens the leaderboard");
                    Assert.AreEqual(System.Math.Min(count + 1, Leaderboard.MaxEntries), game.Scores.Count, "The score was added");
                    finished++;
                    lastGameEnd = turns;
                    game.LeaderboardScreen.PlayAgainButton.Click();
                    yield return WaitUntilSettled(game);
                    Assert.IsFalse(game.Controller.Game.IsOver, "Play again starts a new game");
                    CheckScreenMatchesBoard(game, "new game");
                }
            }
            Debug.Log($"Lines robot: {turns} turns, {finished} games finished, {cleared} lines cleared");
            Assert.Greater(cleared, 0, "Lines were cleared along the way");
            Assert.AreEqual(2, finished, "Two whole games were played to the end");
        }

        static IEnumerator PlayOneTurn(GameBootstrap game, System.Random random, int turn, bool smart)
        {
            var controller = game.Controller;
            var board = controller.Game.Board;
            var cells = Enumerable.Range(0, board.CellCount).Select(i => new Cell(i % board.Size, i / board.Size)).ToList();
            var balls = cells.Where(c => !board.IsEmpty(c)).ToList();
            var empty = cells.Where(c => board.IsEmpty(c)).ToList();
            Vector2 Px(Cell c) => game.Layout.WorldToScreen(game.Layout.BoardCenterWorld + (Vector2)BoardView.CellToLocal(c));

            // Sometimes Undo instead of a move.
            if (turn % 7 == 0 && controller.CanUndo)
            {
                game.Hud.UndoButton.Click();
                yield break;
            }

            // A ball and a cell it can reach (now and then one it can't: "No path").
            var movable = balls.Where(b => empty.Any(e => Pathfinder.HasPath(board, b, e))).ToList();
            bool tryBlocked = turn % 5 == 0;
            Cell from, to;
            if (tryBlocked)
            {
                var pairs = (from b in balls from e in empty where !Pathfinder.HasPath(board, b, e) select (b, e)).ToList();
                if (pairs.Count > 0)
                {
                    (from, to) = pairs[random.Next(pairs.Count)];
                    Assert.IsTrue(game.Taps.HandlePress(Px(from)));
                    yield return null;
                    game.Taps.HandlePress(Px(to));
                    yield return null;
                    Assert.AreEqual(TurnState.BallSelected, controller.State, "No path: the mushroom stays selected");
                    Assert.AreEqual(BallColor.None, board.Get(to), "...and doesn't move");
                    game.Taps.HandlePress(Px(from)); // let go of it
                    yield return null;
                }
            }
            if (movable.Count == 0) yield break;
            // Mostly it plays like a person building lines (so lines get cleared too), sometimes just anything.
            var moves = (from b in movable from e in empty where Pathfinder.HasPath(board, b, e) select (b, e)).ToList();
            if (!smart || random.Next(4) == 0) (from, to) = moves[random.Next(moves.Count)];
            else
            {
                int best = moves.Max(m => RunAfter(board, m.b, m.e));
                var good = moves.Where(m => RunAfter(board, m.b, m.e) == best).ToList();
                (from, to) = good[random.Next(good.Count)];
            }

            if (controller.State == TurnState.BallSelected && controller.Selected != from)
                game.Taps.HandlePress(Px(from)); // switches the selection
            else if (controller.State != TurnState.BallSelected)
                game.Taps.HandlePress(Px(from));
            yield return null;
            Assert.AreEqual(from, controller.Selected, "Selected");
            Assert.IsTrue(game.Taps.HandlePress(Px(to)), "The move starts");

            // A few impatient presses while it rolls: all ignored.
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                if (controller.AcceptsInput) break;
                Assert.IsFalse(game.Taps.HandlePress(Px(cells[random.Next(cells.Count)])), "Ignored during the animation");
            }
        }

        // The longest row of one colour through <paramref name="to"/> after moving there from <paramref name="from"/>.
        static int RunAfter(Board board, Cell from, Cell to)
        {
            var color = board.Get(from);
            int best = 1;
            foreach (var (dc, dr) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
            {
                int run = 1;
                for (int sign = -1; sign <= 1; sign += 2)
                    for (int k = 1; ; k++)
                    {
                        var c = new Cell(to.Column + sign * k * dc, to.Row + sign * k * dr);
                        if (!board.IsInside(c) || c == from || board.Get(c) != color) break;
                        run++;
                    }
                best = System.Math.Max(best, run);
            }
            return best;
        }

        static IEnumerator WaitUntilSettled(GameBootstrap game)
        {
            float waited = 0f;
            while (waited < 10f)
            {
                var c = game.Controller;
                if (c.Game.IsOver || (c.AcceptsInput && Tween.ActiveCount == 0 || c.AcceptsInput && waited > 2f)) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.3f); // the last pop and pulse finish
        }

        static void CheckScreenMatchesBoard(GameBootstrap game, string when)
        {
            var state = game.Controller.Game;
            var board = state.Board;
            for (int i = 0; i < board.CellCount; i++)
            {
                var cell = new Cell(i % board.Size, i / board.Size);
                var ball = game.BoardView.BallAt(cell);
                if (board.IsEmpty(cell))
                    Assert.IsNull(ball, $"{when}: {cell} is empty on the board but shows a mushroom");
                else
                {
                    Assert.IsNotNull(ball, $"{when}: {cell} has a mushroom on the board but not on screen");
                    Assert.AreEqual(board.Get(cell), ball.Color, $"{when}: {cell} shows the wrong mushroom");
                    Assert.AreEqual((Vector3)(Vector2)BoardView.CellToLocal(cell), (Vector3)(Vector2)ball.transform.localPosition, $"{when}: {cell} is out of place");
                }
            }
            Assert.AreEqual(81 - board.EmptyCount, game.BoardView.BallCount, when + ": no extra mushrooms");
            Assert.AreEqual(state.Score, game.Hud.Score, when + ": the score shown");
        }

        static List<BallColor> Snapshot(Board board) =>
            Enumerable.Range(0, board.CellCount).Select(i => board.Get(new Cell(i % board.Size, i / board.Size))).ToList();
    }
}
