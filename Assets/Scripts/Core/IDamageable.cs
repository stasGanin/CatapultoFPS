using UnityEngine;

/// <summary>
/// То, что может получать урон (стены, юниты и т.д.).
/// </summary>
public interface IDamageable
{
    void ApplyDamage(float amount, in DamageInfo info);
}

public readonly struct DamageInfo
{
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly Vector3 Direction;
    public readonly bool FromPlayer;

    public DamageInfo(Vector3 point, Vector3 normal, Vector3 direction, bool fromPlayer = true)
    {
        Point = point;
        Normal = normal;
        Direction = direction;
        FromPlayer = fromPlayer;
    }
}
