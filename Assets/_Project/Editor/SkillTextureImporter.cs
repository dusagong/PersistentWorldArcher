using UnityEditor;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public sealed class SkillTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath != "Assets/_Project/Resources/Effects/SkillOneVolley.png" &&
                assetPath != "Assets/_Project/Resources/Effects/SkillTwoArrowRain.png" &&
                assetPath != "Assets/_Project/Resources/Effects/SkillThreeCast.png" &&
                assetPath != "Assets/_Project/Resources/Effects/SkillThreeFlight.png" &&
                assetPath != "Assets/_Project/Resources/Effects/BasicAttackBow.png" &&
                assetPath != "Assets/_Project/Resources/Effects/BasicAttackArrow.png") return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
