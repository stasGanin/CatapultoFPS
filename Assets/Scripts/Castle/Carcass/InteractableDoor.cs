using UnityEngine;

/// <summary>Swings the door leaf in a wall-door module on E.</summary>
public sealed class InteractableDoor : MonoBehaviour, IPlayerInteractable
{
    const float OpenAngle = 95f;
    const float Duration = 0.28f;
    const float DoorwayDepth = 2f;
    const float DoorwaySidePadding = 0.3f;
    const float DoorwayFallbackHeight = 2f;
    const float DoorwayFallbackWidth = 1.2f;

    Transform _leaf;
    Quaternion _closed;
    Quaternion _open;
    bool _isOpen;
    float _t;

    public bool IsOpen => _isOpen;

    public void BindLeaf(Transform leaf)
    {
        Transform source = leaf != null ? leaf : transform;
        _leaf = EnsureHinge(source, transform);
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

    /// <summary>
    /// Хитбокс для E в проёме: по размеру створки и на метр в обе стороны от стены.
    /// Заодно держит проход свободным — станции на сетке пола сюда не встанут.
    /// </summary>
    void EnsureDoorwayVolume()
    {
        if (transform.Find("DoorwayVolume") != null)
            return;

        Bounds leaf = new Bounds(
            new Vector3(0f, DoorwayFallbackHeight * 0.5f, 0f),
            new Vector3(CarcassMetrics.WallThickness, DoorwayFallbackHeight, DoorwayFallbackWidth));
        var filter = _leaf != null ? _leaf.GetComponentInChildren<MeshFilter>() : null;
        if (filter != null && filter.sharedMesh != null)
            leaf = LocalBounds(filter, transform);

        var go = new GameObject("DoorwayVolume");
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, leaf.size.y * 0.5f, leaf.center.z);
        box.size = new Vector3(DoorwayDepth, leaf.size.y, leaf.size.z + DoorwaySidePadding);
    }

    /// <summary>
    /// Петля — у бокового края створки, в осях модуля (стена вдоль локальной Z).
    /// Раньше петля стояла посередине ширины в мировых осях: открытая створка вставала поперёк проёма.
    /// </summary>
    static Transform EnsureHinge(Transform leaf, Transform module)
    {
        if (leaf.parent != null && leaf.parent.name == "DoorHinge")
            return leaf.parent;

        Vector3 hingeWorld = leaf.position;
        var filter = leaf.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds b = LocalBounds(filter, module);
            hingeWorld = module.TransformPoint(new Vector3(b.center.x, b.min.y, b.min.z));
        }

        var hinge = new GameObject("DoorHinge");
        hinge.transform.SetPositionAndRotation(hingeWorld, module.rotation);
        hinge.transform.SetParent(leaf.parent, true);
        leaf.SetParent(hinge.transform, true);
        return hinge.transform;
    }

    static Bounds LocalBounds(MeshFilter filter, Transform space)
    {
        Bounds mesh = filter.sharedMesh.bounds;
        var local = new Bounds(space.InverseTransformPoint(filter.transform.TransformPoint(mesh.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? mesh.min.x : mesh.max.x,
                (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                (i & 4) == 0 ? mesh.min.z : mesh.max.z);
            local.Encapsulate(space.InverseTransformPoint(filter.transform.TransformPoint(corner)));
        }

        return local;
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
