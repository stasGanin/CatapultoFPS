#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cylinder stone node for pickaxe farming.
/// Menu: Catapulto / Gathering / Generate Stone Outcrop Prefab
/// </summary>
public static class GatheringPrefabGenerator
{
    public const string PrefabFolder = "Assets/Resources/Gathering";
    public const string PrefabPath = PrefabFolder + "/StoneOutcrop.prefab";
    public const string TreePrefabPath = PrefabFolder + "/HarvestableTree.prefab";
    const string StonePath = "Assets/Resources/Items/StoneItem.asset";
    const string TreeArtPath = "Assets/Environment/Trees/tree2_low.prefab";

    [InitializeOnLoadMethod]
    static void AutoCreateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                Generate(ping: false);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath) == null)
                GenerateTree(ping: false);
        };
    }

    [MenuItem("Catapulto/Gathering/Generate Stone Outcrop Prefab")]
    public static void GenerateFromMenu() => Generate(ping: true);

    [MenuItem("Catapulto/Gathering/Generate Harvestable Tree Prefab")]
    public static void GenerateTreeFromMenu() => GenerateTree(ping: true);

    public static void Generate(bool ping)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Gathering");

        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StonePath);
        if (stone == null)
            Debug.LogWarning($"GatheringPrefabGenerator: missing {StonePath}");

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "StoneOutcrop";
        go.transform.localScale = new Vector3(1.35f, 0.72f, 1.35f);

        var col = go.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;

        Paint(go, new Color(0.52f, 0.5f, 0.46f));

        var mine = go.AddComponent<MineableDebris>();
        var so = new SerializedObject(mine);
        so.FindProperty("_maxHp").intValue = 15;
        so.FindProperty("_lootCount").intValue = 4;
        so.FindProperty("_lootItem").objectReferenceValue = stone;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (ping && saved != null)
        {
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
    }

    static void Paint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;
        string matPath = PrefabFolder + "/StoneOutcrop.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, matPath);
            mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        renderer.sharedMaterial = mat;
    }

    public static void GenerateTree(bool ping)
    {
        EnsureFolders();

        GameObject root = CreateTreeRoot();
        root.name = "HarvestableTree";
        EnsureTreeCollider(root);
        if (root.GetComponent<HarvestableTree>() == null)
            root.AddComponent<HarvestableTree>();

        PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
        if (ping && saved != null)
        {
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Gathering");
    }

    static GameObject CreateTreeRoot()
    {
        var art = AssetDatabase.LoadAssetAtPath<GameObject>(TreeArtPath);
        if (art != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(art);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }

        var root = new GameObject("HarvestableTree");
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        trunk.transform.localScale = new Vector3(0.35f, 1.1f, 0.35f);
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        PaintPrimitive(trunk, new Color(0.38f, 0.24f, 0.14f));

        var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Crown";
        crown.transform.SetParent(root.transform, false);
        crown.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        crown.transform.localScale = new Vector3(2.2f, 2.0f, 2.2f);
        Object.DestroyImmediate(crown.GetComponent<Collider>());
        PaintPrimitive(crown, new Color(0.22f, 0.42f, 0.18f));
        return root;
    }

    static void EnsureTreeCollider(GameObject root)
    {
        if (root.GetComponent<Collider>() != null)
            return;

        var rend = root.GetComponentInChildren<Renderer>();
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        if (rend == null)
        {
            capsule.height = 4f;
            capsule.radius = 0.45f;
            capsule.center = new Vector3(0f, 2f, 0f);
            return;
        }

        Bounds b = rend.bounds;
        capsule.height = Mathf.Max(2.4f, b.size.y);
        capsule.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.22f, 0.28f, 1.2f);
        capsule.center = root.transform.InverseTransformPoint(b.center);
    }

    static void PaintPrimitive(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        renderer.sharedMaterial = mat;
    }
}
#endif
