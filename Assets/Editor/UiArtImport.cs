#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Import UI / item icons as sprites for inventory.</summary>
public sealed class UiArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        bool isUi = path.Contains("/Art/UI/") || path.Contains("/Resources/UI/");
        bool isIcon = path.Contains("/Art/Items/") || path.Contains("/Resources/ItemIcons/");
        if (!isUi && !isIcon)
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100f;
        // Рамки UI (frame_*) — 9-slice; границы задаются при генерации и живут в .meta.
    }
}
#endif
