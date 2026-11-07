using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Lines.UI
{
    /// <summary>
    /// The prototype's rounded fonts (free Google Fonts, SIL Open Font License; licences next to the
    /// files): Fredoka SemiBold for numbers and titles, Nunito for everything else (Regular, and
    /// ExtraBold for bold text). The font files are in Resources/Fonts; their TextMeshPro versions are
    /// made from code when first needed, so no font assets have to be built by hand. If the files are
    /// missing, text falls back to TextMeshPro's default font.
    /// </summary>
    public static class UiFonts
    {
        public const string BodyFile = "Nunito-Regular", BoldFile = "Nunito-ExtraBold", DisplayFile = "Fredoka-SemiBold";

        static TMP_FontAsset body, bold, display;

        /// <summary>Nunito; text with the Bold style is drawn with Nunito ExtraBold.</summary>
        public static TMP_FontAsset Body { get { Load(); return body; } }
        /// <summary>Fredoka SemiBold (numbers and titles).</summary>
        public static TMP_FontAsset Display { get { Load(); return display; } }

        static void Load()
        {
            if (body != null && display != null) return;
            body = Create(BodyFile);
            bold = Create(BoldFile);
            display = Create(DisplayFile);
            if (body != null && bold != null)
            {
                // TextMeshPro looks up bold text here (weight 700) and uses the real ExtraBold letters.
                body.fontWeightTable[7] = new TMP_FontWeightPair { regularTypeface = bold, italicTypeface = bold };
                bold.fontWeightTable[7] = new TMP_FontWeightPair { regularTypeface = bold, italicTypeface = bold };
            }
            if (display != null && body != null)
                display.fallbackFontAssetTable = new List<TMP_FontAsset> { body }; // e.g. "·" if Fredoka lacks it
        }

        static TMP_FontAsset Create(string file)
        {
            var font = Resources.Load<Font>("Fonts/" + file);
            if (font == null) return null;
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset != null) asset.name = file;
            return asset;
        }

        /// <summary>Body text: Nunito (bold text in Nunito ExtraBold).</summary>
        public static void UseBody(TMP_Text text)
        {
            if (Body != null) text.font = Body;
        }

        /// <summary>Numbers and titles: Fredoka SemiBold (already heavy, so no extra bold on top).</summary>
        public static void UseDisplay(TMP_Text text)
        {
            if (Display == null) return;
            text.font = Display;
            text.fontStyle &= ~FontStyles.Bold;
        }
    }
}
