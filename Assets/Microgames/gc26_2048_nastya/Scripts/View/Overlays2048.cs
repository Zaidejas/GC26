using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Full-screen windows over the board (GDD 3 and 4): win, game over and pause. At most one is open.
    /// Each panel has a dark blocker image, so the board and the HUD behind it cannot be clicked.
    /// Only displays and reports button clicks; Game2048Nastya decides what they do.
    /// </summary>
    public sealed class Overlays2048 : MonoBehaviour
    {
        public enum Kind { None, Win, GameOver, Pause }

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

        [SerializeField] float fadeIn = 0.4f;

        public event Action KeepGoingClicked;
        public event Action TryAgainClicked;
        public event Action ResumeClicked;
        /// <summary>"New game" on the win overlay or in the pause menu.</summary>
        public event Action NewGameClicked;
        /// <summary>"To menu" on the game over overlay or in the pause menu.</summary>
        public event Action ToMenuClicked;

        public Kind Open { get; private set; }
        public bool IsOpen => Open != Kind.None;

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
            HideAll();
        }

        public void ShowWin() => Show(Kind.Win, winPanel, fadeIn);

        public void ShowGameOver(int score, int best)
        {
            gameOverScoreText.SetText("Score  {0}\nBest  {1}", score, best);
            Show(Kind.GameOver, gameOverPanel, fadeIn);
        }

        /// <summary>The pause menu opens at once: it answers a button press, so it must not lag.</summary>
        public void ShowPause() => Show(Kind.Pause, pausePanel, 0f);

        public void HideAll()
        {
            StopFade();
            SetVisible(winPanel, false);
            SetVisible(gameOverPanel, false);
            SetVisible(pausePanel, false);
            Open = Kind.None;
        }

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
            foreach (var panel in new[] { winPanel, gameOverPanel, pausePanel })
                if (panel != null) panel.alpha = 1f;
        }
    }
}
