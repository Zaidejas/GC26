using System.Linq;
using GC26.Lines.Audio;
using GC26.Lines.Core;
using GC26.Lines.Services;
using GC26.Lines.UI;
using GC26.Lines.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace GC26.Lines.Game
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
        ForestBackground forest;
        HowToPlayView howToPlay;
        LeaderboardView leaderboardView;
        Vector2 lastScreenSize;
        Rect lastSafeArea;

        /// <summary>The running game's turn controller; taps arrive from <see cref="InputHandler"/>.</summary>
        public LinesGameController Controller { get; private set; }
        /// <summary>The current screen layout (used to turn taps into board cells).</summary>
        public BoardLayout Layout { get; private set; }
        public BoardView BoardView => boardView;
        public HudView Hud => hud;
        public MenuView Menu => menu;
        public ForestBackground Forest => forest;
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
        public bool InputBlocked => !GameScreenShowing || (hud != null && (hud.Dialog.IsOpen || hud.GameOver.IsOpen || hud.Settings.IsOpen));

        BoardPresenter presenter;
        SaveService saves;
        LeaderboardService scoreFile;
        SettingsService settingsFile;
        bool howToPlayFromGame; // How to play was opened from Settings during a game: it returns there

        /// <summary>The player's Settings choices (music, sounds, vibration, language), saved on the device.</summary>
        public GameSettings Settings { get; private set; }

        /// <summary>The game's sounds and music.</summary>
        public LinesAudio Sounds { get; private set; }

        /// <summary>Where the game in progress is kept.</summary>
        public SaveService Saves => saves;

        public static string ResetTitle => Texts.Get("confirmTitle");
        public static string ResetMessage => Texts.Get("confirmText");

        void Awake()
        {
            Application.targetFrameRate = 60;
            mainCamera = Camera.main;
            // The language first, so every screen is built with the right texts.
            settingsFile = new SettingsService();
            Settings = settingsFile.Load();
            Texts.Set(Texts.FromCode(Settings.language) ?? Texts.FromSystem(Application.systemLanguage));
            Texts.Changed += OnLanguageChanged;
            boardView = BoardView.Create();
            hud = HudView.Create(mainCamera);
            forest = ForestBackground.Create(mainCamera);
            menu = MenuView.Create(mainCamera);
            howToPlay = HowToPlayView.Create(mainCamera);
            leaderboardView = LeaderboardView.Create(mainCamera);
            presenter = new BoardPresenter(boardView, hud);
            // Sounds and music (made from code in the background), following the Settings switches.
            Sounds = LinesAudio.Create(transform, Settings.music, Settings.sounds);
            presenter.Sounds = Sounds;
            saves = new SaveService();
            scoreFile = new LeaderboardService();
            Scores = scoreFile.Load();
            Scores.placeholderPrefix = Texts.Get("player");
            RecordLeftoverScore();
            presenter.GameOver += OnGameOver;
            hud.GameOver.Saved += SaveScore;
            // MVP art: the 7 glowing mushrooms in every game (glossy balls only if the images are missing).
            boardView.SetPieces(PieceSet.Mushrooms());
            // A saved game is loaded but waits behind Continue.
            TryContinueSavedGame();
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
            howToPlay.Closed += CloseHowToPlay;
            menu.SettingsClicked += OpenSettings;
            hud.SettingsClicked += OpenSettings;
            hud.Settings.Changed += OnSettingsChanged;
            hud.Settings.HowToPlayClicked += () => ShowHowToPlay(fromGame: GameScreenShowing);
            menu.LeaderboardClicked += () => ShowLeaderboard();
            leaderboardView.Closed += ShowMenu;
            leaderboardView.PlayAgainClicked += () => { StartNewGame(); ShowGame(); };
            menu.SetSecondaryScreensAvailable(howToPlay: true, leaderboardScreen: true);
            // Every button ticks softly when tapped (all screens and pop-ups are built by now).
            Sounds.AddTapSounds(new Component[] { menu, hud, hud.Dialog, hud.GameOver, hud.Settings, howToPlay, leaderboardView }
                .SelectMany(view => view.GetComponentsInChildren<UnityEngine.UI.Button>(true)));
            ShowMenu();
            Debug.Log("Lines: game started");
        }

        /// <summary>A fresh game: new seed, 3 random pieces, score 0, no Undo.</summary>
        public void StartNewGame()
        {
            int seed = System.Environment.TickCount;
            ScorePending = false; // only finished games reach the leaderboard
            Use(new LinesGameController(GameState.NewGame(seed), presenter));
            SaveGame(); // Reset also starts a new save
        }

        // ---- Screens ----

        /// <summary>The app opens here. Continue shows the saved game's score, or is greyed out.</summary>
        public void ShowMenu()
        {
            forest.Show(menu: true);
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            hud.SetVisible(false);
            howToPlay.Hide();
            leaderboardView.Hide();
            menu.Show(HasUnfinishedGame ? Controller.Game.Score : (int?)null, boardView.Pieces);
        }

        /// <summary>Menu → How to play: opens on the first card; the back arrow or "Got it" returns.</summary>
        public void ShowHowToPlay() => ShowHowToPlay(fromGame: false);

        /// <summary>
        /// How to play from the menu, or from Settings during a game (<paramref name="fromGame"/>): then the back
        /// arrow and "Got it" return to the game instead of the menu.
        /// </summary>
        public void ShowHowToPlay(bool fromGame)
        {
            howToPlayFromGame = fromGame;
            if (fromGame)
            {
                Controller?.ClearSelection();
                SaveGame();
            }
            forest.Show(menu: false);
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            hud.SetVisible(false);
            menu.Hide();
            leaderboardView.Hide();
            howToPlay.Show(boardView.Pieces);
        }

        void CloseHowToPlay()
        {
            if (howToPlayFromGame && HasUnfinishedGame) ShowGame();
            else ShowMenu();
            howToPlayFromGame = false;
        }

        // ---- Settings ----

        /// <summary>The gear on the menu or the game screen (not while a move is still animating).</summary>
        public void OpenSettings()
        {
            if (hud.Dialog.IsOpen || hud.GameOver.IsOpen || hud.Settings.IsOpen) return;
            if (GameScreenShowing && Controller != null && !Controller.AcceptsInput && Controller.State != TurnState.GameOver) return;
            hud.Settings.Show(Settings);
        }

        /// <summary>A switch or the language changed in Settings: remember it, and switch the language at once.</summary>
        void OnSettingsChanged(GameSettings changed)
        {
            Settings = changed;
            settingsFile.Save(Settings);
            Sounds.Apply(Settings.music, Settings.sounds);
            var language = Texts.FromCode(Settings.language);
            if (language.HasValue) Texts.Set(language.Value);
        }

        /// <summary>Every screen puts its texts in the new language, and the layout follows the new text sizes.</summary>
        void OnLanguageChanged()
        {
            if (Scores != null) Scores.placeholderPrefix = Texts.Get("player");
            menu.RefreshTexts();
            hud.RefreshTexts();
            howToPlay.RefreshTexts();
            Relayout();
        }

        void OnDestroy() => Texts.Changed -= OnLanguageChanged;

        /// <summary>
        /// The Leaderboard screen. Right after a game it shows <paramref name="result"/> and Play again;
        /// the latest saved score in this session stays highlighted.
        /// </summary>
        public void ShowLeaderboard(string result = null, bool afterGame = false)
        {
            forest.Show(menu: false);
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            hud.SetVisible(false);
            menu.Hide();
            howToPlay.Hide();
            leaderboardView.Show(Scores, latestScoreTime, result, afterGame);
        }

        public void ShowGame()
        {
            forest.Show(menu: false);
            menu.Hide();
            howToPlay.Hide();
            leaderboardView.Hide();
            boardView.gameObject.SetActive(true);
            hud.SetVisible(true);
            GameScreenShowing = true;
            boardView.PlayEntrance(); // the board fades in, the mushrooms pop up one by one
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
            hud.Dialog.Show(ResetTitle, ResetMessage, Texts.Get("yes"), () => { StartNewGame(); ShowGame(); });
        }

        /// <summary>The back arrow: saves the game and returns to the menu (not while an animation plays).</summary>
        public void BackToMenu()
        {
            if (!GameScreenShowing || hud.Dialog.IsOpen || hud.Settings.IsOpen) return;
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
            Use(new LinesGameController(game, presenter));
            return true;
        }

        void Use(LinesGameController controller)
        {
            Controller = controller;
            controller.GameChanged += SaveGame;
        }

        /// <summary>Writes the game in progress to the device.</summary>
        public void SaveGame()
        {
            if (Controller == null || saves == null) return;
            var save = GameSave.From(Controller.Game);
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
            Sounds.Play(LinesSound.GameOver);
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
            if (place >= 0) Sounds.Play(LinesSound.Record); // the score made the top 10
            ShowLeaderboard(Texts.ResultMessage(place, score, name), afterGame: true);
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
            hud.Dialog.Show(ResetTitle, ResetMessage, Texts.Get("yes"), () => { StartNewGame(); boardView.PlayEntrance(); });
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
            forest.ApplyLayout(Layout);
            menu.ApplyLayout(Layout);
            howToPlay.ApplyLayout(Layout);
            leaderboardView.ApplyLayout(Layout);
        }
    }
}
