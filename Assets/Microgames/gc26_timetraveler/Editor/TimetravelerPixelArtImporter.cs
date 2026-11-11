using UnityEditor;
using UnityEngine;

namespace GC26.Timetraveler.EditorTools
{
    /// <summary>
    /// Images dropped into this game's Art folder get pixel-art import settings automatically:
    /// 16 pixels per unit, Point filter (sharp pixels), no compression, no mipmaps.
    /// Art/Portraits keeps smooth filtering for the anime-style dialogue portraits.
    /// Only touches files inside Assets/Microgames/gc26_timetraveler/Art/.
    /// </summary>
    public class TimetravelerPixelArtImporter : AssetPostprocessor
    {
        public const int PixelsPerUnit = 16;
        const string ArtRoot = "Assets/Microgames/" + TimetravelerGame.Id + "/Art/";

        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            // Only on first import, so manual tweaks in the Inspector survive.
            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            if (path.StartsWith(ArtRoot + "Portraits/"))
            {
                importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
