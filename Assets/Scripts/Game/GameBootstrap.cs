using Lines.Core;
using Lines.View;
using UnityEngine;

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
        Vector2 lastScreenSize;
        Rect lastSafeArea;

        /// <summary>The running game's turn controller; taps arrive from <see cref="InputHandler"/>.</summary>
        public GameController Controller { get; private set; }
        /// <summary>The current screen layout (used to turn taps into board cells).</summary>
        public BoardLayout Layout { get; private set; }
        public BoardView BoardView => boardView;
        public InputHandler Taps { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            mainCamera = Camera.main;
            boardView = BoardView.Create();
            int seed = System.Environment.TickCount;
            // MVP art: 7 of the 9 creatures per game. For the glossy balls instead, use PieceSet.Balls().
            boardView.SetPieces(PieceSet.RandomCreatures(seed));
            Controller = new GameController(GameState.NewGame(seed), new BoardPresenter(boardView));
            Relayout();
            Taps = gameObject.AddComponent<InputHandler>();
            Taps.Init(this);
            Debug.Log("Lines: game started");
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
        }
    }
}
