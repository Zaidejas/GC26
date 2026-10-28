using UnityEngine;
using UnityEngine.UI;

// Пауза по Esc / P: останавливает время и звук
public class PauseMenu : MonoBehaviour
{
    public GameObject panel;
    public Button resumeButton;
    public Button restartButton;
    public Button menuButton;

    public bool IsPaused { get; private set; }

    void Start()
    {
        panel.SetActive(false);
        resumeButton.onClick.AddListener(Resume);
        restartButton.onClick.AddListener(() => GameManager.Instance.Restart());
        menuButton.onClick.AddListener(() => GameManager.Instance.LoadMenu());
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
        if (GameManager.Instance.IsGameOver) return;

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
