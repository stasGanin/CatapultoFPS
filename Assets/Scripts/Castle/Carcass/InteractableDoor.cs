using UnityEngine;

/// <summary>Swings the door leaf in a wall-door module on E.</summary>
public sealed class InteractableDoor : MonoBehaviour, IPlayerInteractable
{
    const float OpenAngle = 95f;
    const float Duration = 0.28f;

    Transform _leaf;
    Quaternion _closed;
    Quaternion _open;
    bool _isOpen;
    float _t;

    public bool IsOpen => _isOpen;

    public void BindLeaf(Transform leaf)
    {
        Transform source = leaf != null ? leaf : transform;
        _leaf = EnsureHinge(source);
        _closed = _leaf.localRotation;
        _open = _closed * Quaternion.Euler(0f, OpenAngle, 0f);
        if (source.GetComponent<Collider>() == null)
            source.gameObject.AddComponent<BoxCollider>();
        EnsureDoorwayVolume();
    }

    void Awake()
    {
        if (_leaf == null)
        {
            Transform found = FindLeaf(transform);
            BindLeaf(found != null ? found : transform);
        }
        else
        {
            EnsureDoorwayVolume();
        }
    }

    void Update()
    {
        if (_leaf == null)
            return;

        float target = _isOpen ? 1f : 0f;
        if (Mathf.Abs(_t - target) < 0.001f)
            return;

        _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Duration);
        float eased = _t * _t * (3f - 2f * _t);
        _leaf.localRotation = Quaternion.Slerp(_closed, _open, eased);
    }

    public string InteractLabel => _isOpen ? "Close door" : "Open door";

    public bool CanInteract() => isActiveAndEnabled && _leaf != null;

    public void Interact(PlayerInventory inventory)
    {
        _isOpen = !_isOpen;
        Vector3 p = _leaf != null ? _leaf.position : transform.position;
        HitSparkVfx.PlayDust(p + Vector3.up * 0.2f, _isOpen ? transform.right : -transform.right, 8);
    }

    void EnsureDoorwayVolume()
    {
        if (transform.Find("DoorwayVolume") != null)
            return;

        Bounds b = LocalRenderBounds();
        if (b.size.sqrMagnitude < 0.01f)
        {
            b = new Bounds(
                new Vector3(0f, CarcassMetrics.WallHeight * 0.5f, 0f),
                new Vector3(CarcassMetrics.WallThickness, CarcassMetrics.WallHeight, CarcassMetrics.WallAlong));
        }

        var go = new GameObject("DoorwayVolume");
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = b.center;
        Vector3 size = b.size;
        if (size.x <= size.z)
            size.x += 0.8f;
        else
            size.z += 0.8f;
        size.y = Mathf.Max(size.y, CarcassMetrics.WallHeight * 0.85f);
        box.size = size;
    }

    Bounds LocalRenderBounds()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Bounds b = new Bounds(transform.InverseTransformPoint(rends[0].bounds.center), Vector3.zero);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null)
                continue;
            Bounds wb = rends[i].bounds;
            b.Encapsulate(transform.InverseTransformPoint(wb.min));
            b.Encapsulate(transform.InverseTransformPoint(wb.max));
        }

        return b;
    }

    static Transform EnsureHinge(Transform leaf)
    {
        if (leaf.parent != null && leaf.parent.name == "DoorHinge")
            return leaf.parent;

        var rend = leaf.GetComponent<Renderer>();
        Vector3 hingeWorld = leaf.position;
        if (rend != null)
        {
            Bounds b = rend.bounds;
            hingeWorld = new Vector3(b.min.x, b.min.y, b.center.z);
        }

        var hinge = new GameObject("DoorHinge");
        hinge.transform.SetPositionAndRotation(hingeWorld, leaf.rotation);
        hinge.transform.SetParent(leaf.parent, true);
        leaf.SetParent(hinge.transform, true);
        return hinge.transform;
    }

    static Transform FindLeaf(Transform root)
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
            if (n.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Frame", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) < 0)
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
}
