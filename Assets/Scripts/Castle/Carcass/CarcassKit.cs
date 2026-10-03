using System;
using UnityEngine;

/// <summary>
/// Loads carcass FBX pieces once and clones them with meter-scale pivots.
/// </summary>
public static class CarcassKit
{
    const string CarcassPath = "Castle/Carcass/Source/Carcass";
    const string WallPath = "Castle/Carcass/Source/Wall";
    const string DoorPath = "Castle/Carcass/Source/WallDoor";
    const string WindowPath = "Castle/Carcass/Source/WallWindows";
    const string MerlonPath = "Castle/Carcass/Source/Merlon";
    const string WallBrokenPath = "Castle/Carcass/Source/WallDestroyed";
    const string DoorBrokenPath = "Castle/Carcass/Source/WallDoorDestroyed";
    const string WindowBrokenPath = "Castle/Carcass/Source/WallWindowsDestroyed";

    static bool _ready;
    static GameObject _corner;
    static GameObject _floor;
    static GameObject _roof;
    static GameObject _wall;
    static GameObject _window;
    static GameObject _door;
    static GameObject _merlon;
    static GameObject _wallBroken;
    static GameObject _windowBroken;
    static GameObject _doorBroken;
    static Transform _hidden;
    // Масштаб кита считаем по эталону — высоте колонны каркаса, а не угадываем по размеру:
    // тридешник экспортирует то в см с корнем 0.01, то с group1 = 0.33. Эталон переживает оба варианта.
    static float _kitScale = 1f;

    public static GameObject CreateCorner(Transform parent, Vector3 localPos)
    {
        EnsureLoaded();
        return Spawn(_corner, parent, localPos, Quaternion.identity, "Column");
    }

    public static GameObject CreateFloor(Transform parent, Vector3 localPos)
    {
        EnsureLoaded();
        return Spawn(_floor, parent, localPos, Quaternion.identity, "Floor");
    }

    public static GameObject CreateRoof(Transform parent, Vector3 localPos)
    {
        EnsureLoaded();
        return Spawn(_roof, parent, localPos, Quaternion.identity, "Roof");
    }

    public static GameObject CreateMerlon(Transform parent, Vector3 localPos, Quaternion localRot)
    {
        EnsureLoaded();
        if (_merlon == null)
            return null;
        return Spawn(_merlon, parent, localPos, localRot, "Merlon");
    }

    public static GameObject CreateWallModule(CastleModuleKind kind, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        EnsureLoaded();
        GameObject template = kind == CastleModuleKind.Door
            ? _door
            : kind == CastleModuleKind.Window
                ? _window
                : _wall;
        string name = kind == CastleModuleKind.Door
            ? "WallDoor"
            : kind == CastleModuleKind.Window
                ? "WallWindow"
                : "WallSolid";
        GameObject go = Spawn(template, parent, localPos, localRot, name);
        var root = go.AddComponent<CastleModuleRoot>();
        root.SetKind(kind);
        root.SetFootprint(CarcassMetrics.WallAlong, CarcassMetrics.WallThickness, CarcassMetrics.WallHeight);
        if (kind == CastleModuleKind.Door)
            WireDoor(go);
        go.AddComponent<CarcassWallBreakable>();
        return go;
    }

    /// <summary>
    /// Pre-fractured version of a wall module, aligned to the intact one (same bottom-center pivot).
    /// Null when the art is missing — the caller then breaks the intact mesh as a single piece.
    /// </summary>
    public static GameObject CreateBrokenArt(CastleModuleKind kind, Transform parent)
    {
        EnsureLoaded();
        GameObject template = kind == CastleModuleKind.Door
            ? _doorBroken
            : kind == CastleModuleKind.Window
                ? _windowBroken
                : _wallBroken;
        if (template == null)
            return null;

        // Без EnsureCollider: коллайдеры кускам выдаёт CastleWallChunk.
        GameObject go = UnityEngine.Object.Instantiate(template);
        go.name = "BrokenArt";
        go.SetActive(true);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    public static GameObject CreateSectionGhost()
    {
        EnsureLoaded();
        var ghost = new GameObject("Ghost_Section");
        CreateCorner(ghost.transform, CarcassMetrics.ColumnLocal(0, 0, 0));
        CreateCorner(ghost.transform, CarcassMetrics.ColumnLocal(1, 0, 0));
        CreateCorner(ghost.transform, CarcassMetrics.ColumnLocal(0, 0, 1));
        CreateCorner(ghost.transform, CarcassMetrics.ColumnLocal(1, 0, 1));
        CreateFloor(ghost.transform, CarcassMetrics.FloorLocal(0, 0, 0));
        CreateRoof(ghost.transform, CarcassMetrics.RoofLocal(0, 0, 0));
        OffsetChildren(ghost.transform, -CarcassMetrics.CellCenterLocal(0, 0, 0));
        StripGameplay(ghost);
        return ghost;
    }

    public static GameObject CreateWallGhost(CastleModuleKind kind)
    {
        EnsureLoaded();
        GameObject go = CreateWallModule(kind, null, Vector3.zero, Quaternion.identity);
        go.name = "Ghost_Wall";
        StripGameplay(go);
        return go;
    }

    static GameObject Spawn(GameObject template, Transform parent, Vector3 localPos, Quaternion localRot, string name)
    {
        GameObject go = UnityEngine.Object.Instantiate(template);
        go.name = name;
        go.SetActive(true);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one;
        EnsureCollider(go, name);
        return go;
    }

    static void EnsureCollider(GameObject go, string name)
    {
        if (go.GetComponentInChildren<Collider>() != null)
            return;

        if (name == "WallDoor")
        {
            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null)
                    continue;
                var col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
                col.convex = false;
            }

            return;
        }

        Bounds b = EncapsulateLocal(go);
        var box = go.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = Vector3.Max(b.size, Vector3.one * 0.05f);
    }

    static void WireDoor(GameObject module)
    {
        var door = module.GetComponent<InteractableDoor>();
        if (door == null)
            door = module.AddComponent<InteractableDoor>();
        Transform leaf = FindDoorLeaf(module.transform);
        if (leaf != null)
            door.BindLeaf(leaf);
    }

    static Transform FindDoorLeaf(Transform root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        Transform best = null;
        int bestVerts = 0;
        for (int i = 0; i < filters.Length; i++)
        {
            var mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;
            string n = mf.gameObject.name;
            if (n.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Door", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            int v = mf.sharedMesh.vertexCount;
            if (v > bestVerts)
            {
                bestVerts = v;
                best = mf.transform;
            }
        }

        return best;
    }

    static void EnsureLoaded()
    {
        if (_ready)
            return;
        _ready = true;

        _hidden = new GameObject("CarcassKitTemplates").transform;
        _hidden.gameObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_hidden.gameObject);
        _kitScale = MeasureKitScale();

        _corner = CaptureNamed(CarcassPath, "Column", "Corner") ?? FallbackBox("Column", new Vector3(1f, 3.36f, 1f));
        _floor = CaptureNamed(CarcassPath, "Floor", "Floor") ?? FallbackBox("Floor", new Vector3(14f, 0.37f, 14f));
        _roof = CaptureNamed(CarcassPath, "Roof", "Roof") ?? FallbackBox("Roof", new Vector3(14f, 0.37f, 14f));
        _wall = CaptureWall(WallPath, "WallSolid") ?? FallbackBox("WallSolid", new Vector3(1f, 3.36f, 6f));
        _window = CaptureWall(WindowPath, "WallWindow") ?? FallbackBox("WallWindow", new Vector3(1f, 3.36f, 6f));
        _door = CaptureWall(DoorPath, "WallDoor") ?? FallbackDoor();
        _merlon = CaptureNamed(MerlonPath, "Merlon", "T2", "T1", "CornerT")
                  ?? FallbackBox("Merlon", new Vector3(1f, 0.64f, 1.5f));
        _wallBroken = PrepareBroken(WallBrokenPath, "WallSolidBroken");
        _windowBroken = PrepareBroken(WindowBrokenPath, "WallWindowBroken");
        _doorBroken = PrepareBroken(DoorBrokenPath, "WallDoorBroken");

        _corner = RecenterBottom(_corner);
        _floor = RecenterBottom(_floor);
        _roof = RecenterBottom(_roof);
        _wall = RecenterBottom(_wall);
        _window = RecenterBottom(_window);
        _door = RecenterBottom(_door);
        _merlon = RecenterBottom(_merlon);
        _wall = OrientAlongZ(_wall);
        _window = OrientAlongZ(_window);
        _door = OrientAlongZ(_door);
        _merlon = OrientAlongZ(_merlon);

        ParentHidden(_corner);
        ParentHidden(_floor);
        ParentHidden(_roof);
        ParentHidden(_wall);
        ParentHidden(_window);
        ParentHidden(_door);
        ParentHidden(_merlon);
    }

    /// <summary>Тот же пайплайн, что и у целых стен, чтобы куски совпали с целой моделью.</summary>
    static GameObject PrepareBroken(string resourcePath, string cloneName)
    {
        GameObject go = CaptureWall(resourcePath, cloneName);
        if (go == null)
            return null;
        go = OrientAlongZ(RecenterBottom(go));
        ParentHidden(go);
        return go;
    }

    static GameObject CaptureNamed(string resourcePath, string cloneName, params string[] needles)
    {
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
            return null;

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        StripImportJunk(instance);
        ApplyMeterScale(instance);
        MeshFilter src = FindMesh(instance, needles);
        GameObject clone = src != null ? CaptureMesh(src, cloneName) : null;
        UnityEngine.Object.Destroy(instance);
        return clone;
    }

    /// <summary>
    /// Модуль стены подгоняется по высоте стены, а не общим множителем кита: файлы стен приходят
    /// в разных масштабах (старая дверь — 1/100, новые стены — 1/3, внутри разрушенных — 33.33 у детей).
    /// </summary>
    static GameObject CaptureWall(string resourcePath, string cloneName)
    {
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
            return null;

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = cloneName;
        StripImportJunk(instance);
        FitHeight(instance, CarcassMetrics.WallHeight);
        return instance;
    }

    static void FitHeight(GameObject root, float targetHeight)
    {
        float height = EncapsulateWorld(root).size.y;
        if (height < 1e-4f)
        {
            Debug.LogError($"CarcassKit: '{root.name}' has no renderable height — scale left as imported.");
            return;
        }

        root.transform.localScale *= targetHeight / height;
    }

    static void ApplyMeterScale(GameObject root)
    {
        root.transform.localScale *= _kitScale;
    }

    /// <summary>Множитель, при котором колонна из Carcass.fbx получается высотой ColumnHeight.</summary>
    static float MeasureKitScale()
    {
        var prefab = Resources.Load<GameObject>(CarcassPath);
        if (prefab == null)
            return 1f;

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        MeshFilter column = FindMesh(instance, new[] { "Column", "Corner" });
        var renderer = column != null ? column.GetComponent<Renderer>() : null;
        float height = renderer != null ? renderer.bounds.size.y : 0f;
        UnityEngine.Object.Destroy(instance);

        if (height < 1e-4f)
        {
            Debug.LogError("CarcassKit: cannot measure column height in Carcass.fbx — kit scale left at 1.");
            return 1f;
        }

        return CarcassMetrics.ColumnHeight / height;
    }

    static MeshFilter FindMesh(GameObject root, string[] needles)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int n = 0; n < needles.Length; n++)
        {
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] == null)
                    continue;
                if (filters[i].gameObject.name.IndexOf(needles[n], StringComparison.OrdinalIgnoreCase) >= 0)
                    return filters[i];
            }
        }

        return filters.Length > 0 ? filters[0] : null;
    }

    static GameObject CaptureMesh(MeshFilter src, string name)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = src.transform.lossyScale;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = src.sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        var srcRend = src.GetComponent<MeshRenderer>();
        if (srcRend != null)
            mr.sharedMaterials = srcRend.sharedMaterials;
        return go;
    }

    static GameObject RecenterBottom(GameObject go)
    {
        if (go == null)
            return null;
        if (!TryLocalMeshBounds(go, out Bounds local))
            return go;

        Vector3 localPivot = new Vector3(local.center.x, local.min.y, local.center.z);
        Vector3 worldPivot = go.transform.TransformPoint(localPivot);
        var wrap = new GameObject(go.name);
        wrap.transform.SetPositionAndRotation(worldPivot, go.transform.rotation);
        go.transform.SetParent(wrap.transform, true);
        go.name = "Art";
        wrap.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return wrap;
    }

    static bool TryLocalMeshBounds(GameObject root, out Bounds local)
    {
        local = new Bounds();
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        bool any = false;
        for (int i = 0; i < filters.Length; i++)
        {
            var mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;

            Bounds mb = mf.sharedMesh.bounds;
            Vector3 min = mb.min;
            Vector3 max = mb.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 world = mf.transform.TransformPoint(corner);
                        Vector3 loc = root.transform.InverseTransformPoint(world);
                        if (!any)
                        {
                            local = new Bounds(loc, Vector3.zero);
                            any = true;
                        }
                        else
                        {
                            local.Encapsulate(loc);
                        }
                    }
                }
            }
        }

        return any;
    }

    static GameObject OrientAlongZ(GameObject go)
    {
        if (go == null)
            return null;
        Bounds b = EncapsulateWorld(go);
        if (b.size.x > b.size.z * 1.15f)
            go.transform.Rotate(0f, 90f, 0f, Space.World);
        return RecenterBottom(go);
    }

    static void ParentHidden(GameObject go)
    {
        if (go == null)
            return;
        go.transform.SetParent(_hidden, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.SetActive(false);
    }

    static GameObject FallbackBox(string name, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.transform.localScale = size;
        return go;
    }

    static GameObject FallbackDoor()
    {
        var root = FallbackBox("WallDoor", new Vector3(1f, 3.36f, 6f));
        var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "Door";
        leaf.transform.SetParent(root.transform, false);
        leaf.transform.localPosition = new Vector3(-0.35f, 1.1f, 0f);
        leaf.transform.localScale = new Vector3(0.12f, 2.1f, 1.2f);
        return root;
    }

    static void OffsetChildren(Transform root, Vector3 localDelta)
    {
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).localPosition += localDelta;
    }

    static void StripGameplay(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null)
                mb.enabled = false;
        }

        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            UnityEngine.Object.Destroy(rb);
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.Destroy(col);
    }

    static void StripImportJunk(GameObject root)
    {
        foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            UnityEngine.Object.Destroy(cam.gameObject);
        foreach (var light in root.GetComponentsInChildren<Light>(true))
            UnityEngine.Object.Destroy(light);
        foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            UnityEngine.Object.Destroy(anim);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            UnityEngine.Object.Destroy(rb);
    }

    static Bounds EncapsulateWorld(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        return b;
    }

    static Bounds EncapsulateLocal(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(Vector3.up * 1.68f, new Vector3(1f, 3.36f, 6f));

        Bounds b = new Bounds(go.transform.InverseTransformPoint(rends[0].bounds.center), Vector3.zero);
        for (int i = 0; i < rends.Length; i++)
        {
            Bounds wb = rends[i].bounds;
            Vector3 min = go.transform.InverseTransformPoint(wb.min);
            Vector3 max = go.transform.InverseTransformPoint(wb.max);
            b.Encapsulate(min);
            b.Encapsulate(max);
        }

        return b;
    }
}
