using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caps how many ranged enemies attack the player at once, so fights stay readable.
/// </summary>
public static class EnemyAttackTokens
{
    // Больше двух одновременных очередей превращают бой в шум: игрок не может прочитать телеграфы.
    const int MaxConcurrentShooters = 2;

    static readonly HashSet<int> Holders = new HashSet<int>();

    public static bool TryAcquire(Object holder)
    {
        int id = holder.GetInstanceID();
        if (Holders.Contains(id))
            return true;
        if (Holders.Count >= MaxConcurrentShooters)
            return false;
        Holders.Add(id);
        return true;
    }

    public static void Release(Object holder)
    {
        if (holder != null)
            Holders.Remove(holder.GetInstanceID());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Holders.Clear();
}
