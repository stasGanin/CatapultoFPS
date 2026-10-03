#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot bake of the procedural T1 frame into an editable prefab.
/// Menu: Catapulto / Castle / Generate PlayerCastle_T1 Prefab
/// </summary>
public static class CastlePlayerPrefabGenerator
{
    public const string PrefabFolder = "Assets/Prefabs/Castle";
    public const string PrefabPath = PrefabFolder + "/PlayerCastle_T1.prefab";
    public const string MagePrefabPath = PrefabFolder + "/MageHeart.prefab";

    [MenuItem("Catapulto/Castle/Generate PlayerCastle_T1 Prefab")]
    public static void Generate()
    {
        EnsureFolder(PrefabFolder);

        // Remove previous scene leftovers from a failed run
        var existing = Object.FindObjectsByType<CastleFrame>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].name.StartsWith("PlayerCastle_T1"))
                Object.DestroyImmediate(existing[i].gameObject);
        }

        var frame = CastleFrameBuilder.BuildTier1(Vector3.zero, 0f, CastleFrameBuilder.PadFitRadius);
        GameObject root = frame.gameObject;

        // Nested mage prefab so scale is easy to tweak alone
        Transform core = root.transform.Find("Core");
        Transform mage = core != null ? core.Find("MageHeart") : null;
        if (mage != null)
        {
            // Re-fit using mesh assets (more reliable in editor than world bounds)
            var visual = mage.Find("SorcererVisual");
            if (visual != null)
                CastleMageVisual.FitHeight(visual.gameObject, 3.2f);

            GameObject magePrefab = PrefabUtility.SaveAsPrefabAsset(mage.gameObject, MagePrefabPath);
            Object.DestroyImmediate(mage.gameObject);

            GameObject mageInstance = (GameObject)PrefabUtility.InstantiatePrefab(magePrefab, core);
            mageInstance.name = "MageHeart";
            mageInstance.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            mageInstance.transform.localRotation = Quaternion.identity;
            mageInstance.transform.localScale = Vector3.one;
        }

        frame.RefreshSocketsFromChildren();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null)
        {
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }

        Debug.Log($"Saved editable castle prefab:\n  {PrefabPath}\n  {MagePrefabPath}\nOpen it, tweak Hull / Core / MageHeart scale, then Stage 2 will spawn this prefab at runtime.");
    }

    [MenuItem("Catapulto/Castle/Open PlayerCastle_T1 Prefab")]
    public static void OpenPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Missing prefab", "Run Catapulto/Castle/Generate PlayerCastle_T1 Prefab first.", "OK");
            return;
        }

        AssetDatabase.OpenAsset(prefab);
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        string[] parts = assetFolder.Split('/');
        string path = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = path + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(path, parts[i]);
            path = next;
        }
    }
}
#endif
