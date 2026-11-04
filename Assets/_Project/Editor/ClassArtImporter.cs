using UnityEditor;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public sealed class ClassArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            const string root = "Assets/_Project/Resources/";
            bool character = assetPath.StartsWith(root + "Characters/Warrior") || assetPath.StartsWith(root + "Characters/Healer");
            bool effect = assetPath.StartsWith(root + "Effects/Warrior") || assetPath.StartsWith(root + "Effects/Healer") || assetPath == root + "Effects/ArcherSpecial.png";
            if (!character && !effect) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            bool portrait = assetPath.Contains("InventoryIdle");
            importer.textureType = portrait ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (portrait) importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = character ? 8192 : 2048;
            importer.isReadable = character && !portrait;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
