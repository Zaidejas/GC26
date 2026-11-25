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
        [SerializeField] Button undoButton;

        [Tooltip("\"Swipe or use the arrow keys...\" under the board, first launch only (GDD 4.1)")]
        [SerializeField] TMP_Text hintText;

        [Header("New game confirmation")]
        [SerializeField] GameObject confirmPanel;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        public event Action PauseClicked;
        public event Action NewGameClicked;
        public event Action UndoClicked;

        Action _onConfirm;

        public bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

        void Awake()
        {
            pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
            newGameButton.onClick.AddListener(() => NewGameClicked?.Invoke());
            if (undoButton != null) undoButton.onClick.AddListener(() => UndoClicked?.Invoke());
            confirmYesButton.onClick.AddListener(Confirm);
            confirmNoButton.onClick.AddListener(CloseConfirm);
            confirmPanel.SetActive(false);
            ShowHint(false);
        }

        public void SetScore(int score, int best)
        {
            scoreText.SetText("{0}", score);
            bestText.SetText("{0}", best);
        }

        /// <summary>T54: the Undo button is greyed out when there is nothing to undo.</summary>
        public void SetUndoAvailable(bool available)
        {
            if (undoButton != null) undoButton.interactable = available;
        }

        public bool IsUndoAvailable => undoButton != null && undoButton.interactable;

        public bool IsHintVisible => hintText != null && hintText.gameObject.activeSelf;

        public void ShowHint(bool visible)
        {
            if (hintText != null) hintText.gameObject.SetActive(visible);
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
            ShowHint(false);
        }

        void Confirm()
        {
            var action = _onConfirm;
            CloseConfirm();
            action?.Invoke();
        }
    }
}
