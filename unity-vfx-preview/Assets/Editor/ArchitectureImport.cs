using UnityEditor;

namespace Trias.Preview
{
    // Import contract for the Blender-baked plate atlases (tools/blender/build-plate-atlas.py).
    // Data maps hold ids, occlusion and height and must stay linear; normal maps are tangent space.
    sealed class ArchitectureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/TRIAS/Architecture/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.maxTextureSize = 2048;
            if (assetPath.EndsWith("-normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.EndsWith("-data.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }
    }
}
