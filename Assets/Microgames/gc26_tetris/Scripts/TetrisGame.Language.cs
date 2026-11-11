using GC26.Core;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// Russian and English. Every on-screen text is written as T("русский", "English") at the place it is drawn,
    /// so both versions sit side by side. L or the RU | EN button in the main menu switches; the choice is saved.
    /// </summary>
    public sealed partial class TetrisGame
    {
        static readonly string LanguageKey = MicrogameSave.Key(Id, "Language");

        bool english;

        void LoadLanguage()
        {
            // Russian until the player picks English.
            english = PlayerPrefs.GetString(LanguageKey, "ru") == "en";
        }

        void SetLanguage(bool toEnglish)
        {
            if (english == toEnglish)
                return;
            english = toEnglish;
            PlayerPrefs.SetString(LanguageKey, english ? "en" : "ru");
            PlayerPrefs.Save();
            gameAudio.Play("hold", 0.7f);
            Debug.Log("[BlockDrop] Language: " + (english ? "en" : "ru"));
        }

        /// <summary>The text in the current language.</summary>
        string T(string russian, string englishText)
        {
            return english ? englishText : russian;
        }

        string OnOff(bool on)
        {
            return on ? T("вкл.", "on") : T("выкл.", "off");
        }

        /// <summary>Menu: L switches, a click on either half of the RU | EN button picks that language.</summary>
        void HandleLanguageButton()
        {
            if (Pressed(Key.L))
                SetLanguage(!english);

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            Vector2 point = PointerWorld(mouse.position.ReadValue());
            Vector2 center = HudLayout.LanguageButtonCenter, size = HudLayout.LanguageButtonSize;
            if (Inside(point, center, size))
                SetLanguage(point.x > center.x);
        }

        void DrawLanguageButton(float unit)
        {
            Vector2 center = HudLayout.LanguageButtonCenter, size = HudLayout.LanguageButtonSize;
            Mouse mouse = Mouse.current;
            bool hover = mouse != null && Inside(PointerWorld(mouse.position.ReadValue()), center, size);
            Color accent = view.AccentColor;
            RoundedWorld(center.x, center.y, size.x, size.y, hover ? new Color(0.2f, 0.16f, 0.3f, 1f) : new Color(0.13f, 0.11f, 0.2f, 0.95f), 0.3f, 0f, unit);

            // The active half is filled with the accent colour.
            float half = size.x / 2f;
            float activeX = english ? center.x + half / 2f : center.x - half / 2f;
            RoundedWorld(activeX, center.y, half - 0.12f, size.y - 0.12f, accent, 0.25f, 0f, unit);
            RoundedWorld(center.x, center.y, size.x, size.y, new Color(accent.r, accent.g, accent.b, hover ? 0.9f : 0.45f), 0.3f, 0.05f, unit);

            Color dark = new Color(0.08f, 0.06f, 0.12f);
            Label("RU", center.x - half / 2f, center.y, unit, 0.46f, TextAnchor.MiddleCenter, english ? Muted : dark);
            Label("EN", center.x + half / 2f, center.y, unit, 0.46f, TextAnchor.MiddleCenter, english ? dark : Muted);
        }
    }
}
