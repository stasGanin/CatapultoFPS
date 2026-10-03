using UnityEngine;

public enum CastleModuleKind
{
    Door = 0,
    Storage = 1,
    Wall = 2,
    Spawner = 3,
    Tower = 4,
    Section = 5,
    Window = 6,
}

/// <summary>Castle module recipe — snaps only to matching sockets and replaces a wall segment.</summary>
[CreateAssetMenu(menuName = "Catapulto/Castle/Module Definition", fileName = "CastleModule")]
public sealed class CastleModuleDefinition : ScriptableObject
{
    [SerializeField] string _id = "door";
    [SerializeField] string _displayName = "Door";
    [SerializeField] CastleModuleKind _kind = CastleModuleKind.Door;
    [SerializeField] CastleSocketKind _socketKind = CastleSocketKind.Wall;
    [SerializeField] string _prefabResource = "Castle/Modules/DoorModule";
    [SerializeField] Color _iconColor = new Color(0.55f, 0.4f, 0.25f);
    [SerializeField] int _stoneCost = 5;

    public string Id => _id;
    public string DisplayName => _displayName;
    public CastleModuleKind Kind => _kind;
    public CastleSocketKind RequiredSocketKind => _socketKind;
    public Color IconColor => _iconColor;
    public int StoneCost => Mathf.Max(0, _stoneCost);
    public string PrefabResource => _prefabResource;
}
