using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>Top bar: score, best, Pause and New game buttons, and the "start a new game?" confirmation.</summary>
    public sealed class Hud2048 : MonoBehaviour
    {
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] Button pauseButton;
        [SerializeField] Button newGameButton;

        [Header("New game confirmation")]
        [SerializeField] GameObject confirmPanel;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        public event Action PauseClicked;
        public event Action NewGameClicked;

        Action _onConfirm;

        public bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

        void Awake()
        {
            pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
            newGameButton.onClick.AddListener(() => NewGameClicked?.Invoke());
            confirmYesButton.onClick.AddListener(Confirm);
            confirmNoButton.onClick.AddListener(CloseConfirm);
            confirmPanel.SetActive(false);
        }

        public void SetScore(int score, int best)
        {
            scoreText.SetText("{0}", score);
            bestText.SetText("{0}", best);
        }

        public void ShowConfirm(Action onYes)
        {
            _onConfirm = onYes;
            confirmPanel.SetActive(true);
        }

        public void CloseConfirm()
        {
            _onConfirm = null;
            confirmPanel.SetActive(false);
        }

        void Confirm()
        {
            var action = _onConfirm;
            CloseConfirm();
            action?.Invoke();
        }
    }
}
