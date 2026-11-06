using Lines.Core;
using Lines.Services;
using Lines.UI;
using Lines.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Lines.Game
{
    /// <summary>
    /// The only object placed in the Main scene. Everything else
    /// (board, balls, UI) is created from code starting here.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        Camera mainCamera;
        BoardView boardView;
        HudView hud;
        MenuView menu;
        HowToPlayView howToPlay;
        LeaderboardView leaderboardView;
        Vector2 lastScreenSize;
        Rect lastSafeArea;

        /// <summary>The running game's turn controller; taps arrive from <see cref="InputHandler"/>.</summary>
        public GameController Controller { get; private set; }
        /// <summary>The current screen layout (used to turn taps into board cells).</summary>
        public BoardLayout Layout { get; private set; }
        public BoardView BoardView => boardView;
        public HudView Hud => hud;
        public MenuView Menu => menu;
        public HowToPlayView HowToPlay => howToPlay;
        public LeaderboardView LeaderboardScreen => leaderboardView;
        /// <summary>The top 10 and the player-name memory (saved in its own file).</summary>
        public Leaderboard Scores { get; private set; }
        /// <summary>The finished game's score is waiting on the game-over pop-up.</summary>
        public bool ScorePending { get; private set; }
        long latestScoreTime;

        /// <summary>True while the game screen (board and HUD) is showing; false on the main menu.</summary>
        public bool GameScreenShowing { get; private set; }

        /// <summary>There is a game that isn't over (Continue is available).</summary>
        public bool HasUnfinishedGame => Controller != null && !Controller.Game.IsOver;
        public InputHandler Taps { get; private set; }

        /// <summary>True when board taps must be ignored: a pop-up is open, or the menu is showing.</summary>
        public bool InputBlocked => !GameScreenShowing || (hud != null && (hud.Dialog.IsOpen || hud.GameOver.IsOpen));

        BoardPresenter presenter;
        SaveService saves;
        LeaderboardService scoreFile;

        /// <summary>Where the game in progress is kept.</summary>
        public SaveService Saves => saves;

        public const string ResetTitle = "Start a new game?";
        public const string ResetMessage = "Your current game will be lost.";

        void Awake()
        {
            Application.targetFrameRate = 60;
            mainCamera = Camera.main;
            boardView = BoardView.Create();
            hud = HudView.Create(mainCamera);
            menu = MenuView.Create(mainCamera);
            howToPlay = HowToPlayView.Create(mainCamera);
            leaderboardView = LeaderboardView.Create(mainCamera);
            presenter = new BoardPresenter(boardView, hud);
            saves = new SaveService();
            scoreFile = new LeaderboardService();
            Scores = scoreFile.Load();
            RecordLeftoverScore();
            presenter.GameOver += OnGameOver;
            hud.GameOver.Saved += SaveScore;
            // A saved game is loaded but waits behind Continue; without one, the menu still shows pieces.
            if (!TryContinueSavedGame()) boardView.SetPieces(PieceSet.RandomCreatures(System.Environment.TickCount));
            Relayout();
            Taps = gameObject.AddComponent<InputHandler>();
            Taps.Init(this);

            // Buttons need an event system that reads the new Input System (touch and mouse).
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            hud.UndoClicked += () => Controller.Undo();
            hud.ResetClicked += AskToReset;
            hud.BackClicked += BackToMenu;
            menu.ContinueClicked += Continue;
            menu.NewGameClicked += AskForNewGame;
            menu.HowToPlayClicked += ShowHowToPlay;
            howToPlay.Closed += ShowMenu;
            menu.LeaderboardClicked += () => ShowLeaderboard();
            leaderboardView.Closed += ShowMenu;
            leaderboardView.PlayAgainClicked += () => { StartNewGame(); ShowGame(); };
            menu.SetSecondaryScreensAvailable(howToPlay: true, leaderboardScreen: true);
            ShowMenu();
            Debug.Log("Lines: game started");
        }

        /// <summary>A fresh game: new seed, 3 random pieces, a new set of 7 creatures, score 0, no Undo.</summary>
        public void StartNewGame()
        {
            int seed = System.Environment.TickCount;
            // MVP art: 7 of the 9 creatures per game. For the glossy balls instead, use PieceSet.Balls().
            boardView.SetPieces(PieceSet.RandomCreatures(seed));
            ScorePending = false; // only finished games reach the leaderboard
            Use(new GameController(GameState.NewGame(seed), presenter));
            SaveGame(); // Reset also starts a new save
        }

        // ---- Screens ----

        /// <summary>The app opens here. Continue shows the saved game's score, or is greyed out.</summary>
        public void ShowMenu()
        {
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            hud.SetVisible(false);
            howToPlay.Hide();
            leaderboardView.Hide();
            menu.Show(HasUnfinishedGame ? Controller.Game.Score : (int?)null, boardView.Pieces);
        }

        /// <summary>Menu → How to play: opens on the first card; the back arrow or "Got it" returns.</summary>
        public void ShowHowToPlay()
        {
            menu.Hide();
            howToPlay.Show(boardView.Pieces);
        }

        /// <summary>
        /// The Leaderboard screen. Right after a game it shows <paramref name="result"/> and Play again;
        /// the latest saved score in this session stays highlighted.
        /// </summary>
        public void ShowLeaderboard(string result = null, bool afterGame = false)
        {
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            hud.SetVisible(false);
            menu.Hide();
            howToPlay.Hide();
            leaderboardView.Show(Scores, latestScoreTime, result, afterGame);
        }

        public void ShowGame()
        {
            menu.Hide();
            howToPlay.Hide();
            leaderboardView.Hide();
            boardView.gameObject.SetActive(true);
            hud.SetVisible(true);
            GameScreenShowing = true;
        }

        /// <summary>Menu → Continue: back to the saved game, exactly where it was.</summary>
        public void Continue()
        {
            if (HasUnfinishedGame) ShowGame();
        }

        /// <summary>Menu → New game: starts at once, or asks first if a game is unfinished (GDD → Main menu).</summary>
        public void AskForNewGame()
        {
            if (hud.Dialog.IsOpen) return;
            if (!HasUnfinishedGame) { StartNewGame(); ShowGame(); return; }
            hud.Dialog.Show(ResetTitle, ResetMessage, "Yes", () => { StartNewGame(); ShowGame(); });
        }

        /// <summary>The back arrow: saves the game and returns to the menu (not while an animation plays).</summary>
        public void BackToMenu()
        {
            if (!GameScreenShowing || hud.Dialog.IsOpen) return;
            if (Controller != null && !Controller.AcceptsInput && Controller.State != TurnState.GameOver) return;
            Controller?.ClearSelection();
            SaveGame();
            ShowMenu();
        }

        /// <summary>Loads the saved game, if there is one that isn't over. Returns false otherwise.</summary>
        public bool TryContinueSavedGame()
        {
            var save = saves.Load();
            var game = save?.ToGameState();
            if (game == null || game.IsOver) return false;
            boardView.SetPieces(PieceSet.FromCreatures(save.creatures, System.Environment.TickCount));
            Use(new GameController(game, presenter));
            return true;
        }

        void Use(GameController controller)
        {
            Controller = controller;
            controller.GameChanged += SaveGame;
        }

        /// <summary>Writes the game in progress to the device.</summary>
        public void SaveGame()
        {
            if (Controller == null || saves == null) return;
            var save = GameSave.From(Controller.Game, boardView.Pieces.CreatureIndices);
            save.hasPendingScore = ScorePending && Controller.Game.IsOver;
            saves.Save(save);
        }

        // ---- Game over and the leaderboard ----

        /// <summary>The board is full: the pop-up asks for a name; the score waits in the save until Save.</summary>
        void OnGameOver(int score)
        {
            ScorePending = true;
            SaveGame();
            hud.Dialog.Close();
            hud.GameOver.Show(score, Scores.DefaultName);
        }

        /// <summary>Save on the game-over pop-up: adds the score and opens the Leaderboard.</summary>
        public void SaveScore(string typedName)
        {
            if (!ScorePending || Controller == null) return;
            int score = Controller.Game.Score;
            string name = Scores.CleanName(typedName);
            int place = RecordScore(score, name);
            ScorePending = false;
            SaveGame();
            hud.GameOver.Close();
            ShowLeaderboard(Leaderboard.ResultMessage(place, score, name), afterGame: true);
        }

        int RecordScore(int score, string name)
        {
            latestScoreTime = LeaderboardService.Now();
            int place = Scores.Record(score, name, latestScoreTime);
            scoreFile.Save(Scores);
            return place;
        }

        /// <summary>
        /// At launch: a score left on the game-over pop-up (the app closed before Save) goes to the
        /// leaderboard under the name the field was pre-filled with (GDD → Saving).
        /// </summary>
        void RecordLeftoverScore()
        {
            var save = saves.Load();
            if (save == null || !save.isOver || !save.hasPendingScore || save.score < 0) return;
            RecordScore(save.score, Scores.DefaultName);
            latestScoreTime = 0; // nothing to highlight: the player didn't see this one saved
            save.hasPendingScore = false;
            saves.Save(save);
        }

        // The game also saves when the app goes to the background or closes (GDD → Saving).
        void OnApplicationPause(bool paused) { if (paused) SaveGame(); }
        void OnApplicationQuit() => SaveGame();

        /// <summary>Reset: asks "Start a new game?" first (not while an animation plays).</summary>
        public void AskToReset()
        {
            if (InputBlocked) return;
            if (!Controller.AcceptsInput && Controller.State != TurnState.GameOver) return;
            hud.Dialog.Show(ResetTitle, ResetMessage, "Yes", StartNewGame);
        }

        void Update()
        {
            // The editor's Game view can be resized, and phones can report a new safe area.
            if (new Vector2(Screen.width, Screen.height) != lastScreenSize || Screen.safeArea != lastSafeArea)
                Relayout();
        }

        void Relayout()
        {
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            Layout = BoardLayout.Compute(lastScreenSize, lastSafeArea);
            CameraSetup.Apply(mainCamera, Layout);
            boardView.ApplyLayout(Layout);
            hud.ApplyLayout(Layout);
            menu.ApplyLayout(Layout);
            howToPlay.ApplyLayout(Layout);
            leaderboardView.ApplyLayout(Layout);
        }
    }
}
