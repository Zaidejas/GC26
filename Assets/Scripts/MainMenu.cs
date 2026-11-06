using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню: рекорд, накопленные монеты, кнопки «Играть» и «Выход»
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "Game";

    public TMP_Text highScoreText;
    public TMP_Text coinsText;
    public Button playButton;
    public Button quitButton;
    [Tooltip("Выключение автомата перед выходом. Без него игра закрывается сразу")]
    public QuitEffect quitEffect;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        highScoreText.text = $"BEST {PlayerPrefs.GetInt(GameManager.HighScoreKey, 0)}";
        coinsText.text = $"COINS {PlayerPrefs.GetInt(GameManager.TotalCoinsKey, 0)}";

        playButton.onClick.AddListener(Play);
        quitButton.onClick.AddListener(Quit);
    }

    void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    void Quit()
    {
        if (quitEffect != null)
            quitEffect.Play(QuitNow);
        else
            QuitNow();
    }

    void QuitNow()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
