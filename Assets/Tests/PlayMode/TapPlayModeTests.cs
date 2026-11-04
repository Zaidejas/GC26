using System.Collections;
using System.Linq;
using Lines.Core;
using Lines.Game;
using Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lines.Tests
{
    /// <summary>Plays a real turn in the Main scene by pressing on screen positions, like a player would.</summary>
    public class TapPlayModeTests
    {
        [UnityTest]
        public IEnumerator PressBallThenEmptyCell_PlaysATurn()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            var board = game.Controller.Game.Board;
            var layout = game.Layout;

            // Find a ball and an empty cell it can reach.
            var all = Enumerable.Range(0, 81).Select(i => new Cell(i % 9, i / 9)).ToList();
            var ball = all.First(c => !board.IsEmpty(c));
            var target = all.First(c => board.IsEmpty(c) && Pathfinder.HasPath(board, ball, c));
            Vector2 Px(Cell c) => layout.WorldToScreen(layout.BoardCenterWorld + (Vector2)BoardView.CellToLocal(c));

            Assert.IsTrue(game.Taps.HandlePress(Px(ball)));
            Assert.AreEqual(TurnState.BallSelected, game.Controller.State);
            yield return new WaitForSeconds(0.2f);
            Assert.AreNotEqual(BoardView.CellToLocal(ball), game.BoardView.BallAt(ball).transform.localPosition, "It bounces");

            int moves = 0;
            game.Controller.Game.OnBallMoved += (from, to, path) => { if (from == ball && to == target) moves++; };
            Assert.IsTrue(game.Taps.HandlePress(Px(target)));
            Assert.AreEqual(1, moves, "The ball moved to the pressed cell");

            // The ball rolls for a moment; presses meanwhile are ignored (the input lock).
            Assert.AreEqual(TurnState.Moving, game.Controller.State);
            Assert.IsFalse(game.Taps.HandlePress(Px(target)), "Ignored while moving");

            float waited = 0;
            while (game.Controller.State != TurnState.Idle && waited < 2f) { waited += Time.deltaTime; yield return null; }
            Assert.AreEqual(TurnState.Idle, game.Controller.State);
            Assert.AreEqual(81 - board.EmptyCount, game.BoardView.BallCount, "The screen shows the board");
            Assert.IsFalse(game.Taps.HandlePress(new Vector2(1, 1)), "Press outside the board does nothing");
        }
    }
}
