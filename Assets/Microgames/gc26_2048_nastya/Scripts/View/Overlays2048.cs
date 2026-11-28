using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Full-screen windows over the board (GDD 4): start menu, tutorial, settings, win, game over and pause.
    /// At most one is open. Each panel blocks clicks to the board and the HUD behind it.
    /// Only displays and reports button clicks; Game2048Nastya decides what they do. Moving between the
    /// start menu, the tutorial and the settings is pure navigation, so it stays here.
    /// </summary>
    public sealed class Overlays2048 : MonoBehaviour
    {
        public enum Kind { None, Win, GameOver, Pause, Menu, Tutorial, Settings }

        [Header("Start menu")]
        [SerializeField] CanvasGroup menuPanel;
        [SerializeField] Button menuNewGameButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button tutorialButton;
        [SerializeField] Button settingsButton;
        [SerializeField] TMP_Text versionText;

        [Header("Tutorial")]
        [SerializeField] CanvasGroup tutorialPanel;
        [SerializeField] Button tutorialBackButton;

        [Header("Settings")]
        [SerializeField] CanvasGroup settingsPanel;
        [SerializeField] Button settingsSoundButton;
        [SerializeField] TMP_Text settingsSoundLabel;
        [SerializeField] Button settingsBackButton;

        [Header("Win: \"You made 2048!\"")]
        [SerializeField] CanvasGroup winPanel;
        [SerializeField] Button keepGoingButton;
        [SerializeField] Button winNewGameButton;

        [Header("Game over: \"No more moves\"")]
        [SerializeField] CanvasGroup gameOverPanel;
        [SerializeField] TMP_Text gameOverScoreText;
        [SerializeField] Button tryAgainButton;
        [SerializeField] Button gameOverMenuButton;

        [Header("Pause")]
        [SerializeField] CanvasGroup pausePanel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button pauseNewGameButton;
        [SerializeField] Button pauseMenuButton;
        [SerializeField] Button soundButton;
        [SerializeField] TMP_Text soundLabel;

        [SerializeField] float fadeIn = 0.4f;

        public event Action KeepGoingClicked;
        public event Action TryAgainClicked;
        public event Action ResumeClicked;
        /// <summary>"New game" on the win overlay or in the pause menu.</summary>
        public event Action NewGameClicked;
        /// <summary>"To menu" on the game over overlay or in the pause menu.</summary>
        public event Action ToMenuClicked;
        /// <summary>"Sound Effects: On/Off" in the pause menu or in the settings.</summary>
        public event Action SoundClicked;
        /// <summary>"New Game" in the start menu.</summary>
        public event Action MenuNewGameClicked;
        /// <summary>"Continue" in the start menu.</summary>
        public event Action ContinueClicked;

        public Kind Open { get; private set; }
        public bool IsOpen => Open != Kind.None;
        public bool CanContinue => continueButton != null && continueButton.interactable;
        public string VersionLabel => versionText != null ? versionText.text : null;

        Coroutine _fade;

        void Awake()
        {
            keepGoingButton.onClick.AddListener(() => KeepGoingClicked?.Invoke());
            winNewGameButton.onClick.AddListener(() => NewGameClicked?.Invoke());
            tryAgainButton.onClick.AddListener(() => TryAgainClicked?.Invoke());
            gameOverMenuButton.onClick.AddListener(() => ToMenuClicked?.Invoke());
            resumeButton.onClick.AddListener(() => ResumeClicked?.Invoke());
            pauseNewGameButton.onClick.AddListener(() => NewGameClicked?.Invoke());
            pauseMenuButton.onClick.AddListener(() => ToMenuClicked?.Invoke());
            soundButton.onClick.AddListener(() => SoundClicked?.Invoke());

            menuNewGameButton.onClick.AddListener(() => MenuNewGameClicked?.Invoke());
            continueButton.onClick.AddListener(() => ContinueClicked?.Invoke());
            tutorialButton.onClick.AddListener(ShowTutorial);
            settingsButton.onClick.AddListener(ShowSettings);
            tutorialBackButton.onClick.AddListener(BackToMenu);
            settingsSoundButton.onClick.AddListener(() => SoundClicked?.Invoke());
            settingsBackButton.onClick.AddListener(BackToMenu);
            HideAll();
        }

        /// <summary>The start menu after the splash. "Continue" works only when a saved game was loaded.</summary>
        public void ShowMenu(bool canContinue, string version)
        {
            continueButton.interactable = canContinue;
            versionText.SetText(version);
            Show(Kind.Menu, menuPanel, 0f);
        }

        public void ShowTutorial() => Show(Kind.Tutorial, tutorialPanel, 0f);
        public void ShowSettings() => Show(Kind.Settings, settingsPanel, 0f);
        public void BackToMenu() => Show(Kind.Menu, menuPanel, 0f);

        public void ShowWin() => Show(Kind.Win, winPanel, fadeIn);

        public void ShowGameOver(int score, int best)
        {
            gameOverScoreText.SetText("Score  {0}\nBest  {1}", score, best);
            Show(Kind.GameOver, gameOverPanel, fadeIn);
        }

        /// <summary>The pause menu opens at once: it answers a button press, so it must not lag.</summary>
        public void ShowPause() => Show(Kind.Pause, pausePanel, 0f);

        public void SetSound(bool on)
        {
            string label = on ? "Sound Effects: On" : "Sound Effects: Off";
            soundLabel.SetText(label);
            settingsSoundLabel.SetText(label);
        }

        public void HideAll()
        {
            StopFade();
            foreach (var panel in Panels()) SetVisible(panel, false);
            Open = Kind.None;
        }

        CanvasGroup[] Panels() => new[] { menuPanel, tutorialPanel, settingsPanel, winPanel, gameOverPanel, pausePanel };

        void Show(Kind kind, CanvasGroup panel, float duration)
        {
            HideAll();
            Open = kind;
            SetVisible(panel, true);
            if (duration > 0f && isActiveAndEnabled)
            {
                panel.alpha = 0f;
                _fade = StartCoroutine(Fade(panel, duration));
            }
        }

        IEnumerator Fade(CanvasGroup panel, float duration)
        {
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float p = Tween2048.Progress(t, duration);
                panel.alpha = Tween2048.EaseOut(p);
                if (p >= 1f) break;
                yield return null;
            }
            _fade = null;
        }

        void StopFade()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = null;
        }

        static void SetVisible(CanvasGroup panel, bool visible)
        {
            if (panel == null) return;
            panel.alpha = 1f;
            // Clicks are blocked from the first frame, even while the panel is still fading in.
            panel.blocksRaycasts = visible;
            panel.interactable = visible;
            panel.gameObject.SetActive(visible);
        }

        void OnDisable()
        {
            StopFade();
            foreach (var panel in Panels())
                if (panel != null) panel.alpha = 1f;
        }
    }
}
