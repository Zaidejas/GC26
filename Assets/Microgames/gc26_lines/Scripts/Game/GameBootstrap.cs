using System.Collections.Generic;
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
        /// <summary>L09: Timed mode's own top 10 (the name memory stays in <see cref="Scores"/>).</summary>
        public Leaderboard TimedScores { get; private set; }
        /// <summary>L09: the mode chosen on the menu (Continue, New game and Leaderboard follow it).</summary>
        public bool Timed { get; private set; }
        public Leaderboard ScoresFor(bool timed) => timed ? TimedScores : Scores;
        /// <summary>The finished game's score is waiting on the game-over pop-up.</summary>
        public bool ScorePending { get; private set; }
        long latestScoreTime;

        /// <summary>True while the game screen (board and HUD) is showing; false on the main menu.</summary>
        public bool GameScreenShowing { get; private set; }

        /// <summary>There is a game that isn't over (Continue is available).</summary>
        public bool HasUnfinishedGame => Controller != null && !Controller.Game.IsOver;
        public InputHandler Taps { get; private set; }

        /// <summary>True when board taps must be ignored: a pop-up or a tutorial is open, or the menu is showing.</summary>
        public bool InputBlocked => !GameScreenShowing || (hud != null && (hud.Dialog.IsOpen || hud.GameOver.IsOpen || hud.Settings.IsOpen))
            || (coach != null && coach.IsShowing);

        /// <summary>L17: the tutorials on the board (coach marks).</summary>
        public CoachMarks Coach => coach;
        CoachMarks coach;
        /// <summary>L18: the timed game is paused (the board and NEXT are hidden).</summary>
        public bool Paused { get; private set; }
        bool hiddenBySettings;
        float celebrateUntil;  // "New best!" and its fireworks play first; the puffball's banner or tutorial waits
        int aimStartPuffballs; // to tell a burst from a cancelled aim
        readonly List<string> tutorialQueue = new List<string>();
        string tutorial;       // the tutorial showing now ("rainbow", "ice", "super", "rescue"), or null
        int tutorialStep;

        BoardPresenter presenter;
        SaveService classicSaves, timedSaves;
        LeaderboardService scoreFile, timedScoreFile;
        int lastClockSecond = -1;
        bool timeUpPending; // "Time's up!" is showing; the game-over pop-up follows
        SettingsService settingsFile;
        bool howToPlayFromGame; // How to play was opened from Settings during a game: it returns there

        /// <summary>The player's Settings choices (music, sounds, vibration, language), saved on the device.</summary>
        public GameSettings Settings { get; private set; }

        /// <summary>The game's sounds and music.</summary>
        public LinesAudio Sounds { get; private set; }
        /// <summary>Vibration (task L02), following the Vibration switch.</summary>
        public Haptics.LinesHaptics Haptics { get; private set; }
        /// <summary>L04: fireworks when the score passes the best.</summary>
        public Fireworks Fireworks => fireworks;
        Fireworks fireworks;
        /// <summary>L04: the best finished game when this game started (0: none yet), and whether it was celebrated.</summary>
        public int BestAtStart { get; private set; }
        bool celebrated;

        /// <summary>Where the chosen mode's game in progress is kept.</summary>
        public SaveService Saves => SavesFor(Timed);
        public SaveService SavesFor(bool timed) => timed ? timedSaves : classicSaves;

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
            fireworks = Fireworks.Create(mainCamera);
            hud = HudView.Create(mainCamera);
            forest = ForestBackground.Create(mainCamera);
            menu = MenuView.Create(mainCamera);
            howToPlay = HowToPlayView.Create(mainCamera);
            leaderboardView = LeaderboardView.Create(mainCamera);
            coach = CoachMarks.Create(mainCamera);
            presenter = new BoardPresenter(boardView, hud);
            // Sounds and music (made from code in the background), following the Settings switches.
            Sounds = LinesAudio.Create(transform, Settings.music, Settings.sounds);
            presenter.Sounds = Sounds;
            Haptics = new Haptics.LinesHaptics(Settings.vibration);
            presenter.Haptics = Haptics;
            classicSaves = new SaveService();
            timedSaves = new SaveService(timed: true);
            scoreFile = new LeaderboardService();
            timedScoreFile = new LeaderboardService(timed: true);
            Scores = scoreFile.Load();
            Scores.placeholderPrefix = Texts.Get("player");
            TimedScores = timedScoreFile.Load();
            Timed = Settings.mode == "timed";
            RecordLeftoverScore(timed: false);
            RecordLeftoverScore(timed: true);
            presenter.GameOver += OnGameOver;
            hud.GameOver.Saved += SaveScore;
            hud.GameOver.MenuChosen += () => { hud.GameOver.Close(); ShowMenu(); };
            hud.GameOver.PlayAgainChosen += () => { hud.GameOver.Close(); StartNewGame(); ShowGame(); };
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
            hud.PuffballClicked += OnPuffballButton;
            hud.PauseClicked += Pause;
            coach.GotItClicked += CloseTutorial;
            coach.SkipClicked += SkipTutorial;
            coach.Tapped += OnCoachTap;
            coach.Dragged += OnCoachDrag;
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
            menu.ModeChosen += SetMode;
            leaderboardView.TabChosen += timed =>
                leaderboardView.Show(ScoresFor(timed), latestScoreTime, null, leaderboardView.PlayAgainShowing, timed);
            leaderboardView.Closed += ShowMenu;
            leaderboardView.PlayAgainClicked += () => { StartNewGame(); ShowGame(); };
            menu.SetSecondaryScreensAvailable(howToPlay: true, leaderboardScreen: true);
            // Every button ticks softly when tapped (all screens and pop-ups are built by now).
            Sounds.AddTapSounds(new Component[] { menu, hud, hud.Dialog, hud.GameOver, hud.Settings, howToPlay, leaderboardView, coach }
                .SelectMany(view => view.GetComponentsInChildren<UnityEngine.UI.Button>(true)));
            ShowMenu();
            StartCoroutine(UiFonts.WarmUp()); // all letters ready before they're needed, without a hitch
#if LINES_FPS
            LinesFpsMeter.Create(mainCamera); // test builds only (LINES_FPS=1): frame rate on screen and in the log
#endif
            Debug.Log("Lines: game started");
        }

        /// <summary>A fresh game: new seed, 3 random pieces, score 0, no Undo.</summary>
        public void StartNewGame()
        {
            int seed = System.Environment.TickCount;
            ScorePending = false; // only finished games reach the leaderboard
            Use(new LinesGameController(GameState.NewGame(seed, Timed), presenter));
            SaveGame(); // Reset also starts a new save
        }

        // ---- Screens ----

        /// <summary>The app opens here. Continue shows the saved game's score, or is greyed out.</summary>
        public void ShowMenu()
        {
            LeaveGameScreen();
            forest.Show(menu: true);
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            fireworks.Stop();
            hud.SetVisible(false);
            howToPlay.Hide();
            leaderboardView.Hide();
            menu.Show(HasUnfinishedGame ? Controller.Game.Score : (int?)null, boardView.Pieces, Timed,
                HasUnfinishedGame ? Controller.Game.TimeLeft : (float?)null);
        }

        /// <summary>
        /// L09: a mode badge on the menu. The other mode's game in progress (if any) is loaded behind Continue;
        /// the one left is saved first. The choice is remembered.
        /// </summary>
        public void SetMode(bool timed)
        {
            if (timed == Timed) return;
            SaveGame();
            Timed = timed;
            Settings.mode = timed ? "timed" : "classic";
            settingsFile.Save(Settings);
            Controller = null;
            ScorePending = false;
            TryContinueSavedGame();
            ShowMenu();
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
            LeaveGameScreen();
            forest.Show(menu: false);
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            fireworks.Stop();
            hud.SetVisible(false);
            menu.Hide();
            leaderboardView.Hide();
            howToPlay.Show(boardView.Pieces, fromGame);
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
            if (hud.Dialog.IsOpen || hud.GameOver.IsOpen || hud.Settings.IsOpen || timeUpPending) return;
            if (GameScreenShowing && Controller != null && !Controller.AcceptsInput && Controller.State != TurnState.GameOver) return;
            if (coach.IsShowing) return;
            Controller?.CancelAiming();
            hud.Settings.Show(Settings);
            // L18: in Timed mode the clock stops behind Settings, so the board and NEXT hide too (no free thinking time).
            if (GameScreenShowing && Controller != null && Controller.Game.Timed && !Controller.Game.IsOver)
            {
                hiddenBySettings = true;
                SetPiecesHidden(true);
            }
        }

        /// <summary>A switch or the language changed in Settings: remember it, and switch the language at once.</summary>
        void OnSettingsChanged(GameSettings changed)
        {
            bool vibrationWasOn = Settings.vibration;
            Settings = changed;
            settingsFile.Save(Settings);
            Sounds.Apply(Settings.music, Settings.sounds);
            Haptics.Enabled = Settings.vibration;
            if (Settings.vibration && !vibrationWasOn) Haptics.Play(GC26.Lines.Haptics.LinesFeel.Tick); // feel what was switched on
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
        public void ShowLeaderboard(string result = null, bool afterGame = false, bool? timed = null)
        {
            bool tab = timed ?? Timed;
            LeaveGameScreen();
            forest.Show(menu: false);
            GameScreenShowing = false;
            boardView.gameObject.SetActive(false);
            fireworks.Stop();
            hud.SetVisible(false);
            menu.Hide();
            howToPlay.Hide();
            leaderboardView.Show(ScoresFor(tab), latestScoreTime, result, afterGame, tab);
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
            if (Controller != null) { hud.SetTimed(Controller.Game.Timed); UpdateClock(false); }
            boardView.PlayEntrance(); // the board fades in, the mushrooms pop up one by one
            var game = Controller?.Game;
            Tween.Delay(0.9f, () => { if (Controller != null && Controller.Game == game) CheckTutorials(); }); // after the entrance
        }

        // Leaving the game screen (menu, How to play, Leaderboard): no aiming, tutorial, bubble, banner or pause stays.
        void LeaveGameScreen()
        {
            Controller?.CancelAiming();
            HideCoach();
            tutorial = null;
            hud.Bubble.Hide();
            hud.Banner.Stop();
            if (Paused) Resume();
            if (hiddenBySettings) { hiddenBySettings = false; SetPiecesHidden(false); }
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
            if (!GameScreenShowing || hud.Dialog.IsOpen || hud.Settings.IsOpen || timeUpPending || coach.IsShowing) return;
            if (Controller != null && !Controller.AcceptsInput && Controller.State != TurnState.GameOver) return;
            Controller?.ClearSelection();
            SaveGame();
            ShowMenu();
        }

        /// <summary>Loads the saved game, if there is one that isn't over. Returns false otherwise.</summary>
        public bool TryContinueSavedGame()
        {
            var save = Saves.Load();
            var game = save?.ToGameState();
            if (game == null || game.IsOver || game.Timed != Timed) return false;
            Use(new LinesGameController(game, presenter));
            return true;
        }

        void Use(LinesGameController controller)
        {
            Controller = controller;
            controller.GameChanged += SaveGame;
            controller.GameChanged += CheckBest;
            controller.TimeAdded += seconds => hud.ShowTimeAdded(seconds);
            controller.PuffballsEarned += OnPuffballsEarned;
            controller.StuckWithPuffball += OfferRescue;
            controller.AimingChanged += OnAimingChanged;
            controller.GameChanged += CheckTutorials;
            hud.SetTimed(controller.Game.Timed);
            tutorialQueue.Clear();
            tutorial = null;
            HideCoach();
            hud.Bubble.Hide();
            hud.Banner.Stop();
            celebrateUntil = 0f;
            lastClockSecond = -1;
            timeUpPending = false;
            // L04: the best is the top finished game of this mode. A continued game already above it gets the crown without fireworks.
            var best = ScoresFor(controller.Game.Timed);
            BestAtStart = best != null && best.Count > 0 ? best.entries[0].score : 0;
            celebrated = BestAtStart > 0 && controller.Game.Score > BestAtStart;
            hud.SetCrown(celebrated);
            hud.StopNewBest();
            fireworks.Stop();
        }

        /// <summary>
        /// L04 (GDD → Scoring): the first time in a game the score passes the best, fireworks, "New best!", the record
        /// sound and the success vibration, and the crown pops up next to the score. Undo below the best hides the
        /// crown quietly; passing it again doesn't celebrate twice.
        /// </summary>
        void CheckBest()
        {
            if (Controller == null) return;
            bool above = BestAtStart > 0 && Controller.Game.Score > BestAtStart;
            if (above && !celebrated)
            {
                celebrated = true;
                celebrateUntil = Time.time + 2.2f; // the puffball's banner or tutorial waits for this
                hud.SetCrown(true, pop: true);
                hud.ShowNewBest();
                fireworks.Play();
                Sounds.Play(LinesSound.Record);
                Haptics.Play(GC26.Lines.Haptics.LinesFeel.Success);
                return;
            }
            if (hud.CrownShowing != above) hud.SetCrown(above);
        }

        /// <summary>Writes the game in progress to the device.</summary>
        public void SaveGame()
        {
            if (Controller == null || classicSaves == null) return;
            var save = GameSave.From(Controller.Game);
            save.hasPendingScore = ScorePending && Controller.Game.IsOver;
            SavesFor(Controller.Game.Timed).Save(save);
        }

        // ---- Game over and the leaderboard ----

        /// <summary>The board is full: the pop-up asks for a name; the score waits in the save until Save.</summary>
        void OnGameOver(int score)
        {
            timeUpPending = false;
            // Only a score that gets on the board asks for a name (0 points, or one not above a full top 10's 10th,
            // gets Menu / Play again instead).
            var board = ScoresFor(Controller != null && Controller.Game.Timed);
            ScorePending = board.Qualifies(score);
            SaveGame();
            hud.Dialog.Close();
            hud.Settings.Close();
            HideCoach();
            tutorial = null;
            hud.Bubble.Hide();
            hud.GameOver.Show(score, Scores.DefaultName, Controller != null && Controller.Game.EndedWithNoMoves,
                score > 0 && !ScorePending ? board.TenthScore : -1);
            Sounds.Play(LinesSound.GameOver);
            Haptics.Play(GC26.Lines.Haptics.LinesFeel.GameOver);
        }

        /// <summary>Save on the game-over pop-up: adds the score and opens the Leaderboard.</summary>
        public void SaveScore(string typedName)
        {
            if (!ScorePending || Controller == null) return;
            int score = Controller.Game.Score;
            bool timed = Controller.Game.Timed;
            string name = Scores.CleanName(typedName);
            int place = RecordScore(score, typedName, timed);
            ScorePending = false;
            SaveGame();
            hud.GameOver.Close();
            if (place >= 0) // the score made the top 10
            {
                Sounds.Play(LinesSound.Record);
                Haptics.Play(GC26.Lines.Haptics.LinesFeel.Success);
            }
            ShowLeaderboard(Texts.ResultMessage(place, score, name), afterGame: true, timed: timed);
        }

        // The name memory lives in the Classic leaderboard; the score goes to its mode's list.
        int RecordScore(int score, string typedName, bool timed)
        {
            latestScoreTime = LeaderboardService.Now();
            string name = Scores.RememberName(typedName);
            int place = ScoresFor(timed).Add(score, name, latestScoreTime);
            scoreFile.Save(Scores);
            if (timed) timedScoreFile.Save(TimedScores);
            return place;
        }

        /// <summary>
        /// At launch: a score left on the game-over pop-up (the app closed before Save) goes to the
        /// leaderboard under the name the field was pre-filled with (GDD → Saving).
        /// </summary>
        void RecordLeftoverScore(bool timed)
        {
            var file = SavesFor(timed);
            var save = file.Load();
            if (save == null || !save.isOver || !save.hasPendingScore) return;
            if (save.score > 0) RecordScore(save.score, Scores.DefaultName, timed);
            latestScoreTime = 0; // nothing to highlight: the player didn't see this one saved
            save.hasPendingScore = false;
            file.Save(save);
        }

        // The game also saves when the app goes to the background or closes (GDD → Saving).
        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            SaveGame();
            Pause(); // L18: a timed game comes back paused (nothing happens in Classic)
        }
        void OnApplicationQuit() => SaveGame();

        /// <summary>Reset: asks "Start a new game?" first (not while an animation plays).</summary>
        public void AskToReset()
        {
            if (InputBlocked || timeUpPending) return;
            Controller.CancelAiming();
            if (!Controller.AcceptsInput && Controller.State != TurnState.GameOver) return;
            hud.Dialog.Show(ResetTitle, ResetMessage, Texts.Get("yes"), () => { StartNewGame(); boardView.PlayEntrance(); });
        }

        void Update()
        {
            TickClock(Mathf.Min(Time.deltaTime, 0.1f));
            if (hiddenBySettings && !hud.Settings.IsOpen) { hiddenBySettings = false; SetPiecesHidden(false); } // Settings closed
            // The editor's Game view can be resized, and phones can report a new safe area.
            if (new Vector2(Screen.width, Screen.height) != lastScreenSize || Screen.safeArea != lastSafeArea)
                Relayout();
        }

        // ---- L16: the puffball ----

        /// <summary>
        /// The puffball button: starts aiming (the burst lights up on the busiest spot), or cancels it; with none ready,
        /// a bubble says how far the next one is ("Puffball in 40 points" and a progress bar).
        /// </summary>
        public void OnPuffballButton()
        {
            if (Controller == null || InputBlocked || timeUpPending || Controller.Game.IsOver || !Controller.AcceptsInput) return;
            if (Controller.Aiming) { Controller.CancelAiming(); return; }
            if (Controller.Game.Puffballs > 0) { Controller.StartAiming(); return; }
            var game = Controller.Game;
            hud.Bubble.Show(Texts.PuffballIn(game.NextPuffballAt - game.Score), Texts.Get("superRule"), game.PuffballProgress, 2.6f);
        }

        void OnAimingChanged(bool on)
        {
            hud.Puffball.SetArmed(on);
            if (on)
            {
                aimStartPuffballs = Controller.Game.Puffballs;
                if (!coach.IsShowing) hud.Bubble.Show(Texts.Get("aimTitle"), Texts.Get("aimText"));
                Sounds.Play(LinesSound.Arm);
                Haptics.Play(GC26.Lines.Haptics.LinesFeel.Tick);
                return;
            }
            hud.Bubble.Hide();
            bool burst = Controller.Game.Puffballs < aimStartPuffballs;
            if (burst) return;
            Sounds.Play(LinesSound.Disarm);
            // Still stuck after cancelling: offer the puffball again.
            if (Controller.Game.Stuck && tutorial == null) Tween.Delay(0.25f, OfferRescue);
        }

        // A turn's points earned puffballs: the button pops, a bell arpeggio and the "reward" feel; then the banner
        // (after "New best!" if that plays too), or, the first time in a game, the tutorial instead.
        void OnPuffballsEarned(int count)
        {
            hud.Puffball.Pop();
            Sounds.Play(LinesSound.PuffballGot);
            Haptics.Play(GC26.Lines.Haptics.LinesFeel.Reward);
            if (tutorial == "super" || tutorialQueue.Contains("super")) return;
            var game = Controller.Game;
            Tween.Delay(Mathf.Max(0f, celebrateUntil - Time.time), () =>
            {
                if (Controller != null && Controller.Game == game && GameScreenShowing && !game.IsOver) hud.Banner.Show();
            });
        }

        /// <summary>
        /// L16: no ball can move but a puffball is in stock: the screen dims, the hand points at the puffball,
        /// "The board is full!" (or "No moves left!") / "Use your puffball to keep going", and End game.
        /// </summary>
        void OfferRescue()
        {
            if (Controller == null || !Controller.Game.Stuck || !GameScreenShowing || coach.IsShowing || Paused) return;
            if (!Controller.AcceptsInput || Controller.Aiming || hud.Dialog.IsOpen || hud.Settings.IsOpen) return;
            tutorial = "rescue";
            tutorialStep = 0;
            ShowTutorialStep();
            Sounds.Play(LinesSound.Coach);
        }

        // ---- L17: the tutorials on the board ----

        /// <summary>
        /// The first rainbow, the first frozen mushroom and the first puffball of a game get a tutorial, once per game
        /// (remembered in the game's save). Called after every turn and when the game screen opens.
        /// </summary>
        public void CheckTutorials()
        {
            if (Controller == null) return;
            var game = Controller.Game;
            if (game.IsOver) return;
            if (game.Stuck) { OfferRescue(); return; }
            bool rainbow = game.Preview.Colors.Contains(BallColor.Rainbow);
            bool ice = game.Board.FrozenCount > 0 || game.Preview.Frozen.Contains(true);
            for (int row = 0; row < game.Board.Size && !rainbow; row++)
            for (int column = 0; column < game.Board.Size; column++)
                if (game.Board.Get(new Cell(column, row)) == BallColor.Rainbow) { rainbow = true; break; }
            if (rainbow) Queue("rainbow");
            if (ice) Queue("ice");
            if (game.Puffballs > 0) Queue("super");
            ShowNextTutorial();
        }

        void Queue(string key)
        {
            if (!Controller.Game.TutorialsShown.Contains(key) && !tutorialQueue.Contains(key) && tutorial != key) tutorialQueue.Add(key);
        }

        void ShowNextTutorial()
        {
            if (Controller == null || tutorialQueue.Count == 0 || tutorial != null || coach.IsShowing) return;
            if (!GameScreenShowing || Paused || timeUpPending || Controller.Game.IsOver || !Controller.AcceptsInput) return;
            if (hud.Dialog.IsOpen || hud.Settings.IsOpen || hud.GameOver.IsOpen) return;
            float wait = celebrateUntil - Time.time;
            if (wait > 0f)
            {
                var game = Controller.Game;
                Tween.Delay(wait + 0.1f, () => { if (Controller != null && Controller.Game == game) ShowNextTutorial(); });
                return;
            }
            tutorial = tutorialQueue[0];
            tutorialQueue.RemoveAt(0);
            if (tutorial == "super" && Controller.Game.Puffballs <= 0) { tutorial = null; ShowNextTutorial(); return; }
            Controller.ClearSelection();
            Controller.Game.TutorialsShown.Add(tutorial);
            SaveGame();
            tutorialStep = 0;
            hud.Bubble.Hide();
            ShowTutorialStep();
            Sounds.Play(LinesSound.Coach);
        }

        /// <summary>The tutorial showing now ("rainbow", "ice", "super", "rescue"), or null (tests).</summary>
        public string TutorialShowing => tutorial;
        public int TutorialStep => tutorialStep;

        void ShowTutorialStep()
        {
            if (tutorial == null || Controller == null) return;
            var game = Controller.Game;
            hud.Puffball.SetBadgeOnTop(tutorial == "rescue" || tutorial == "super" && tutorialStep == 0); // lit: the button itself
            switch (tutorial)
            {
                case "rainbow":
                case "ice":
                {
                    bool ice = tutorial == "ice";
                    var hole = SpotOf(game, ice);
                    if (!hole.HasValue) { tutorial = null; ShowNextTutorial(); return; }
                    coach.Show(hole.Value, false, hole.Value.center, Texts.Get(ice ? "coachIce" : "coachRainbow"), "", showGotIt: true, showSkip: false);
                    break;
                }
                case "super" when tutorialStep == 0:
                    coach.Show(PuffballRect(), true, PuffballRect().center, Texts.Get("coachSuper1"), Texts.Get("coachSuper1b"), false, true);
                    break;
                case "super":
                    coach.Show(Layout.Board, false, CellRect(Controller.AimCell).center, Texts.Get("coachAimTitle"), Texts.Get("coachSuper2"),
                        false, true, captionAbove: true);
                    break;
                case "rescue":
                    coach.Show(PuffballRect(), true, PuffballRect().center, Texts.Get(game.Board.IsFull ? "rescueFull" : "rescueNoMoves"),
                        Texts.Get("rescueSub"), false, true, Texts.Get("endGame"));
                    break;
            }
        }

        // Where the first rainbow (or frozen mushroom) is: on the board if one is there already, else in NEXT.
        Rect? SpotOf(GameState game, bool ice)
        {
            for (int row = 0; row < game.Board.Size; row++)
            for (int column = 0; column < game.Board.Size; column++)
            {
                var cell = new Cell(column, row);
                if (game.Board.IsEmpty(cell)) continue;
                if (ice ? game.Board.IsFrozen(cell) : game.Board.Get(cell) == BallColor.Rainbow) return CellRect(cell);
            }
            for (int i = 0; i < game.Preview.Colors.Count && i < hud.NextPieces.Count; i++)
                if (ice ? game.Preview.IsFrozen(i) : game.Preview.Colors[i] == BallColor.Rainbow)
                {
                    var rt = hud.NextPieces[i].rectTransform;
                    return new Rect(Layout.Hud.position + rt.anchoredPosition, rt.sizeDelta);
                }
            return null;
        }

        Rect CellRect(Cell cell)
        {
            var centre = Layout.WorldToScreen(Layout.BoardCenterWorld + (Vector2)BoardView.CellToLocal(cell));
            float size = Layout.CellPitch;
            return new Rect(centre - Vector2.one * size / 2f, Vector2.one * size);
        }

        void HideCoach()
        {
            coach.Hide();
            hud.Puffball.SetBadgeOnTop(false);
        }

        Rect PuffballRect()
        {
            var rt = hud.Puffball.Rect;
            return new Rect(rt.anchoredPosition - rt.sizeDelta / 2f, rt.sizeDelta);
        }

        // Got it (rainbow, ice): the next tutorial, if any, follows.
        void CloseTutorial()
        {
            HideCoach();
            tutorial = null;
            Tween.Delay(0.3f, ShowNextTutorial);
        }

        // Skip (the puffball's tutorial: the puffball stays for later) or End game (the rescue).
        void SkipTutorial()
        {
            string was = tutorial;
            HideCoach();
            tutorial = null;
            if (was == "rescue") { Controller?.EndGame(); return; }
            Controller?.CancelAiming();
            Tween.Delay(0.3f, ShowNextTutorial);
        }

        // A tap while a tutorial shows: only the lit spot does something.
        void OnCoachTap(Vector2 position)
        {
            if (Controller == null) return;
            if ((tutorial == "super" && tutorialStep == 0) || tutorial == "rescue")
            {
                if (!coach.Hole.Contains(position)) return;
                if (tutorial == "rescue") { HideCoach(); tutorial = null; Controller.StartAiming(); return; }
                tutorialStep = 1;
                Controller.StartAiming();
                hud.Bubble.Hide();
                ShowTutorialStep();
                return;
            }
            if (tutorial == "super" && Layout.TryGetCell(position, out var cell))
            {
                if (cell == Controller.AimCell)
                {
                    HideCoach();
                    tutorial = null;
                    if (!Controller.Burst()) { tutorial = "super"; ShowTutorialStep(); } // an empty square can't burst
                    return;
                }
                Controller.AimAt(cell);
                ShowTutorialStep();
            }
        }

        void OnCoachDrag(Vector2 position)
        {
            if (tutorial != "super" || tutorialStep != 1 || Controller == null || !Layout.TryGetCell(position, out var cell)) return;
            if (cell == Controller.AimCell) return;
            Controller.AimAt(cell);
            ShowTutorialStep();
        }

        // ---- L18: Pause (Timed mode) ----

        /// <summary>
        /// Pause: the clock stops and a "Paused" card with Resume opens; the board's mushrooms and NEXT are hidden, so a
        /// pause gives no free thinking time. Also when the app goes to the background during a timed game.
        /// </summary>
        public void Pause()
        {
            if (Paused || Controller == null || !GameScreenShowing || !Controller.Game.Timed || Controller.Game.IsOver || timeUpPending) return;
            if (hud.Dialog.IsOpen || hud.GameOver.IsOpen || hud.Settings.IsOpen || coach.IsShowing || !Controller.AcceptsInput) return;
            Controller.ClearSelection();
            hud.Bubble.Hide();
            Paused = true;
            SetPiecesHidden(true);
            hud.Dialog.ShowSingle(Texts.Get("paused"), Texts.Get("pausedText"), Texts.Get("resume"), Resume);
        }

        /// <summary>Resume: the board comes back and the clock runs again.</summary>
        public void Resume()
        {
            if (!Paused) return;
            Paused = false;
            if (hud.Dialog.IsOpen) hud.Dialog.Close();
            if (!hiddenBySettings) SetPiecesHidden(false);
            CheckTutorials();
        }

        void SetPiecesHidden(bool hidden)
        {
            boardView.SetPiecesHidden(hidden);
            hud.SetNextHidden(hidden);
        }

        // ---- L09: the clock ----

        /// <summary>
        /// Runs the Timed game's clock by <paramref name="seconds"/>: only on the game screen, while the player is
        /// thinking (no animation, no pop-up). From 0:30 it ticks each second (sharper from 0:10). At 0:00:
        /// "Time's up!", then the game-over pop-up. Tests call it directly to move time on.
        /// </summary>
        public void TickClock(float seconds)
        {
            if (!GameScreenShowing || Controller == null || !Controller.Game.Timed || Controller.Game.IsOver) return;
            if (hud.Dialog.IsOpen || hud.Settings.IsOpen || hud.GameOver.IsOpen || coach.IsShowing) return;
            bool ranOut = Controller.AdvanceClock(seconds);
            UpdateClock(true);
            if (ranOut) TimeUp();
        }

        public bool TimeUpShowing => timeUpPending;

        void UpdateClock(bool live)
        {
            if (Controller == null || !Controller.Game.Timed) return;
            float left = Controller.Game.TimeLeft;
            hud.SetClock(left, !Controller.Game.IsOver);
            int second = Mathf.CeilToInt(left - 1e-4f);
            if (second == lastClockSecond) return;
            lastClockSecond = second;
            if (!live || second <= 0 || left > GameConfig.TimeWarn) return;
            Sounds.Play(LinesSound.Tick, left <= GameConfig.TimeLow ? 1 : 0);
            hud.PulseClock();
        }

        void TimeUp()
        {
            timeUpPending = true;
            hud.ShowTimeUp();
            var game = Controller.Game;
            Tween.Delay(1.3f, () =>
            {
                if (Controller == null || Controller.Game != game || !timeUpPending) return;
                presenter.ShowGameOver(game.Score);
            });
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
            coach.ApplyLayout(lastScreenSize, Layout.Unit);
            if (tutorial != null) ShowTutorialStep(); // the lit spot follows the new layout
        }
    }
}
