using UnityEngine;

public enum BuildingPieceKind
{
    Foundation = 0,
    Wall = 1,
    Roof = 2,
    Stairs = 3
}

/// <summary>One placeable building recipe (cost in stone for now).</summary>
[CreateAssetMenu(menuName = "Catapulto/Building/Definition", fileName = "BuildingDefinition")]
public sealed class BuildingDefinition : ScriptableObject
{
    [SerializeField] string _id = "foundation";
    [SerializeField] string _displayName = "Foundation";
    [SerializeField] BuildingPieceKind _kind = BuildingPieceKind.Foundation;
    [SerializeField] Color _iconColor = new Color(0.45f, 0.42f, 0.38f, 1f);
    [SerializeField] int _stoneCost = 10;
    [SerializeField] Vector3 _size = new Vector3(2f, 0.3f, 2f);
    [SerializeField] GameObject _prefab;
    [SerializeField] bool _snapToGrid = true;
    [SerializeField] bool _snapToNeighbors = true;

    public string Id => _id;
    public string DisplayName => _displayName;
    public BuildingPieceKind Kind => _kind;
    public Color IconColor => _iconColor;
    public int StoneCost => Mathf.Max(0, _stoneCost);
    public Vector3 Size => _size;
    public GameObject Prefab => _prefab;
    public bool SnapToGrid => _snapToGrid;
    public bool SnapToNeighbors => _snapToNeighbors;
}
