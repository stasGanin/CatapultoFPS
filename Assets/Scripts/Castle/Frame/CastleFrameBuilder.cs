using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds T1 triangle castle hierarchy (Hull / Sockets / Core).
/// Preferred workflow: bake once to PlayerCastle_T1 prefab via Catapulto/Castle menu.
/// </summary>
public static class CastleFrameBuilder
{
    public const float PadFitRadius = 12f;
    public const float WallHeight = 3.8f;
    public const float WallThickness = 1.2f;

    public static float SideForRadius(float radius) => radius * Mathf.Sqrt(3f);

    public static CastleFrame BuildTier1(Vector3 center, float yawDegrees = 0f, float fitRadius = PadFitRadius)
    {
        float side = SideForRadius(fitRadius);
        var root = new GameObject("PlayerCastle_T1");
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        var frame = root.AddComponent<CastleFrame>();
        var sockets = new List<CastleSocket>(9);

        Transform hull = CreateChild(root.transform, "Hull");
        Transform socketsRoot = CreateChild(root.transform, "Sockets");
        Transform core = CreateChild(root.transform, "Core");

        CreatePadCircle(hull, fitRadius);

        float h = side * Mathf.Sqrt(3f) * 0.5f;
        Vector3 c0 = new Vector3(0f, 0f, h * 2f / 3f);
        Vector3 c1 = new Vector3(-side * 0.5f, 0f, -h / 3f);
        Vector3 c2 = new Vector3(side * 0.5f, 0f, -h / 3f);

        sockets.Add(CreateCornerSocket(hull, socketsRoot, c0, 0));
        sockets.Add(CreateCornerSocket(hull, socketsRoot, c1, 1));
        sockets.Add(CreateCornerSocket(hull, socketsRoot, c2, 2));

        int wallIdx = 0;
        wallIdx = AddEdgeWithSegments(hull, socketsRoot, c0, c1, wallIdx, sockets);
        wallIdx = AddEdgeWithSegments(hull, socketsRoot, c1, c2, wallIdx, sockets);
        AddEdgeWithSegments(hull, socketsRoot, c2, c0, wallIdx, sockets);

        CreateMagePedestal(core);
        frame.Bind(1, sockets);
        return frame;
    }

    static Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static int AddEdgeWithSegments(
        Transform hull, Transform socketsRoot, Vector3 a, Vector3 b, int startIndex, List<CastleSocket> into)
    {
        Vector3 mid = (a + b) * 0.5f;
        AddSegmentAndSocket(hull, socketsRoot, a, mid, startIndex, into);
        AddSegmentAndSocket(hull, socketsRoot, mid, b, startIndex + 1, into);
        return startIndex + 2;
    }

    static void AddSegmentAndSocket(
        Transform hull, Transform socketsRoot, Vector3 a, Vector3 b, int index, List<CastleSocket> into)
    {
        Vector3 mid = (a + b) * 0.5f;
        Vector3 delta = b - a;
        float length = Mathf.Max(0.5f, delta.magnitude);
        float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

        var segGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segGo.name = $"WallSegment_{index}";
        segGo.transform.SetParent(hull, false);
        segGo.transform.localPosition = mid + Vector3.up * (WallHeight * 0.5f);
        segGo.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        segGo.transform.localScale = new Vector3(WallThickness, WallHeight, length);
        ApplyColor(segGo, new Color(0.42f, 0.4f, 0.38f));

        var segment = segGo.AddComponent<CastleWallSegment>();

        Vector3 outward = mid;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.001f)
            outward.Normalize();
        else
            outward = Vector3.forward;

        Vector3 socketLocal = mid + outward * (WallThickness * 0.55f);
        into.Add(CreateWallSocket(socketsRoot, socketLocal, index, segment, yaw));
    }

    static CastleSocket CreateCornerSocket(Transform hull, Transform socketsRoot, Vector3 localPos, int index)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = $"CornerPillar_{index}";
        pillar.transform.SetParent(hull, false);
        pillar.transform.localPosition = localPos + Vector3.up * (WallHeight * 0.5f);
        pillar.transform.localScale = new Vector3(1.6f, WallHeight * 0.5f, 1.6f);
        ApplyColor(pillar, new Color(0.38f, 0.36f, 0.34f));

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Socket_Corner_{index}";
        go.transform.SetParent(socketsRoot, false);
        go.transform.localPosition = localPos + Vector3.up * 0.25f;
        go.transform.localScale = new Vector3(2.1f, 0.25f, 2.1f);
        DestroyCompat(go.GetComponent<Collider>());
        ApplyColor(go, new Color(0.25f, 0.65f, 0.95f));

        AddGhost(go.transform, new Color(0.25f, 0.65f, 0.95f));

        var socket = go.AddComponent<CastleSocket>();
        socket.Setup(CastleSocketKind.Corner, index, null);
        return socket;
    }

    static CastleSocket CreateWallSocket(
        Transform socketsRoot, Vector3 localPos, int index, CastleWallSegment segment, float yaw)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Socket_Wall_{index}";
        go.transform.SetParent(socketsRoot, false);
        go.transform.localPosition = localPos + Vector3.up * 0.22f;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = new Vector3(1.8f, 0.22f, 1.8f);
        DestroyCompat(go.GetComponent<Collider>());
        ApplyColor(go, new Color(0.95f, 0.8f, 0.25f));

        AddGhost(go.transform, new Color(0.95f, 0.8f, 0.25f));

        var socket = go.AddComponent<CastleSocket>();
        socket.Setup(CastleSocketKind.Wall, index, segment);
        return socket;
    }

    static void AddGhost(Transform socket, Color color)
    {
        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "Ghost";
        ghost.transform.SetParent(socket, false);
        ghost.transform.localPosition = new Vector3(0f, 4f, 0f);
        ghost.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
        DestroyCompat(ghost.GetComponent<Collider>());
        ApplyColor(ghost, Color.Lerp(color, Color.white, 0.35f));
    }

    static void CreatePadCircle(Transform parent, float radius)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "PadCircle";
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
        DestroyCompat(ring.GetComponent<Collider>());
        ApplyColor(ring, new Color(0.28f, 0.4f, 0.32f, 1f));
    }

    static void CreateMagePedestal(Transform core)
    {
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "MagePedestal";
        pedestal.transform.SetParent(core, false);
        pedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        pedestal.transform.localScale = new Vector3(2.6f, 0.35f, 2.6f);
        ApplyColor(pedestal, new Color(0.35f, 0.28f, 0.45f));

        CastleMageVisual.Spawn(core, new Vector3(0f, 0.75f, 0f), heightScale: 3.2f, attachMageComponent: false);
    }

    public static GameObject CreateDoorGhost()
    {
        var root = new GameObject("Ghost_Door");
        float height = WallHeight;
        float length = 6f;
        float thickness = WallThickness;
        float opening = 2.2f;
        float postWidth = (length - opening) * 0.5f;

        CreateDoorPost(root.transform, new Vector3(0f, 0f, -(opening * 0.5f + postWidth * 0.5f)),
            thickness, height, postWidth);
        CreateDoorPost(root.transform, new Vector3(0f, 0f, opening * 0.5f + postWidth * 0.5f),
            thickness, height, postWidth);

        var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.name = "Lintel";
        lintel.transform.SetParent(root.transform, false);
        lintel.transform.localPosition = new Vector3(0f, height * 0.5f - 0.25f, 0f);
        lintel.transform.localScale = new Vector3(thickness * 1.05f, 0.45f, length);
        DestroyCompat(lintel.GetComponent<Collider>());

        foreach (var col in root.GetComponentsInChildren<Collider>())
            DestroyCompat(col);

        var ghostMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        ghostMat.color = new Color(0.25f, 0.9f, 0.4f, 0.45f);
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = ghostMat;

        return root;
    }

    public static GameObject CreateDoorModule(CastleWallSegment segment, Transform socket)
    {
        var root = new GameObject("Module_Door");
        Vector3 pos = segment != null ? segment.transform.position : socket.position;
        Quaternion rot = segment != null ? segment.transform.rotation : socket.rotation;
        Vector3 scale = segment != null ? segment.transform.lossyScale : new Vector3(1.2f, 3.8f, 6f);

        root.transform.SetPositionAndRotation(pos, rot);

        float height = scale.y;
        float length = scale.z;
        float thickness = scale.x;
        float opening = Mathf.Min(2.4f, length * 0.45f);
        float postWidth = Mathf.Max(0.35f, (length - opening) * 0.5f);

        CreateDoorPost(root.transform, new Vector3(0f, 0f, -(opening * 0.5f + postWidth * 0.5f)),
            thickness, height, postWidth);
        CreateDoorPost(root.transform, new Vector3(0f, 0f, opening * 0.5f + postWidth * 0.5f),
            thickness, height, postWidth);

        var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.name = "Lintel";
        lintel.transform.SetParent(root.transform, false);
        lintel.transform.localPosition = new Vector3(0f, height * 0.5f - 0.25f, 0f);
        lintel.transform.localScale = new Vector3(thickness * 1.05f, 0.45f, length);
        ApplyColor(lintel, new Color(0.48f, 0.36f, 0.22f));

        return root;
    }

    public static GameObject CreateStorageGhost()
    {
        var root = new GameObject("Ghost_Storage");
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Chest";
        box.transform.SetParent(root.transform, false);
        box.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        box.transform.localScale = new Vector3(1.6f, 1.1f, 1.1f);
        Object.Destroy(box.GetComponent<Collider>());

        foreach (var col in root.GetComponentsInChildren<Collider>())
            DestroyCompat(col);

        var ghostMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        ghostMat.color = new Color(0.25f, 0.9f, 0.4f, 0.45f);
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = ghostMat;

        return root;
    }

    public static GameObject CreateStorageModule(CastleWallSegment segment, Transform socket)
    {
        var root = new GameObject("Module_Storage");
        Vector3 pos = segment != null ? segment.transform.position : socket.position;
        Quaternion rot = segment != null ? segment.transform.rotation : socket.rotation;
        root.transform.SetPositionAndRotation(pos, rot);

        float height = segment != null ? segment.transform.lossyScale.y : WallHeight;
        float length = segment != null ? segment.transform.lossyScale.z : 6f;
        float thickness = segment != null ? segment.transform.lossyScale.x : WallThickness;
        float opening = Mathf.Min(2.4f, length * 0.45f);
        float postWidth = Mathf.Max(0.35f, (length - opening) * 0.5f);

        CreateDoorPost(root.transform, new Vector3(0f, 0f, -(opening * 0.5f + postWidth * 0.5f)),
            thickness, height, postWidth);
        CreateDoorPost(root.transform, new Vector3(0f, 0f, opening * 0.5f + postWidth * 0.5f),
            thickness, height, postWidth);

        var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chest.name = "ChestVisual";
        chest.transform.SetParent(root.transform, false);
        chest.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        chest.transform.localScale = new Vector3(1.55f, 1.05f, 1.05f);
        ApplyColor(chest, new Color(0.52f, 0.38f, 0.22f));

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(chest.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        lid.transform.localScale = new Vector3(1.05f, 0.22f, 1.05f);
        DestroyCompat(lid.GetComponent<Collider>());
        ApplyColor(lid, new Color(0.45f, 0.32f, 0.18f));

        root.AddComponent<StorageContainer>();
        return root;
    }

    static void CreateDoorPost(Transform parent, Vector3 localPos, float thickness, float height, float width)
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "DoorPost";
        post.transform.SetParent(parent, false);
        post.transform.localPosition = localPos;
        post.transform.localScale = new Vector3(thickness * 1.05f, height, width);
        ApplyColor(post, new Color(0.48f, 0.36f, 0.22f));
    }

    /// <summary>Bake color into a unique material so it survives prefab save (PropertyBlocks do not).</summary>
    static void ApplyColor(GameObject go, Color color)
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

    static void DestroyCompat(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}
