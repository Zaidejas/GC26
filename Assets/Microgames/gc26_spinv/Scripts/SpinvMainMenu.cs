using System.Collections;
using TMPro;
using UnityEngine;

namespace GC26.Spinv
{
    // Главное меню: рекорды и кнопки. Пока аркада закрыта - HI-SCORE по центру, PLAY и QUIT.
    // После прохождения игры: слева HI-SCORE, справа рекорд аркады; кнопки CLASSIC, ARCADE MODE и QUIT.
    // Меню и игра в одной сцене: при каждом возвращении в меню всё пересчитывается (OnEnable)
    public class SpinvMainMenu : MonoBehaviour
    {
        public SpinvMicrogame flow;

        public TMP_Text highScoreLabel;
        public TMP_Text highScoreText;
        public TMP_Text arcadeScoreLabel;
        public TMP_Text arcadeScoreText;
        [Tooltip("На сколько вбок разъезжаются рекорды, когда их два")]
        public float scoreColumnOffset = 260f;

        public PressableButton playButton;
        public PressableButton arcadeButton;
        public PressableButton quitButton;
        public string playLabel = "PLAY";
        public string classicLabel = "CLASSIC";
        [Tooltip("Где стоит QUIT, когда аркада открыта и кнопок три")]
        public float quitYWithArcade = -530f;

        [Tooltip("Выключение автомата перед выходом. Без него игра закрывается сразу")]
        public QuitEffect quitEffect;

        [Header("Достижения")]
        public PressableButton trophyButton;
        public TMP_Text trophyCount;
        public StarMapScreen starMap;

        float quitYAlone;

        void Awake()
        {
            quitYAlone = ((RectTransform)quitButton.transform).anchoredPosition.y;
            trophyButton.onPress.AddListener(starMap.Open);
            playButton.onPress.AddListener(() => Play(false));
            arcadeButton.onPress.AddListener(() => Play(true));
            quitButton.onPress.AddListener(Quit);
        }

        void OnEnable()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            // Сначала возможный сброс сохранений: счётчик кубка и рекорды должны видеть его результат
            HighScoreTable.Load();
            RefreshTrophy();
            StartCoroutine(ShowPendingToasts());

            // Личный рекорд игрока
            highScoreText.text = PlayerPrefs.GetInt(SpinvGameManager.HighScoreKey, 0).ToString("00000");

            bool arcade = ArcadeMode.IsUnlocked;
            arcadeScoreLabel.gameObject.SetActive(arcade);
            arcadeScoreText.gameObject.SetActive(arcade);
            arcadeButton.gameObject.SetActive(arcade);
            float column = arcade ? scoreColumnOffset : 0f;
            SetX(highScoreLabel, -column);
            SetX(highScoreText, -column);
            SetX(arcadeScoreLabel, column);
            SetX(arcadeScoreText, column);
            if (arcade) arcadeScoreText.text = ArcadeMode.HighScore.ToString("00000");

            var quitRect = (RectTransform)quitButton.transform;
            quitRect.anchoredPosition = new Vector2(quitRect.anchoredPosition.x, arcade ? quitYWithArcade : quitYAlone);
            if (playButton.label != null) playButton.label.text = arcade ? classicLabel : playLabel;
        }

        void RefreshTrophy()
        {
            trophyCount.text = $"{Achievements.UnlockedVisibleCount} / {Achievements.VisibleCount}";
        }

        // Отложенные плашки (IMPOSTOR): после заставки включения, иначе их закроет TAP TO START
        IEnumerator ShowPendingToasts()
        {
            var intro = FindAnyObjectByType<IntroBoot>();
            while (intro != null && intro.isActiveAndEnabled)
                yield return null;

            var toast = FindAnyObjectByType<AchievementToast>();
            if (toast != null) Achievements.ShowPendingToasts(toast);
        }

    #if UNITY_EDITOR
        // Только в редакторе: F6 - полный сброс сохранений и меню заново, как после первой установки
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.f6Key.wasPressedThisFrame) return;
            HighScoreTable.WipeAll();
            flow.ReloadMenu();
        }
    #endif

        static void SetX(TMP_Text text, float x)
        {
            var rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }

        void Play(bool arcade)
        {
            ArcadeMode.Select(arcade);
            flow.StartGame();
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
}
