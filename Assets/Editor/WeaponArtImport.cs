#if UNITY_EDITOR
using UnityEditor;

/// <summary>Import flags for first-person weapon FBX files.</summary>
public sealed class WeaponArtImport : AssetPostprocessor
{
    const string WeaponsFolder = "/Resources/Weapons/";

    void OnPreprocessModel()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(WeaponsFolder) || !path.EndsWith(".fbx"))
            return;

        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.addCollider = false;
        importer.preserveHierarchy = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }
}
#endif
