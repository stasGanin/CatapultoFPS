/// <summary>
/// Optional module logic (spawner, turret, door lock…). Called when CastleModuleBreakable HP hits 0.
/// </summary>
public interface ICastleModuleBehavior
{
    bool IsOperational { get; }
    void OnModuleDestroyed();
}
