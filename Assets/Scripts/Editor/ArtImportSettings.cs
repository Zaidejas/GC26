using UnityEditor;

namespace Lines.EditorTools
{
    /// <summary>
    /// Imports the art exported from the prototype as sprites: the mushrooms (Assets/Resources/Mushrooms,
    /// transparent, sharp), the forest backgrounds (Assets/Resources/Backgrounds, 1080×2160) and the app
    /// icon and splash logo (Assets/Art/AppIcon, 1024×1024, not in Resources: only the build needs them).
    /// </summary>
    sealed class ArtImportSettings : AssetPostprocessor
    {
        public const string MushroomFolder = "Assets/Resources/Mushrooms/";
        public const string BackgroundFolder = "Assets/Resources/Backgrounds/";
        public const string AppIconFolder = "Assets/Art/AppIcon/";

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(AppIconFolder))
            {
                // The app icon, its two Android layers and the splash logo (task 32): exact pixels, no compression.
                var icon = (TextureImporter)assetImporter;
                icon.textureType = TextureImporterType.Sprite; // the splash logo must be a sprite; icons accept it too
                icon.spriteImportMode = SpriteImportMode.Single;
                icon.alphaIsTransparency = true;
                icon.mipmapEnabled = false;
                icon.maxTextureSize = 1024;
                icon.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            bool mushroom = assetPath.StartsWith(MushroomFolder), background = assetPath.StartsWith(BackgroundFolder);
            if (!mushroom && !background) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = mushroom;
            importer.mipmapEnabled = mushroom; // pieces are drawn small (preview dots), so they stay smooth
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = background ? 4096 : 1024;
            importer.textureCompression = mushroom ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        }
    }
}
