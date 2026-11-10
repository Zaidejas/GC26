using UnityEditor;

namespace Lines.EditorTools
{
    /// <summary>
    /// Imports the art exported from the prototype as sprites: the mushrooms (Assets/Resources/Mushrooms,
    /// transparent, sharp) and the forest backgrounds (Assets/Resources/Backgrounds, 1080×2160).
    /// </summary>
    sealed class ArtImportSettings : AssetPostprocessor
    {
        public const string MushroomFolder = "Assets/Resources/Mushrooms/";
        public const string BackgroundFolder = "Assets/Resources/Backgrounds/";

        void OnPreprocessTexture()
        {
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
