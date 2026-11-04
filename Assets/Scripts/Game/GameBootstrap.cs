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
        GameState game;
        Vector2 lastScreenSize;
        Rect lastSafeArea;

        void Awake()
        {
            Application.targetFrameRate = 60;
            mainCamera = Camera.main;
            boardView = BoardView.Create();
            game = GameState.NewGame(System.Environment.TickCount);
            boardView.ShowBoard(game.Board);
            Relayout();
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
            var layout = BoardLayout.Compute(lastScreenSize, lastSafeArea);
            CameraSetup.Apply(mainCamera, layout);
            boardView.ApplyLayout(layout);
        }
    }
}
