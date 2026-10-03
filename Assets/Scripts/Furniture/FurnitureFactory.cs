using UnityEngine;

/// <summary>Runtime furniture meshes + ghosts. Not castle wall modules.</summary>
public static class FurnitureFactory
{
    public static GameObject Create(ItemDefinition item, Vector3 position, Quaternion rotation)
    {
        if (item == null || item.Id != "chest")
        {
            Debug.LogError($"FurnitureFactory: unknown furniture id '{item?.Id}'.");
            return null;
        }

        return CreateChest(position, rotation);
    }

    public static GameObject CreateGhost(ItemDefinition item)
    {
        if (item == null || item.Id != "chest")
            return null;
        return CreateChestGhost();
    }

    public static GameObject CreateChest(Vector3 position, Quaternion rotation)
    {
        var root = BuildChestVisual("Furniture_Chest", position, rotation, ghost: false);
        var storage = root.AddComponent<StorageContainer>();
        storage.Configure("Chest", 36, 2.8f);
        return root;
    }

    public static GameObject CreateChestGhost()
    {
        var root = BuildChestVisual("Ghost_Chest", Vector3.zero, Quaternion.identity, ghost: true);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        Color ghost = new Color(0.55f, 0.92f, 0.45f, 0.4f);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", ghost);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", ghost);
        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
            rends[i].sharedMaterial = mat;
        return root;
    }

    static GameObject BuildChestVisual(string name, Vector3 position, Quaternion rotation, bool ghost)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(position, rotation);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Chest";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        body.transform.localScale = new Vector3(1.45f, 0.85f, 0.95f);
        Paint(body, new Color(0.52f, 0.36f, 0.2f));

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(body.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        lid.transform.localScale = new Vector3(1.04f, 0.2f, 1.04f);
        StripCollider(lid);
        Paint(lid, new Color(0.44f, 0.3f, 0.16f));

        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        band.name = "Band";
        band.transform.SetParent(body.transform, false);
        band.transform.localPosition = new Vector3(0f, 0.05f, 0.51f);
        band.transform.localScale = new Vector3(0.18f, 0.55f, 0.08f);
        StripCollider(band);
        Paint(band, new Color(0.72f, 0.62f, 0.28f));

        if (ghost)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].enabled = false;
                Object.Destroy(cols[i]);
            }
        }

        return root;
    }

    static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null)
            return;
        col.enabled = false;
        Object.Destroy(col);
    }

    static void Paint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        renderer.sharedMaterial = mat;
    }
}
