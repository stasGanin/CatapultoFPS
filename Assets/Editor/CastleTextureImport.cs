#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Normal maps for castle/loot textures.
/// </summary>
public class CastleTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        bool isNormal = path.Contains("CastleBrick_Normal") || path.Contains("StoneLoot_Normal");
        if (!isNormal)
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
    }
}
#endif
