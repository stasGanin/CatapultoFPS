using UnityEngine;

/// <summary>Pre-marked courtyard pad for furniture (chest, later workbench).</summary>
public sealed class FurnitureSlot : MonoBehaviour
{
    [SerializeField] bool _occupied;
    [SerializeField] GameObject _placed;

    MeshRenderer _pad;

    public bool IsOccupied => _occupied;
    public Transform Anchor => transform;

    void Awake()
    {
        _pad = GetComponent<MeshRenderer>();
        SetVisible(false);
    }

    public void SetVisible(bool on)
    {
        if (_pad == null)
            _pad = GetComponent<MeshRenderer>();
        if (_pad != null)
            _pad.enabled = on && !_occupied;
        ApplyTint(hovered: false);
    }

    public void SetHovered(bool hovered)
    {
        if (_occupied)
            return;
        ApplyTint(hovered);
    }

    public bool TryPlace(GameObject instance)
    {
        if (_occupied || instance == null)
            return false;

        _occupied = true;
        _placed = instance;
        instance.transform.SetParent(transform.parent, true);
        instance.transform.SetPositionAndRotation(GetPlacePose(), transform.rotation);
        SetVisible(false);
        return true;
    }

    public Vector3 GetPlacePose()
    {
        return transform.position + Vector3.up * 0.02f;
    }

    void ApplyTint(bool hovered)
    {
        if (_pad == null)
            return;
        var block = new MaterialPropertyBlock();
        Color c = hovered
            ? new Color(0.95f, 0.92f, 0.62f, 0.55f)
            : new Color(0.88f, 0.84f, 0.58f, 0.32f);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        _pad.SetPropertyBlock(block);
    }
}
