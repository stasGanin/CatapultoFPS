using UnityEngine;

/// <summary>
/// Builds interior stations. Geometry is a placeholder made of cubes until the art arrives;
/// the pivot is the footprint center on the floor, so placement code never depends on art.
/// </summary>
public static class StationFactory
{
    static readonly Color Wood = new Color(0.45f, 0.3f, 0.18f);
    static readonly Color DarkWood = new Color(0.3f, 0.2f, 0.12f);
    static readonly Color Stone = new Color(0.42f, 0.4f, 0.38f);
    static readonly Color Ember = new Color(1f, 0.45f, 0.1f);
    static readonly Color Arcane = new Color(0.35f, 0.6f, 1f);
    static Material _shared;

    public static GameObject Create(StationDefinition def, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        GameObject root = BuildVisual(def, ghost: false);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = localRot;

        var box = root.AddComponent<BoxCollider>();
        Vector2Int fp = def.Footprint;
        box.center = new Vector3(0f, def.Height * 0.5f, 0f);
        box.size = new Vector3(fp.x - 0.1f, def.Height, fp.y - 0.1f);

        root.AddComponent<PlacedStation>().Bind(def);
        switch (def.Kind)
        {
            case StationKind.Workbench:
                root.AddComponent<CraftStationInteractable>().Configure(CraftStation.Workbench, def.DisplayName);
                break;
            case StationKind.Smelter:
                root.AddComponent<CraftStationInteractable>().Configure(CraftStation.Smelter, def.DisplayName);
                break;
            case StationKind.ResearchTable:
                root.AddComponent<ResearchTableInteractable>();
                break;
            case StationKind.Chest:
                root.AddComponent<StorageContainer>().Configure(def.DisplayName, 36, 2.8f);
                break;
        }

        return root;
    }

    /// <summary>Same silhouette without colliders or behaviours — for the placement preview.</summary>
    public static GameObject CreateGhost(StationDefinition def) => BuildVisual(def, ghost: true);

    static GameObject BuildVisual(StationDefinition def, bool ghost)
    {
        var root = new GameObject(ghost ? "Ghost_" + def.Id : def.Id);
        Vector2Int fp = def.Footprint;
        float w = fp.x - 0.15f, d = fp.y - 0.15f, h = def.Height;
        switch (def.Kind)
        {
            case StationKind.Workbench:
                Part(root, new Vector3(0f, h - 0.06f, 0f), new Vector3(w, 0.12f, d), Wood, ghost: ghost);
                foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Part(root, new Vector3(sx * (w * 0.5f - 0.08f), (h - 0.12f) * 0.5f, sz * (d * 0.5f - 0.08f)), new Vector3(0.1f, h - 0.12f, 0.1f), DarkWood, ghost: ghost);
                Part(root, new Vector3(w * 0.3f, h + 0.12f, 0f), new Vector3(0.25f, 0.24f, 0.3f), Stone, ghost: ghost);
                break;
            case StationKind.Smelter:
                Part(root, new Vector3(0f, h * 0.35f, 0f), new Vector3(w, h * 0.7f, d), Stone, ghost: ghost);
                Part(root, new Vector3(0f, h * 0.85f, -d * 0.2f), new Vector3(w * 0.35f, h * 0.3f, d * 0.35f), Stone, ghost: ghost);
                Part(root, new Vector3(0f, h * 0.3f, d * 0.5f), new Vector3(w * 0.45f, h * 0.3f, 0.04f), Ember, emissive: true, ghost: ghost);
                if (!ghost)
                    AddLight(root, new Vector3(0f, h * 0.3f, d * 0.5f + 0.3f), Ember);
                break;
            case StationKind.ResearchTable:
                Part(root, new Vector3(0f, h * 0.45f, 0f), new Vector3(w, h * 0.9f, d), DarkWood, ghost: ghost);
                Part(root, new Vector3(-w * 0.2f, h + 0.05f, 0f), new Vector3(0.45f, 0.1f, 0.35f), Wood, ghost: ghost);
                Part(root, new Vector3(w * 0.25f, h + 0.2f, 0f), new Vector3(0.25f, 0.25f, 0.25f), Arcane, emissive: true, ghost: ghost);
                break;
            case StationKind.Chest:
                Part(root, new Vector3(0f, h * 0.4f, 0f), new Vector3(w, h * 0.8f, d), Wood, ghost: ghost);
                Part(root, new Vector3(0f, h * 0.9f, 0f), new Vector3(w + 0.04f, h * 0.2f, d + 0.04f), DarkWood, ghost: ghost);
                break;
        }

        return root;
    }

    static void Part(GameObject root, Vector3 localPos, Vector3 size, Color color, bool emissive = false, bool ghost = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Part";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        var renderer = go.GetComponent<MeshRenderer>();
        if (_shared == null)
        {
            _shared = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            _shared.EnableKeyword("_EMISSION");
        }

        renderer.sharedMaterial = _shared;
        // У призрака цвет задаёт материал превью (зелёный/красный) — блок цвета его бы перебил.
        if (ghost)
            return;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        if (emissive)
            block.SetColor("_EmissionColor", color * 2.5f);
        renderer.SetPropertyBlock(block);
    }

    static void AddLight(GameObject root, Vector3 localPos, Color color)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 4f;
        light.intensity = 1.5f;
        light.shadows = LightShadows.None;
    }
}
