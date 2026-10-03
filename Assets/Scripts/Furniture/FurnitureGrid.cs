using UnityEngine;

/// <summary>Courtyard furniture pads on the player castle. Built with the hull, not wall modules.</summary>
public sealed class FurnitureGrid : MonoBehaviour
{
    public const float PadWidth = 1.7f;
    public const float PadDepth = 1.15f;
    const float Spacing = 2.55f;
    const float MageClearance = 2.15f;

    FurnitureSlot[] _slots = System.Array.Empty<FurnitureSlot>();

    public FurnitureSlot[] Slots
    {
        get
        {
            if (_slots == null || _slots.Length == 0)
                _slots = GetComponentsInChildren<FurnitureSlot>(true);
            return _slots;
        }
    }

    public static FurnitureGrid FindOnPlayerCastle()
    {
        var castle = SquareCastle.FindPlayerOwned();
        if (castle == null)
            return null;
        var existing = castle.GetComponentInChildren<FurnitureGrid>(true);
        return existing != null ? existing : Build(castle.transform);
    }

    public static FurnitureGrid Build(Transform castleRoot)
    {
        var existing = castleRoot.GetComponentInChildren<FurnitureGrid>(true);
        if (existing != null)
            return existing;

        var go = new GameObject("FurnitureGrid");
        go.transform.SetParent(castleRoot, false);
        var grid = go.AddComponent<FurnitureGrid>();
        grid.CreatePads();
        return grid;
    }

    void CreatePads()
    {
        int index = 0;
        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0)
                    continue;

                Vector3 local = new Vector3(x * Spacing, 0.05f, z * Spacing);
                if (local.magnitude < MageClearance)
                    continue;

                _ = CreatePad(local, index);
                index++;
            }
        }

        _slots = GetComponentsInChildren<FurnitureSlot>(true);
    }

    FurnitureSlot CreatePad(Vector3 localPos, int index)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = $"FurniturePad_{index}";
        pad.transform.SetParent(transform, false);
        pad.transform.localPosition = localPos;
        pad.transform.localScale = new Vector3(PadWidth, 0.04f, PadDepth);

        var col = pad.GetComponent<BoxCollider>();
        col.isTrigger = true;

        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        Color idle = new Color(0.88f, 0.84f, 0.58f, 0.32f);
        mat.renderQueue = 3000;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", idle);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", idle);
        pad.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var slot = pad.AddComponent<FurnitureSlot>();
        pad.GetComponent<MeshRenderer>().enabled = false;
        return slot;
    }

    public void SetPadsVisible(bool on)
    {
        var slots = Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetVisible(on);
        }
    }

    public FurnitureSlot FindAimSlot(Ray ray, float maxDistance)
    {
        FurnitureSlot best = null;
        float bestDist = maxDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
        {
            var slot = hit.collider.GetComponentInParent<FurnitureSlot>();
            if (slot != null && !slot.IsOccupied)
                return slot;
        }

        var slots = Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.IsOccupied)
                continue;
            Vector3 to = slot.Anchor.position - ray.origin;
            float along = Vector3.Dot(to, ray.direction);
            if (along < 0.4f || along > maxDistance)
                continue;
            Vector3 closest = ray.origin + ray.direction * along;
            float lateral = Vector3.Distance(closest, slot.Anchor.position);
            if (lateral > 1.35f)
                continue;
            if (along < bestDist)
            {
                bestDist = along;
                best = slot;
            }
        }

        return best;
    }
}
