using UnityEngine;

[CreateAssetMenu(fileName = "CastleWallConfig", menuName = "Catapulto/Castle/Wall Config")]
public class CastleWallConfig : ScriptableObject
{
    [Header("Voxel damage grid over art mesh")]
    [SerializeField, Min(0.15f)] float _voxelCellSize = 0.35f;
    [SerializeField] Color _blockColor = new Color(0.92f, 0.9f, 0.86f, 1f);
    [SerializeField, Min(1)] int _maxDebrisPerBlast = 24;
    [SerializeField, Min(0.1f)] float _debrisMass = 25f;
    [SerializeField, Min(0f)] float _debrisForceScale = 0.22f;
    [SerializeField, Min(0.05f)] float _mineRadius = 0.75f;
    [SerializeField, Min(1)] int _cellMaxHp = 10;
    [SerializeField, Min(1)] int _debrisMaxHp = 10;

    public float VoxelCellSize => _voxelCellSize;
    public Color BlockColor => _blockColor;
    public int MaxDebrisPerBlast => _maxDebrisPerBlast;
    public float DebrisMass => _debrisMass;
    public float DebrisForceScale => _debrisForceScale;
    public float MineRadius => _mineRadius;
    public int CellMaxHp => _cellMaxHp;
    public int DebrisMaxHp => _debrisMaxHp;
}
