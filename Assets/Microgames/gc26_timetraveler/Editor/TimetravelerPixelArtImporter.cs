using UnityEditor;
using UnityEngine;

namespace GC26.Timetraveler.EditorTools
{
    /// <summary>
    /// Images dropped into this game's Art folder get pixel-art import settings automatically:
    /// 16 pixels per unit, Point filter (sharp pixels), no compression, no mipmaps.
    /// Art/Portraits keeps smooth filtering for the anime-style dialogue portraits.
    /// Only touches files inside this game's Art/ and Resources/ folders.
    /// </summary>
    public class TimetravelerPixelArtImporter : AssetPostprocessor
    {
        public const int PixelsPerUnit = 16;
        const string ArtRoot = "Assets/Microgames/" + TimetravelerGame.Id + "/Art/";
        const string ResourcesRoot = "Assets/Microgames/" + TimetravelerGame.Id + "/Resources/";

        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ArtRoot) && !path.StartsWith(ResourcesRoot)) return;

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

            // Art slots: centred pivot and full-rect meshes, so tiled slots (ground, paths) repeat cleanly.
            if (path.Contains("/Slots/"))
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
        }
    }
}
