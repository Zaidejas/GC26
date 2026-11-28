using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Splash screen at every launch (GDD 4.0, T5A): "Colours / 2048" on the game background, holds, then fades out.
    /// A tap or click on it (a full-screen Button) or <see cref="Skip"/> (any key, called by the game) ends it at once.
    /// </summary>
    public sealed class Splash2048 : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Button skipArea;
        [SerializeField] float hold = 1.2f;
        [SerializeField] float fadeOut = 0.4f;

        public bool IsVisible => group != null && group.gameObject.activeSelf;

        /// <summary>Still holding: moves are ignored and input skips it. During the fade-out the game is already usable.</summary>
        public bool IsBlocking => IsVisible && group.blocksRaycasts;

        Coroutine _run;
        Action _onDone;

        void Awake()
        {
            if (skipArea != null) skipArea.onClick.AddListener(Skip);
        }

        /// <summary>Shows the splash; <paramref name="onDone"/> runs once when it is gone.</summary>
        public void Show(Action onDone)
        {
            StopRun();
            _onDone = onDone;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.gameObject.SetActive(true);
            if (isActiveAndEnabled) _run = StartCoroutine(Run());
            else Skip();
        }

        // Taps and clicks hit the full-screen skip button; keys and gamepad buttons end it here.
        void Update()
        {
            if (!IsBlocking) return;
            if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))
                Skip();
        }

        public void Skip()
        {
            if (!IsVisible) return;
            StopRun();
            Hide();
        }

        IEnumerator Run()
        {
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime)
                yield return null;
            group.blocksRaycasts = false; // the game is usable while the splash fades
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float p = Tween2048.Progress(t, fadeOut);
                group.alpha = 1f - Tween2048.EaseOut(p);
                if (p >= 1f) break;
                yield return null;
            }
            _run = null;
            Hide();
        }

        void Hide()
        {
            group.alpha = 1f;
            group.gameObject.SetActive(false);
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        void StopRun()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
        }

        void OnDisable()
        {
            StopRun();
            _onDone = null;
        }
    }
}
