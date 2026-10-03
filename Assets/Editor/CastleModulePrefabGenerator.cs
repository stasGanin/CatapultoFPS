#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds editable module prefabs from ExportedModels FBX.
/// Menu: Catapulto / Castle / Generate Module Prefabs
/// Existing prefabs are not overwritten unless Force Regenerated.
/// </summary>
public static class CastleModulePrefabGenerator
{
    const string SourceFolder = "Assets/Resources/Castle/Modules/Source/";
    const string PrefabFolder = "Assets/Resources/Castle/Modules";
    const float TargetHeight = 4f;
    const string StonePathA = "Assets/Resources/Items/StoneItem.asset";
    const string StonePathB = "Assets/Data/Items/StoneItem.asset";

    struct Spec
    {
        public string FbxName;
        public string PrefabName;
        public CastleModuleKind Kind;
        public bool AddSpawner;
        public bool AddGolemSocket;
    }

    static readonly Spec[] Specs =
    {
        new Spec { FbxName = "SimpleWallModuleBroken.fbx", PrefabName = "WallModule.prefab", Kind = CastleModuleKind.Wall },
        new Spec { FbxName = "SimpleDoorModuleBroken.fbx", PrefabName = "DoorModule.prefab", Kind = CastleModuleKind.Door },
        new Spec
        {
            FbxName = "SimpleSpawnerModuleBroken.fbx",
            PrefabName = "SpawnerModule.prefab",
            Kind = CastleModuleKind.Spawner,
            AddSpawner = true,
            AddGolemSocket = true
        },
        new Spec { FbxName = "SimpleTowerModuleBroken.fbx", PrefabName = "TowerModule.prefab", Kind = CastleModuleKind.Tower },
    };

    [InitializeOnLoadMethod]
    static void AutoCreateIfMissing()
    {
        EditorApplication.delayCall += GenerateMissing;
    }

    [MenuItem("Catapulto/Castle/Generate Module Prefabs")]
    public static void GenerateFromMenu() => Generate(ping: true, overwrite: false);

    [MenuItem("Catapulto/Castle/Force Regenerate Module Prefabs")]
    public static void ForceFromMenu() => Generate(ping: true, overwrite: true);

    public static void GenerateMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Generate(ping: false, overwrite: false);
    }

    public static void Generate(bool ping, bool overwrite)
    {
        EnsureFolders();

        GameObject last = null;
        int made = 0;
        for (int i = 0; i < Specs.Length; i++)
        {
            string prefabPath = PrefabFolder + "/" + Specs[i].PrefabName;
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                continue;

            var saved = BuildOne(Specs[i]);
            if (saved != null)
            {
                last = saved;
                made++;
            }
        }

        if (made == 0)
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (ping && last != null)
        {
            Selection.activeObject = last;
            EditorGUIUtility.PingObject(last);
        }

        Debug.Log("Castle module prefabs generated (" + made + ") in " + PrefabFolder);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Castle/Modules"))
            AssetDatabase.CreateFolder("Assets/Resources/Castle", "Modules");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Castle/Modules/Source"))
            AssetDatabase.CreateFolder("Assets/Resources/Castle/Modules", "Source");
    }

    static GameObject BuildOne(Spec spec)
    {
        string fbxPath = SourceFolder + spec.FbxName;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null)
        {
            Debug.LogError("CastleModulePrefabGenerator: missing " + fbxPath);
            return null;
        }

        var root = new GameObject(spec.PrefabName.Replace(".prefab", ""));
        GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
        art.name = "Art";
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;
        art.transform.localScale = Vector3.one;
        PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        Strip(art);
        OrientLongAxisAlongX(root, art.transform);
        RecenterBottom(root);
        FitHeight(root, TargetHeight);
        BakeScaleIntoChildren(root);

        var module = root.AddComponent<CastleModuleRoot>();
        module.SetKind(spec.Kind);
        module.PrepareAuthoredPieces();
        AssignDefaultLoot(root);

        Bounds b = Encapsulate(root);
        Vector3 size = b.size;
        module.SetFootprint(Mathf.Max(0.8f, size.x), Mathf.Max(0.3f, size.z), Mathf.Max(0.8f, size.y));

        if (spec.AddGolemSocket)
        {
            var socket = new GameObject("GolemSocket");
            socket.transform.SetParent(root.transform, false);
            Vector3 localCenter = root.transform.InverseTransformPoint(b.center);
            socket.transform.localPosition = localCenter + Vector3.forward * (size.z * 0.08f) + Vector3.up * (size.y * 0.05f);
            module.SetGolemSocket(socket.transform);
        }

        if (spec.AddSpawner)
            root.AddComponent<EnemyCastleSpawnModule>();

        string prefabPath = PrefabFolder + "/" + spec.PrefabName;
        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static void AssignDefaultLoot(GameObject root)
    {
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StonePathA)
                    ?? AssetDatabase.LoadAssetAtPath<ItemDefinition>(StonePathB);
        if (stone == null)
            return;

        var chunks = root.GetComponentsInChildren<CastleWallChunk>(true);
        for (int i = 0; i < chunks.Length; i++)
            chunks[i].SetLoot(stone, 1);
    }

    static void Strip(GameObject art)
    {
        foreach (var a in art.GetComponentsInChildren<Animator>(true))
            Object.DestroyImmediate(a);
        foreach (var rb in art.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);
        foreach (var col in art.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);
        foreach (var cam in art.GetComponentsInChildren<Camera>(true))
            Object.DestroyImmediate(cam.gameObject);
        foreach (var light in art.GetComponentsInChildren<Light>(true))
            Object.DestroyImmediate(light);
    }

    static void OrientLongAxisAlongX(GameObject root, Transform art)
    {
        Bounds b = Encapsulate(root);
        if (b.size.z <= b.size.x * 1.05f)
            return;

        art.localRotation = Quaternion.Euler(0f, 90f, 0f) * art.localRotation;
    }

    static void RecenterBottom(GameObject root)
    {
        Bounds b = Encapsulate(root);
        if (b.size.sqrMagnitude < 0.0001f)
            return;

        Vector3 worldPivot = new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 localPivot = root.transform.InverseTransformPoint(worldPivot);
        for (int i = 0; i < root.transform.childCount; i++)
            root.transform.GetChild(i).localPosition -= localPivot;
    }

    static void FitHeight(GameObject root, float targetHeight)
    {
        Bounds b = Encapsulate(root);
        if (b.size.y < 0.01f)
            return;

        float s = targetHeight / b.size.y;
        root.transform.localScale = root.transform.localScale * s;
    }

    static void BakeScaleIntoChildren(GameObject root)
    {
        Vector3 s = root.transform.localScale;
        if ((s - Vector3.one).sqrMagnitude < 0.000001f)
            return;

        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform child = root.transform.GetChild(i);
            child.localPosition = Vector3.Scale(child.localPosition, s);
            child.localScale = Vector3.Scale(child.localScale, s);
        }

        root.transform.localScale = Vector3.one;
    }

    static Bounds Encapsulate(GameObject root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        return b;
    }
}
#endif
