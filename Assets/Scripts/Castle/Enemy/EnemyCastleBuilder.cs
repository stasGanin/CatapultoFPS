using UnityEngine;

/// <summary>Builds a test enemy triangular castle: door + spawn module + cone mage.</summary>
public static class EnemyCastleBuilder
{
    public static EnemyCastle BuildTest(Vector3 center, float yawDegrees = 0f, float fitRadius = CastleFrameBuilder.PadFitRadius)
    {
        float side = CastleFrameBuilder.SideForRadius(fitRadius);
        float wallH = CastleFrameBuilder.WallHeight;
        float wallT = CastleFrameBuilder.WallThickness;

        var root = new GameObject("EnemyCastle_T1");
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        var castle = root.AddComponent<EnemyCastle>();

        float h = side * Mathf.Sqrt(3f) * 0.5f;
        Vector3 c0 = new Vector3(0f, 0f, h * 2f / 3f);
        Vector3 c1 = new Vector3(-side * 0.5f, 0f, -h / 3f);
        Vector3 c2 = new Vector3(side * 0.5f, 0f, -h / 3f);

        CreatePad(root.transform, fitRadius);

        castle.RegisterPiece(CreatePillar(root.transform, c0, wallH));
        castle.RegisterPiece(CreatePillar(root.transform, c1, wallH));
        castle.RegisterPiece(CreatePillar(root.transform, c2, wallH));

        // Edge 0→1: door on first half
        var doorSeg = CreateSegment(root.transform, c0, Mid(c0, c1), wallH, wallT, "DoorSlot");
        var door = InstallDoor(root.transform, doorSeg);
        castle.RegisterPiece(door.transform);
        Object.Destroy(doorSeg);

        castle.RegisterPiece(CreateSegment(root.transform, Mid(c0, c1), c1, wallH, wallT, "Wall").transform);

        // Edge 1→2: spawn module on first half
        var spawnSeg = CreateSegment(root.transform, c1, Mid(c1, c2), wallH, wallT, "SpawnSlot");
        var spawner = InstallSpawner(root.transform, spawnSeg);
        castle.RegisterSpawner(spawner);
        castle.RegisterPiece(spawner.transform);
        Object.Destroy(spawnSeg);

        castle.RegisterPiece(CreateSegment(root.transform, Mid(c1, c2), c2, wallH, wallT, "Wall").transform);

        // Edge 2→0: solid
        castle.RegisterPiece(CreateSegment(root.transform, c2, Mid(c2, c0), wallH, wallT, "Wall").transform);
        castle.RegisterPiece(CreateSegment(root.transform, Mid(c2, c0), c0, wallH, wallT, "Wall").transform);

        var mage = CreateMageCone(root.transform, wallH);
        castle.BindMage(mage);
        mage.Bind(castle, 120f);

        return castle;
    }

    static Vector3 Mid(Vector3 a, Vector3 b) => (a + b) * 0.5f;

    static void CreatePad(Transform parent, float radius)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "EnemyPad";
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
        Object.Destroy(ring.GetComponent<Collider>());
        ApplyColor(ring, new Color(0.45f, 0.22f, 0.2f));
    }

    static Transform CreatePillar(Transform parent, Vector3 localPos, float wallH)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "CornerPillar";
        pillar.transform.SetParent(parent, false);
        pillar.transform.localPosition = localPos + Vector3.up * (wallH * 0.5f);
        pillar.transform.localScale = new Vector3(1.6f, wallH * 0.5f, 1.6f);
        ApplyColor(pillar, new Color(0.4f, 0.22f, 0.2f));
        return pillar.transform;
    }

    static GameObject CreateSegment(
        Transform parent, Vector3 a, Vector3 b, float wallH, float wallT, string name)
    {
        Vector3 mid = Mid(a, b);
        Vector3 delta = b - a;
        float length = Mathf.Max(0.5f, delta.magnitude);
        float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

        var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seg.name = name;
        seg.transform.SetParent(parent, false);
        seg.transform.localPosition = mid + Vector3.up * (wallH * 0.5f);
        seg.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        seg.transform.localScale = new Vector3(wallT, wallH, length);
        ApplyColor(seg, new Color(0.48f, 0.28f, 0.25f));
        return seg;
    }

    static GameObject InstallDoor(Transform parent, GameObject slotSegment)
    {
        var door = CastleFrameBuilder.CreateDoorModule(
            slotSegment.AddComponent<CastleWallSegment>(),
            slotSegment.transform);
        door.transform.SetParent(parent, true);
        door.transform.SetPositionAndRotation(slotSegment.transform.position, slotSegment.transform.rotation);
        foreach (var r in door.GetComponentsInChildren<Renderer>())
        {
            var block = new MaterialPropertyBlock();
            Color c = new Color(0.35f, 0.18f, 0.15f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
        return door;
    }

    static EnemyCastleSpawnModule InstallSpawner(Transform parent, GameObject slotSegment)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "EnemySpawnModule";
        go.transform.SetParent(parent, false);

        Vector3 local = parent.InverseTransformPoint(slotSegment.transform.position);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Inverse(parent.rotation) * slotSegment.transform.rotation;

        Vector3 lossy = slotSegment.transform.lossyScale;
        // Convert approx world scale into local under parent (parent scale is 1)
        go.transform.localScale = new Vector3(lossy.x * 1.35f, lossy.y * 0.85f, lossy.z * 0.85f);
        ApplyColor(go, new Color(0.55f, 0.35f, 0.15f));

        Vector3 outward = local;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.01f)
            go.transform.localRotation = Quaternion.LookRotation(outward.normalized, Vector3.up);

        return go.AddComponent<EnemyCastleSpawnModule>();
    }

    static EnemyCastleMage CreateMageCone(Transform parent, float wallH)
    {
        // Shared sorcerer FBX — damageable castle heart
        var go = CastleMageVisual.Spawn(parent, new Vector3(0f, 0.05f, 0f), heightScale: 2.8f, attachMageComponent: true);
        return go.GetComponent<EnemyCastleMage>();
    }

    static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
