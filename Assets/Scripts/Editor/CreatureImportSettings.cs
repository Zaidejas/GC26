using UnityEditor;

namespace Lines.EditorTools
{
    /// <summary>Imports the creature images (Assets/Resources/Creatures) as transparent sprites.</summary>
    sealed class CreatureImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/Resources/Creatures/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
