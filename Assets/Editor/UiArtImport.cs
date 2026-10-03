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
        if (isUi && path.Contains("panel_parchment"))
            importer.wrapMode = TextureWrapMode.Repeat;

        // 9-slice: 1 px of texture = 1 canvas unit so wood frames stay readable.
        bool framed = path.Contains("panel_craft") || path.Contains("tab_beige") || path.Contains("ui_tab");
        if (framed)
        {
            importer.spritePixelsPerUnit = 1f;
            importer.spriteBorder = path.Contains("panel_craft")
                ? new Vector4(88f, 88f, 88f, 88f)
                : new Vector4(72f, 72f, 72f, 72f);
        }
    }
}
#endif
