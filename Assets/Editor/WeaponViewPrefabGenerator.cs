#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds an editable FPS view prefab: mesh + Muzzle empty (local +Z = shot).
/// Menu: Catapulto / Weapons / Generate Crossbow View Prefab
/// </summary>
public static class WeaponViewPrefabGenerator
{
    public const string FbxPath = "Assets/Resources/Weapons/Crossbow.fbx";
    public const string PrefabPath = "Assets/Resources/Weapons/CrossbowView.prefab";

    [InitializeOnLoadMethod]
    static void AutoCreateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) != null)
                Generate(ping: false);
        };
    }

    [MenuItem("Catapulto/Weapons/Generate Crossbow View Prefab")]
    public static void GenerateFromMenu() => Generate(ping: true);

    [MenuItem("Catapulto/Weapons/Open Crossbow View Prefab")]
    public static void OpenPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Missing prefab", "Run Catapulto/Weapons/Generate Crossbow View Prefab first.", "OK");
            return;
        }

        AssetDatabase.OpenAsset(prefab);
    }

    public static void Generate(bool ping)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null)
        {
            Debug.LogError($"WeaponViewPrefabGenerator: missing {FbxPath}");
            return;
        }

        var root = new GameObject("CrossbowView");
        root.transform.localPosition = new Vector3(0.28f, -0.22f, 0.45f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
        mesh.name = "Mesh";
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;
        mesh.transform.localScale = Vector3.one;
        StripColliders(mesh);
        RecenterAndFit(root.transform, mesh.transform, 0.5f);

        var muzzleGo = new GameObject("Muzzle");
        muzzleGo.transform.SetParent(root.transform, false);
        muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.28f);
        muzzleGo.transform.localRotation = Quaternion.identity;
        muzzleGo.AddComponent<WeaponMuzzle>();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (ping && saved != null)
        {
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            AssetDatabase.OpenAsset(saved);
        }

        Debug.Log(
            "CrossbowView prefab: rotate Mesh so the bow points local +Z (yellow gizmo). " +
            "Drag Muzzle to the bolt nock. Shots use Muzzle position + forward.");
    }

    static void StripColliders(GameObject go)
    {
        var colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Object.DestroyImmediate(colliders[i]);
    }

    static void RecenterAndFit(Transform root, Transform model, float targetLength)
    {
        Bounds bounds = CombinedBounds(root.gameObject);
        model.position += root.position - bounds.center;

        bounds = CombinedBounds(root.gameObject);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest < 1e-4f)
            return;

        model.localScale *= targetLength / longest;
        bounds = CombinedBounds(root.gameObject);
        model.position += root.position - bounds.center;
    }

    static Bounds CombinedBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
#endif
