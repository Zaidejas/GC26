using UnityEngine;

namespace Lines.Game
{
    /// <summary>
    /// The only object placed in the Main scene. Everything else
    /// (board, balls, UI) is created from code starting here.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            Debug.Log("Lines: game started");
        }
    }
}
