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
        static Material displayMaterial;

        // Mystery Quest comes in one thin weight; Unity draws it thinner than a browser, so its letters are
        // thickened a little (the prototype's look) through the text material.
        const float DisplayThickening = 0.22f;

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
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset != null) asset.name = file;
            return asset;
        }

        /// <summary>Body text: Nunito (bold text in Nunito ExtraBold).</summary>
        public static void UseBody(TMP_Text text)
        {
            if (Body != null) text.font = Body;
        }

        /// <summary>Title, numbers, labels, buttons and headings: Mystery Quest (one weight, so no fake bold on top).</summary>
        public static void UseDisplay(TMP_Text text)
        {
            if (Display == null) return;
            text.font = Display;
            text.fontStyle &= ~FontStyles.Bold;
            if (displayMaterial == null || displayMaterial.mainTexture != Display.material.mainTexture)
            {
                displayMaterial = new Material(Display.material) { name = "Mystery Quest (bolder)" };
                displayMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, DisplayThickening);
            }
            text.fontSharedMaterial = displayMaterial;
        }

    }
}
