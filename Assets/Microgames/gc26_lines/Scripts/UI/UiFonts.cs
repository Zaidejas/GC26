using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GC26.Lines.UI
{
    /// <summary>
    /// The prototype's fonts (free Google Fonts, SIL Open Font License; licences next to the files):
    /// Mystery Quest for the title, numbers, labels, buttons and headings; Nunito for longer text
    /// (Regular, and ExtraBold for bold text). Mystery Quest has only Latin letters, so Russian letters in
    /// those places come from Kurale (a fairy-tale serif with Cyrillic); Nunito has Cyrillic itself. The font files are in Resources/gc26_lines/Fonts; their TextMeshPro versions are
    /// made from code when first needed, so no font assets have to be built by hand. If the files are
    /// missing, text falls back to TextMeshPro's default font.
    /// </summary>
    public static class UiFonts
    {
        public const string BodyFile = "Nunito-Regular", BoldFile = "Nunito-ExtraBold", DisplayFile = "MysteryQuest-Regular";
        public const string CyrillicDisplayFile = "Kurale-Regular";

        static TMP_FontAsset body, bold, display, cyrillicDisplay;

        // Each letter is drawn once at this size into a 2048×2048 letter image, then scaled to any text size.
        // 180 keeps Mystery Quest's fine brush edges and curls crisp even in the big title (at 90 they were
        // rounded off); the padding around each letter is a tenth of it.
        public const int SamplingSize = 180, AtlasSize = 2048;

        /// <summary>Nunito; text with the Bold style is drawn with Nunito ExtraBold.</summary>
        public static TMP_FontAsset Body { get { Load(); return body; } }
        /// <summary>Kurale: Russian letters in titles, labels and buttons (a fallback of <see cref="Display"/>).</summary>
        public static TMP_FontAsset CyrillicDisplay { get { Load(); return cyrillicDisplay; } }
        /// <summary>Mystery Quest (title, numbers, labels, buttons, headings).</summary>
        public static TMP_FontAsset Display { get { Load(); return display; } }

        static void Load()
        {
            if (body != null && display != null) return;
            body = Create(BodyFile);
            bold = Create(BoldFile);
            display = Create(DisplayFile);
            cyrillicDisplay = Create(CyrillicDisplayFile);
            if (body != null && bold != null)
            {
                // TextMeshPro looks up bold text here (weight 700) and uses the real ExtraBold letters.
                body.fontWeightTable[7] = new TMP_FontWeightPair { regularTypeface = bold, italicTypeface = bold };
                bold.fontWeightTable[7] = new TMP_FontWeightPair { regularTypeface = bold, italicTypeface = bold };
            }
            if (display != null)
            {
                // Letters Mystery Quest lacks: Russian from Kurale, anything else (e.g. "·") from Nunito.
                var fallbacks = new List<TMP_FontAsset>();
                if (cyrillicDisplay != null) fallbacks.Add(cyrillicDisplay);
                if (body != null) fallbacks.Add(body);
                display.fallbackFontAssetTable = fallbacks;
            }
        }

        static TMP_FontAsset Create(string file)
        {
            var font = Resources.Load<Font>("gc26_lines/Fonts/" + file);
            if (font == null) return null;
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, SamplingSize / 10, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset != null) asset.name = file;
            return asset;
        }

        /// <summary>
        /// Prepares every letter the game can show, a few per frame, while the menu is up (task 35). Letters are
        /// otherwise drawn into the font the first time they appear, and drawing several at once (new digits in the
        /// score, a pop-up opening for the first time) made a frame late enough to see. The current language
        /// goes first, then the other one.
        /// </summary>
        public static IEnumerator WarmUp(float millisecondsPerFrame = 3f)
        {
            Load();
            var other = Texts.Current == Language.Russian ? Language.English : Language.Russian;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var language in new[] { Texts.Current, other })
            foreach (char c in Texts.Letters(language))
            {
                string letter = c.ToString();
                bool cyrillic = c >= '\u0400' && c <= '\u04FF';
                foreach (var font in new[] { body, bold, cyrillic ? cyrillicDisplay : display })
                    if (font != null) font.TryAddCharacters(letter, out _);
                if (clock.Elapsed.TotalMilliseconds >= millisecondsPerFrame) // a small share of each frame only
                {
                    yield return null;
                    clock.Restart();
                }
            }
        }

        /// <summary>How many letters the fonts have drawn so far (the test build's frame-rate log shows it).</summary>
        public static int LetterCount
        {
            get
            {
                int n = 0;
                foreach (var font in new[] { body, bold, display, cyrillicDisplay })
                    if (font != null) n += font.characterTable.Count;
                return n;
            }
        }

        /// <summary>Body text: Nunito (bold text in Nunito ExtraBold).</summary>
        public static void UseBody(TMP_Text text)
        {
            if (Body != null) text.font = Body;
        }

        /// <summary>
        /// Title, numbers, labels, buttons and headings: Mystery Quest, drawn as designed (thin, like the
        /// prototype; thickening it filled in its curls and softened its brush edges). One weight, so no fake bold.
        /// </summary>
        public static void UseDisplay(TMP_Text text)
        {
            if (Display == null) return;
            text.font = Display;
            text.fontStyle &= ~FontStyles.Bold;
        }

    }
}
