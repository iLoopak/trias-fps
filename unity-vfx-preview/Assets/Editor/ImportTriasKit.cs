using UnityEditor;
using UnityEngine;

namespace Trias.Preview
{
    internal sealed class ImportTriasKit : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/TRIAS/Kit/", System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            if (!assetPath.Contains("/Pilots/")) return;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.preserveHierarchy = true;
            importer.importAnimation = !assetPath.EndsWith("/characterMedium.fbx", System.StringComparison.Ordinal);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
