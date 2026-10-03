using UnityEngine;

/// <summary>Creates runtime building meshes when no prefab is assigned.</summary>
public static class BuildingPrefabFactory
{
    public static GameObject CreateRuntimePiece(BuildingDefinition def)
    {
        if (def == null)
            return CreateBox("Piece", new Vector3(2f, 0.3f, 2f), new Color(0.45f, 0.42f, 0.38f), null);

        switch (def.Kind)
        {
            case BuildingPieceKind.Wall:
                return CreateBox(def.DisplayName, def.Size, def.IconColor, def);
            case BuildingPieceKind.Roof:
                return CreateBox(def.DisplayName, def.Size, def.IconColor, def);
            case BuildingPieceKind.Stairs:
                return CreateStairs(def);
            case BuildingPieceKind.Foundation:
            default:
                return CreateBox(def.DisplayName, def.Size, def.IconColor, def);
        }
    }

    public static GameObject CreateRuntimeFoundation(BuildingDefinition def) => CreateRuntimePiece(def);

    static GameObject CreateBox(string name, Vector3 size, Color color, BuildingDefinition def)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.localScale = size;

        var col = go.GetComponent<BoxCollider>();
        if (col != null)
            col.size = Vector3.one;

        ApplyLitColor(go, color);

        var piece = go.AddComponent<BuildingPiece>();
        piece.Init(def);
        return go;
    }

    static GameObject CreateStairs(BuildingDefinition def)
    {
        Vector3 size = def.Size;
        var root = new GameObject(def.DisplayName);
        int steps = 4;
        float stepW = size.x;
        float stepH = size.y / steps;
        float stepD = size.z / steps;

        for (int i = 0; i < steps; i++)
        {
            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{i}";
            step.transform.SetParent(root.transform, false);
            float depth = size.z - i * stepD;
            step.transform.localScale = new Vector3(stepW, stepH, depth);
            // Sit on ground, each step higher and shorter toward +Z
            step.transform.localPosition = new Vector3(
                0f,
                stepH * 0.5f + i * stepH,
                -size.z * 0.5f + depth * 0.5f);
            ApplyLitColor(step, def.IconColor);
        }

        // Single collider for placement / standing
        var box = root.AddComponent<BoxCollider>();
        box.size = size;
        box.center = new Vector3(0f, size.y * 0.5f, 0f);

        var piece = root.AddComponent<BuildingPiece>();
        piece.Init(def);
        return root;
    }

    static void ApplyLitColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    public static GameObject CreateGhost(BuildingDefinition def)
    {
        GameObject source = def != null && def.Prefab != null
            ? Object.Instantiate(def.Prefab)
            : CreateRuntimePiece(def);

        source.name = "BuildGhost";
        foreach (var col in source.GetComponentsInChildren<Collider>())
            Object.Destroy(col);

        foreach (var rb in source.GetComponentsInChildren<Rigidbody>())
            Object.Destroy(rb);

        var piece = source.GetComponent<BuildingPiece>();
        if (piece != null)
            Object.Destroy(piece);

        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        foreach (var r in source.GetComponentsInChildren<Renderer>())
        {
            var mat = new Material(shader);
            mat.color = new Color(0.2f, 0.85f, 0.35f, 0.45f);
            r.sharedMaterial = mat;
        }

        return source;
    }
}
