using System.IO;
using System.Linq;
using Lines.Core;
using Lines.Services;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class SaveTests
    {
        static Cell C(int column, int row) => new Cell(column, row);

        /// <summary>A game a few moves in, with an Undo step available.</summary>
        static GameState PlayedGame(int seed, int moves)
        {
            var game = GameState.NewGame(seed);
            var random = new GameRandom(seed + 50);
            for (int i = 0; i < moves && !game.IsOver; i++)
            {
                var balls = Enumerable.Range(0, 81).Select(k => C(k % 9, k / 9)).Where(c => !game.Board.IsEmpty(c)).ToList();
                foreach (var from in balls.OrderBy(_ => random.Next(1000)))
                {
                    var to = game.Board.EmptyCells().FirstOrDefault(c => Pathfinder.HasPath(game.Board, from, c));
                    if (game.Board.IsInside(to) && game.Board.IsEmpty(to) && Pathfinder.HasPath(game.Board, from, to))
                    {
                        game.TryMove(from, to);
                        break;
                    }
                }
            }
            return game;
        }

        static void AssertSameBoard(Board expected, Board actual, string what)
        {
            for (int i = 0; i < 81; i++)
                Assert.AreEqual(expected.Get(C(i % 9, i / 9)), actual.Get(C(i % 9, i / 9)), $"{what} at {C(i % 9, i / 9)}");
        }

        static void AssertSamePreview(Preview expected, Preview actual, string what)
        {
            CollectionAssert.AreEqual(expected.Colors, actual.Colors, what + " colors");
            CollectionAssert.AreEqual(expected.Cells, actual.Cells, what + " cells");
        }

        // ---- Game ↔ save data ----

        [Test]
        public void SaveAndRestore_SameBoardScorePreviewAndUndo()
        {
            var game = PlayedGame(5, 6);
            Assert.IsTrue(game.CanUndo);

            var restored = GameSave.From(game).ToGameState();

            Assert.IsNotNull(restored);
            AssertSameBoard(game.Board, restored.Board, "board");
            Assert.AreEqual(game.Score, restored.Score);
            AssertSamePreview(game.Preview, restored.Preview, "preview");
            Assert.IsTrue(restored.CanUndo, "Undo still works after a restart");
            AssertSameBoard(game.UndoStep.Board, restored.UndoStep.Board, "undo board");
            Assert.AreEqual(game.UndoStep.Score, restored.UndoStep.Score);
            AssertSamePreview(game.UndoStep.Preview, restored.UndoStep.Preview, "undo preview");
        }

        [Test]
        public void Restored_UndoTakesBackTheLastMove()
        {
            var game = PlayedGame(6, 4);
            var restored = GameSave.From(game).ToGameState();
            game.Undo();
            restored.Undo();
            AssertSameBoard(game.Board, restored.Board, "after undo");
            Assert.AreEqual(game.Score, restored.Score);
        }

        [Test]
        public void Restored_ComingPiecesStayTheSame()
        {
            // The random generator's state is saved, so the same move spawns the same pieces in both.
            var game = PlayedGame(7, 3);
            var restored = GameSave.From(game).ToGameState();
            var from = Enumerable.Range(0, 81).Select(k => C(k % 9, k / 9)).First(c => !game.Board.IsEmpty(c));
            var to = game.Board.EmptyCells().Where(c => !game.Preview.Cells.Contains(c)).First(c => Pathfinder.HasPath(game.Board, from, c));

            game.TryMove(from, to);
            restored.TryMove(from, to);

            AssertSameBoard(game.Board, restored.Board, "after the next turn");
            AssertSamePreview(game.Preview, restored.Preview, "next preview");
        }

        [Test]
        public void NewGame_NoUndoInTheSave()
        {
            var save = GameSave.From(GameState.NewGame(1));
            Assert.IsFalse(save.hasUndo);
            Assert.IsFalse(save.ToGameState().CanUndo);
        }

        [Test]
        public void FinishedGame_IsSavedAsOver()
        {
            var game = PlayedGame(8, 500);
            Assert.IsTrue(game.IsOver);
            var restored = GameSave.From(game).ToGameState();
            Assert.IsTrue(restored.IsOver);
            Assert.IsFalse(restored.CanUndo);
        }

        [Test]
        public void OldSaveWithACreatureSet_StillLoads()
        {
            var json = JsonUtility.ToJson(GameSave.From(PlayedGame(5, 2)));
            json = json.Replace("\"hasPendingScore\"", "\"creatures\":[8,0,3,5,1,7,2],\"hasPendingScore\"");
            Assert.IsNotNull(JsonUtility.FromJson<GameSave>(json).ToGameState(), "The old field is simply ignored");
        }

        // ---- Damaged data: no crash, just "no saved game" ----

        static GameSave GoodSave() => GameSave.From(PlayedGame(9, 3));

        [Test]
        public void DamagedSaves_ReturnNull()
        {
            var tooShort = GoodSave(); tooShort.board = new int[10];
            var badColor = GoodSave(); badColor.board[0] = 9;
            var noPreview = GoodSave(); noPreview.previewColors = null;
            var futureVersion = GoodSave(); futureVersion.version = 99;
            var negativeScore = GoodSave(); negativeScore.score = -5;
            var dotOnABall = GoodSave();
            dotOnABall.previewCells = new[] { Enumerable.Range(0, 81).First(i => dotOnABall.board[i] != 0) };

            Assert.IsNull(tooShort.ToGameState(), "board too short");
            Assert.IsNull(badColor.ToGameState(), "unknown color");
            Assert.IsNull(noPreview.ToGameState(), "missing preview");
            Assert.IsNull(futureVersion.ToGameState(), "unknown version");
            Assert.IsNull(negativeScore.ToGameState(), "negative score");
            Assert.IsNull(dotOnABall.ToGameState(), "preview cell already taken");
        }

        // ---- The file ----

        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "LinesSaveTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(folder, true); } catch { /* best effort */ }
        }

        [Test]
        public void Leaderboard_WriteThenRead()
        {
            var service = new LeaderboardService(folder);
            var board = service.Load();
            Assert.AreEqual(0, board.Count, "Empty at first");
            board.Record(120, "Anna", 1000);
            board.Record(80, "", 2000);
            service.Save(board);

            var again = new LeaderboardService(folder).Load();
            Assert.AreEqual(2, again.Count);
            Assert.AreEqual("Anna", again.entries[0].name);
            Assert.AreEqual(1000, again.entries[0].time);
            Assert.AreEqual("Player1", again.entries[1].name);
            Assert.AreEqual("Anna", again.lastName);
            Assert.AreEqual(1, again.placeholdersUsed);
            Assert.AreNotEqual(new SaveService(folder).FilePath, service.FilePath, "Separate from the game save");
        }

        [Test]
        public void Leaderboard_GarbageFile_MeansEmpty()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, LeaderboardService.FileName), "{ nope");
            Assert.AreEqual(0, new LeaderboardService(folder).Load().Count);
        }

        [Test]
        public void PendingScore_IsKeptInTheSave()
        {
            var save = GameSave.From(GameState.NewGame(1));
            save.hasPendingScore = true;
            var service = new SaveService(folder);
            service.Save(save);
            Assert.IsTrue(service.Load().hasPendingScore);
        }

        [Test]
        public void File_WriteThenRead_SameGame()
        {
            var service = new SaveService(folder);
            var game = PlayedGame(10, 5);
            Assert.IsFalse(service.HasSave);

            service.Save(GameSave.From(game));

            Assert.IsTrue(service.HasSave);
            var loaded = service.Load();
            var restored = loaded.ToGameState();
            AssertSameBoard(game.Board, restored.Board, "from the file");
            Assert.AreEqual(game.Score, restored.Score);
            Assert.AreEqual(game.CanUndo, restored.CanUndo);
        }

        [Test]
        public void File_Missing_OrGarbage_MeansNoSave()
        {
            var service = new SaveService(folder);
            Assert.IsNull(service.Load(), "no file");

            Directory.CreateDirectory(folder);
            File.WriteAllText(service.FilePath, "this is not a saved game {");
            Assert.IsNull(service.Load(), "garbage file");
        }

        [Test]
        public void File_Delete()
        {
            var service = new SaveService(folder);
            service.Save(GameSave.From(GameState.NewGame(1)));
            service.Delete();
            Assert.IsFalse(service.HasSave);
        }

        [Test]
        public void File_SavingAgain_ReplacesTheOldGame()
        {
            var service = new SaveService(folder);
            service.Save(GameSave.From(GameState.NewGame(1)));
            var second = PlayedGame(2, 4);
            service.Save(GameSave.From(second));
            AssertSameBoard(second.Board, service.Load().ToGameState().Board, "latest save");
            Assert.IsFalse(File.Exists(service.FilePath + ".tmp"), "No temporary file left behind");
        }

        // ---- Pieces ----

        [Test]
        public void Mushrooms_AreThe7Pictures_OnePerColor()
        {
            var pieces = PieceSet.Mushrooms();
            Assert.IsTrue(pieces.UsesImages, "The mushroom images are in Resources/Mushrooms");
            var sprites = BallColors.All.Select(pieces.SpriteFor).ToArray();
            Assert.AreEqual(7, sprites.Distinct().Count(), "A different mushroom for every color");
            Assert.IsTrue(sprites.All(s => s.name.StartsWith("mushroom_")));
            Assert.AreEqual(0.94f, pieces.SizeInCell, 1e-4f);
        }
    }
}
