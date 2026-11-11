using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Пауза по Esc / P: останавливает время и звук
    public class PauseMenu : MonoBehaviour
    {
        public GameObject panel;
        public PressableButton resumeButton;
        public PressableButton restartButton;
        public PressableButton menuButton;
        [Tooltip("Кнопка паузы на экране: на телефоне нет Esc")]
        public PressableButton pauseButton;

        public bool IsPaused { get; private set; }

        void Start()
        {
            panel.SetActive(false);
            pauseButton.onPress.AddListener(Pause);
            resumeButton.onPress.AddListener(Resume);
            restartButton.onPress.AddListener(() => SpinvGameManager.Instance.Restart());
            menuButton.onPress.AddListener(() => SpinvGameManager.Instance.LoadMenu());
        }

        // Паузу выключили вместе с игрой (выход в меню, рестарт): время и звук - как были
        void OnDisable()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Update()
        {
            if (!GameInput.PausePressed()) return;

            if (IsPaused) Resume();
            else Pause();
        }

    #if !UNITY_EDITOR
        // В билде ставим на паузу, если игрок свернул окно (Alt+Tab)
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !IsPaused) Pause();
        }
    #endif

        public void Pause()
        {
            if (SpinvGameManager.Instance.IsGameOver) return;

            IsPaused = true;
            panel.SetActive(true);
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public void Resume()
        {
            IsPaused = false;
            panel.SetActive(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
