using UnityEngine;

/// <summary>Empty module socket — owns a wall segment that modules replace.</summary>
public sealed class CastleSocket : MonoBehaviour
{
    [SerializeField] CastleSocketKind _kind = CastleSocketKind.Wall;
    [SerializeField] int _index;
    [SerializeField] bool _occupied;
    [SerializeField] CastleWallSegment _segment;
    [SerializeField] GameObject _placedModule;
    [SerializeField] GameObject _ghostVisual;

    public CastleSocketKind Kind => _kind;
    public int Index => _index;
    public bool IsOccupied => _occupied;
    public CastleWallSegment Segment => _segment;
    public Transform Anchor => transform;

    public void Setup(CastleSocketKind kind, int index, CastleWallSegment segment)
    {
        _kind = kind;
        _index = index;
        _segment = segment;
        _occupied = false;
        _ghostVisual = transform.Find("Ghost")?.gameObject;
        gameObject.name = kind == CastleSocketKind.Corner
            ? $"Socket_Corner_{index}"
            : $"Socket_Wall_{index}";
    }

    public void SetHighlight(bool on)
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Color baseCol = _kind == CastleSocketKind.Corner
            ? new Color(0.25f, 0.65f, 0.95f)
            : new Color(0.95f, 0.8f, 0.25f);
        if (on)
            baseCol = Color.Lerp(baseCol, Color.white, 0.55f);
        block.SetColor("_BaseColor", baseCol);
        block.SetColor("_Color", baseCol);
        renderer.SetPropertyBlock(block);
    }

    public bool TryPlace(CastleModuleDefinition def, GameObject moduleInstance)
    {
        if (_occupied || def == null || moduleInstance == null)
            return false;
        if (def.RequiredSocketKind != _kind)
            return false;

        _occupied = true;
        _placedModule = moduleInstance;
        Transform frameRoot = transform.root != null ? transform.root : transform.parent;
        moduleInstance.transform.SetParent(frameRoot != null ? frameRoot : null, true);

        Vector3 pos = GetModulePose();
        Quaternion rot = GetModuleWorldRotation();
        moduleInstance.transform.SetPositionAndRotation(pos, rot);

        if (_segment != null)
            _segment.SetReplaced(true);

        if (_ghostVisual != null)
            _ghostVisual.SetActive(false);

        // Hide socket pad while occupied
        var padRenderer = GetComponent<MeshRenderer>();
        if (padRenderer != null)
            padRenderer.enabled = false;

        return true;
    }

    public Quaternion GetModuleWorldRotation()
    {
        if (_segment != null)
            return _segment.transform.rotation;
        return GetModulePoseRotation();
    }

    public Quaternion GetModulePoseRotation()
    {
        Vector3 outward = transform.localPosition;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
            return transform.rotation;
        return transform.parent != null
            ? transform.parent.rotation * Quaternion.LookRotation(outward.normalized, Vector3.up)
            : Quaternion.LookRotation(outward.normalized, Vector3.up);
    }

    public Vector3 GetModulePose()
    {
        if (_segment != null)
            return _segment.transform.position;
        return transform.position + Vector3.up * 0.5f;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = _kind == CastleSocketKind.Corner
            ? new Color(0.3f, 0.7f, 1f, 0.85f)
            : new Color(1f, 0.85f, 0.3f, 0.85f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 1.1f);
    }
#endif
}
