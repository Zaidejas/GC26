using System.Collections.Generic;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.UI;

namespace GC26.Lines.Game
{
    /// <summary>The Debug panel's tools (GDD → Debug panel; the prototype's #debug).</summary>
    public enum DebugTool
    {
        RainbowNext, FrozenNext, AddPuffball, StartAt190,
        GameOverTop, GameOverShort, GameOverZero,
        ClockTo35, ForgetGames, ClearLeaderboards, FillLeaderboards,
    }

    /// <summary>
    /// The hidden Debug panel's tools, for testing on our own phones (5 quick taps on the version on the menu).
    /// Each game tool takes the game in progress in the chosen mode (or a new one), changes a copy of its save and
    /// loads it again, so it goes through the same saving and loading as a real game.
    /// </summary>
    public partial class GameBootstrap
    {
        /// <summary>The 10 test scores of "Fill top 10": 400 down to 130, a day apart.</summary>
        public static IEnumerable<ScoreEntry> TestScores()
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < Leaderboard.MaxEntries; i++)
                yield return new ScoreEntry(400 - 30 * i, "Test " + (i + 1), now - (i + 1) * 86_400_000L);
        }

        /// <summary>Runs one Debug tool. Returns a short line for the panel ("Leaderboards cleared"), or null.</summary>
        public string RunDebug(DebugTool tool)
        {
            switch (tool)
            {
                case DebugTool.RainbowNext:
                    return Edit(s => { s.previewColors[0] = (int)BallColor.Rainbow; s.previewFrozen[0] = false; Forget(s, "rainbow"); });
                case DebugTool.FrozenNext:
                    return Edit(s =>
                    {
                        if (s.previewColors[0] == (int)BallColor.Rainbow) s.previewColors[0] = (int)BallColor.Red; // a rainbow never freezes
                        s.previewFrozen[0] = true;
                        Forget(s, "ice");
                    });
                case DebugTool.AddPuffball:
                    return Edit(s => { s.puffballs = System.Math.Min(GameConfig.PuffballMax, s.puffballs + 1); Forget(s, "super"); });
                case DebugTool.StartAt190:
                    return Edit(StartAt190, fresh: true);
                case DebugTool.GameOverTop:
                case DebugTool.GameOverShort:
                case DebugTool.GameOverZero:
                    return GameOverWith(tool);
                case DebugTool.ClockTo35:
                    if (!Timed) SetMode(true);
                    return Edit(s => s.timeLeft = 35f);
                case DebugTool.ForgetGames:
                    SavesFor(false).Delete();
                    SavesFor(true).Delete();
                    Controller = null;
                    ScorePending = false;
                    ShowMenu();
                    return "Saved games forgotten";
                case DebugTool.ClearLeaderboards:
                case DebugTool.FillLeaderboards:
                    bool fill = tool == DebugTool.FillLeaderboards;
                    foreach (var board in new[] { Scores, TimedScores })
                    {
                        board.entries.Clear();
                        if (fill) board.entries.AddRange(TestScores());
                    }
                    scoreFile.Save(Scores);
                    timedScoreFile.Save(TimedScores);
                    if (Controller != null) // "New best" compares with the new top of this mode
                    {
                        var best = ScoresFor(Controller.Game.Timed);
                        BestAtStart = best.Count > 0 ? best.entries[0].score : 0;
                    }
                    return fill ? "Top 10 filled: 10th place has 130" : "Leaderboards cleared";
            }
            return null;
        }

        static void Forget(GameSave save, string tutorialKey)
        {
            if (save.tutorials != null) save.tutorials = save.tutorials.Where(t => t != tutorialKey).ToArray();
        }

        // 190 points with 4 red mushrooms in a row and a 5th below: move it up to make the line, pass 200 and get
        // the first puffball (and its tutorial). The same board as the prototype's "Start at 190".
        static void StartAt190(GameSave s)
        {
            for (int i = 0; i < s.board.Length; i++) { s.board[i] = (int)BallColor.None; s.frozen[i] = false; }
            foreach (int i in new[] { 37, 38, 39, 40, 59 }) s.board[i] = (int)BallColor.Red;
            s.board[10] = (int)BallColor.Green; s.board[16] = (int)BallColor.Blue; s.board[64] = (int)BallColor.Yellow;
            s.board[70] = (int)BallColor.Cyan; s.board[76] = (int)BallColor.Purple;
            s.previewColors = new[] { (int)BallColor.Orange, (int)BallColor.Yellow, (int)BallColor.Blue };
            s.previewCells = new[] { 0, 8, 80 };
            s.previewFrozen = new bool[3];
            s.score = 190;
            s.nextPuffballAt = 200;
            s.puffballs = 0;
            s.tutorials = new string[0];
            s.hasUndo = false;
        }

        // Ends the game at once with a score that shows one game-over case: one that gets into the top 10 (a name
        // is asked), one that doesn't (the 10th place's score as the goal; the board is filled first if needed),
        // or 0 points.
        string GameOverWith(DebugTool tool)
        {
            var board = ScoresFor(Timed);
            if (tool == DebugTool.GameOverShort && board.Count < Leaderboard.MaxEntries)
            {
                board.entries.Clear();
                board.entries.AddRange(TestScores());
                (Timed ? timedScoreFile : scoreFile).Save(board);
            }
            string result = Edit(s =>
            {
                int tenth = board.TenthScore;
                s.score = tool == DebugTool.GameOverZero ? 0
                    : tool == DebugTool.GameOverTop ? (tenth >= 0 ? tenth + 10 : System.Math.Max(s.score, 50))
                    : System.Math.Max(1, tenth - 10);
                s.hasUndo = false;
            });
            Controller?.EndGame(); // the game-over pop-up opens as after a real last move
            return result;
        }

        // Takes the game in progress (or a new one in the chosen mode), lets <paramref name="change"/> edit its save,
        // loads it and shows the game screen.
        string Edit(System.Action<GameSave> change, bool fresh = false)
        {
            var game = !fresh && HasUnfinishedGame && Controller.Game.Timed == Timed ? Controller.Game
                : GameState.NewGame(System.Environment.TickCount, Timed);
            var save = GameSave.From(game);
            if (save.previewFrozen == null || save.previewFrozen.Length != save.previewColors.Length) save.previewFrozen = new bool[save.previewColors.Length];
            if (save.frozen == null) save.frozen = new bool[save.board.Length];
            change(save);
            var changed = save.ToGameState();
            if (changed == null) return "That didn't work";
            ScorePending = false;
            Use(new LinesGameController(changed, presenter));
            SaveGame();
            ShowGame();
            return null;
        }
    }
}
