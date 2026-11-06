using Lines.Core;
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
        Vector2 lastScreenSize;
        Rect lastSafeArea;

        /// <summary>The running game's turn controller; taps arrive from <see cref="InputHandler"/>.</summary>
        public GameController Controller { get; private set; }
        /// <summary>The current screen layout (used to turn taps into board cells).</summary>
        public BoardLayout Layout { get; private set; }
        public BoardView BoardView => boardView;
        public HudView Hud => hud;
        public InputHandler Taps { get; private set; }

        /// <summary>True while a pop-up covers the board, so board taps are ignored.</summary>
        public bool InputBlocked => hud != null && hud.Dialog.IsOpen;

        BoardPresenter presenter;

        public const string ResetTitle = "Start a new game?";
        public const string ResetMessage = "Your current game will be lost.";

        void Awake()
        {
            Application.targetFrameRate = 60;
            mainCamera = Camera.main;
            boardView = BoardView.Create();
            hud = HudView.Create(mainCamera);
            presenter = new BoardPresenter(boardView, hud);
            StartNewGame();
            Relayout();
            Taps = gameObject.AddComponent<InputHandler>();
            Taps.Init(this);

            // Buttons need an event system that reads the new Input System (touch and mouse).
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            hud.UndoClicked += () => Controller.Undo();
            hud.ResetClicked += AskToReset;
            Debug.Log("Lines: game started");
        }

        /// <summary>A fresh game: new seed, 3 random pieces, a new set of 7 creatures, score 0, no Undo.</summary>
        public void StartNewGame()
        {
            int seed = System.Environment.TickCount;
            // MVP art: 7 of the 9 creatures per game. For the glossy balls instead, use PieceSet.Balls().
            boardView.SetPieces(PieceSet.RandomCreatures(seed));
            Controller = new GameController(GameState.NewGame(seed), presenter);
        }

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
        }
    }
}
