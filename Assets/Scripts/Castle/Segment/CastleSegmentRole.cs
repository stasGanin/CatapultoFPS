namespace Catapulto.Battle.Castle
{
    /// <summary>How this castle piece participates in combat (FPS uses Normal only).</summary>
    public enum CastleSegmentRole
    {
        Normal = 0,
        WeaponEmitter = 1,
        EnemyCore = 2
    }
}
