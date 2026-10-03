using UnityEngine;

/// <summary>One of two wall slots on a carcass edge. Neighbors share this object.</summary>
public sealed class CarcassBay : MonoBehaviour
{
    [SerializeField] int _cellX;
    [SerializeField] int _cellY;
    [SerializeField] int _cellZ;
    [SerializeField] CarcassMetrics.WallDir _dir;
    [SerializeField] int _slot;
    [SerializeField] CastleModuleKind _occupant = CastleModuleKind.Wall;
    [SerializeField] GameObject _module;

    public int CellX => _cellX;
    public int CellY => _cellY;
    public int CellZ => _cellZ;
    public CarcassMetrics.WallDir Dir => _dir;
    public int Slot => _slot;
    public CastleModuleKind Occupant => _occupant;
    public GameObject Module => _module;
    public bool IsEmpty => _module == null;

    public void Setup(int cellX, int cellY, int cellZ, CarcassMetrics.WallDir dir, int slot)
    {
        _cellX = cellX;
        _cellY = cellY;
        _cellZ = cellZ;
        _dir = dir;
        _slot = slot;
        gameObject.name = $"Bay_{dir}_{slot}_{cellX}_{cellY}_{cellZ}";
    }

    public void BindModule(GameObject module, CastleModuleKind kind)
    {
        _module = module;
        _occupant = kind;
    }

    public void ClearModule()
    {
        if (_module != null)
            Destroy(_module);
        _module = null;
    }

    public void SetPreviewHidden(bool hidden)
    {
        if (_module == null)
            return;
        var rends = _module.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = !hidden;
        }
    }
}
