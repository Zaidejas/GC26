using UnityEditor;
using UnityEngine;

namespace GC26.Tanki.EditorTools
{
    /// <summary>
    /// Pictures and models dropped into this game's Resources folders get the right settings automatically.
    /// Only touches files under Assets/Microgames/gc26_tanki/, never other games' assets.
    /// </summary>
    public sealed class TankiAssetImport : AssetPostprocessor
    {
        const string Root = "Assets/Microgames/gc26_tanki/Resources/gc26_tanki/";

        bool Mine => assetPath.StartsWith(Root);
        bool IsPixelArt => Mine && (assetPath.StartsWith(Root + "Tiles/") || assetPath.StartsWith(Root + "Bonuses/"));
        bool IsGameArt => Mine && (assetPath.StartsWith(Root + "Tanks/") || IsPixelArt);

        void OnPreprocessModel()
        {
            if (!IsGameArt) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
        }

        void OnPreprocessTexture()
        {
            if (Mine && assetPath.StartsWith(Root + "Backgrounds/"))
            {
                // Menu pictures: small pixel art shown full screen, so no smoothing, no mipmaps and no shrinking.
                var picture = (TextureImporter)assetImporter;
                picture.mipmapEnabled = false;
                picture.npotScale = TextureImporterNPOTScale.None;
                picture.wrapMode = TextureWrapMode.Clamp;
                picture.filterMode = FilterMode.Point;
                picture.maxTextureSize = 1024;
                picture.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (!IsGameArt) return;
            var importer = (TextureImporter)assetImporter;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Keeps the pixel art crisp up close, like the tanks' drawings.
            if (IsPixelArt) importer.filterMode = FilterMode.Point;
        }
    }
}
