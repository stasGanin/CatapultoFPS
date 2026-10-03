#if UNITY_EDITOR
using UnityEditor;

/// <summary>Import flags for authored castle module FBX files.</summary>
public sealed class CastleModuleImport : AssetPostprocessor
{
    const string Folder = "/Resources/Castle/Modules/Source/";

    void OnPreprocessModel()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(Folder) || !path.EndsWith(".fbx"))
            return;

        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.addCollider = false;
        importer.preserveHierarchy = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.isReadable = true;
        importer.importCameras = false;
        importer.importLights = false;
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool modulesImported = false;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i].Replace('\\', '/');
            if (path.Contains(Folder) && path.EndsWith(".fbx"))
            {
                modulesImported = true;
                break;
            }
        }

        if (!modulesImported)
            return;

        EditorApplication.delayCall += () => CastleModulePrefabGenerator.GenerateMissing();
    }
}
#endif
