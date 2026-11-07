using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню: рекорд, кнопки «Играть» и «Выход»
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "Game";

    public TMP_Text highScoreText;
    public PressableButton playButton;
    public PressableButton quitButton;
    [Tooltip("Выключение автомата перед выходом. Без него игра закрывается сразу")]
    public QuitEffect quitEffect;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // Подпись HI-SCORE стоит отдельно над числом
        // Личный рекорд игрока. HighScoreTable.Load() заодно применяет сброс таблицы, если он новый
        HighScoreTable.Load();
        highScoreText.text = PlayerPrefs.GetInt(GameManager.HighScoreKey, 0).ToString("00000");

        playButton.onPress.AddListener(Play);
        quitButton.onPress.AddListener(Quit);
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
