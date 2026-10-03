using UnityEngine;

/// <summary>
/// Clones terrain tree data at play start and plants pickaxe colliders.
/// Trees on the player's castle pad are decorative only.
/// </summary>
[DefaultExecutionOrder(80)]
public sealed class HarvestableForest : MonoBehaviour
{
    const float PadClearExtra = 2.5f;
    const int MaxTrees = 500;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<HarvestableForest>() != null)
            return;
        var host = new GameObject("HarvestableForest");
        host.AddComponent<HarvestableForest>();
    }

    void Start()
    {
        ResolvePlayerPad(out Vector3 padCenter, out float padRadius);

        var terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            var source = terrain.terrainData;
            var clone = Instantiate(source);
            clone.name = source.name + "_Play";
            terrain.terrainData = clone;
            var col = terrain.GetComponent<TerrainCollider>();
            if (col != null)
                col.terrainData = clone;
            WrapTerrainTrees(terrain, clone, padCenter, padRadius);
        }

        WrapSceneMeshTrees(padCenter, padRadius);
    }

    void WrapTerrainTrees(Terrain terrain, TerrainData clone, Vector3 padCenter, float padRadius)
    {
        var trees = clone.treeInstances;
        int count = Mathf.Min(trees.Length, MaxTrees);
        Vector3 size = clone.size;
        Vector3 origin = terrain.transform.position;

        for (int i = 0; i < count; i++)
        {
            var inst = trees[i];
            Vector3 world = origin + Vector3.Scale(inst.position, size);
            if (IsOnPlayerPad(world, padCenter, padRadius))
                continue;

            float height = Mathf.Clamp(4.5f * Mathf.Max(0.4f, inst.heightScale), 3.2f, 9f);
            float radius = Mathf.Clamp(0.38f * Mathf.Max(0.4f, inst.widthScale), 0.28f, 0.7f);

            var go = new GameObject($"HarvestTree_{i}");
            go.transform.SetParent(transform, false);
            go.transform.position = world;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            capsule.direction = 1;

            var tree = go.AddComponent<HarvestableTree>();
            tree.Bind(terrain, i);
        }
    }

    void WrapSceneMeshTrees(Vector3 padCenter, float padRadius)
    {
        var rends = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < rends.Length; i++)
        {
            var rend = rends[i];
            if (rend == null)
                continue;
            var go = rend.gameObject;
            if (!LooksLikeTree(go.name))
                continue;
            if (go.GetComponentInParent<HarvestableTree>() != null)
                continue;

            if (go.GetComponent<Collider>() == null)
            {
                Bounds b = rend.bounds;
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.height = Mathf.Max(2.2f, b.size.y);
                capsule.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.28f, 0.22f, 1.1f);
                capsule.center = go.transform.InverseTransformPoint(b.center);
            }

            go.AddComponent<HarvestableTree>();
        }
    }

    static bool LooksLikeTree(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (name.StartsWith("HarvestTree_"))
            return false;
        if (name.IndexOf("stump", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return name.IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsOnPlayerPad(Vector3 world, Vector3 padCenter, float padRadius)
    {
        if (padRadius <= 0.5f)
            return false;
        Vector2 delta = new Vector2(world.x - padCenter.x, world.z - padCenter.z);
        return delta.sqrMagnitude <= padRadius * padRadius;
    }

    static void ResolvePlayerPad(out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;

        var nodes = FindObjectsByType<MapNodeMarker>(FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null || node.Kind != MapNodeKind.PlayerCastle)
                continue;
            var pad = node.GetComponent<CastlePlatform>();
            center = pad != null ? pad.transform.position : node.transform.position;
            radius = (pad != null ? pad.SurfaceRadius : 14f) + PadClearExtra;
            return;
        }

        var castle = SquareCastle.FindPlayerOwned();
        if (castle == null)
            return;
        center = castle.transform.position;
        radius = 16f + PadClearExtra;
    }
}
