using UnityEngine;

/// <summary>Ядра для башен хранятся в любых сундуках замка: общий учёт для ручной и автоматической стрельбы.</summary>
public static class CastleAmmoStorage
{
    const string CannonballPath = "Items/CannonballItem";

    static ItemDefinition _cannonball;

    public static ItemDefinition Cannonball
    {
        get
        {
            if (_cannonball == null)
                _cannonball = Resources.Load<ItemDefinition>(CannonballPath);
            return _cannonball;
        }
    }

    public static int Count(CarcassCastle castle)
    {
        int total = 0;
        foreach (var storage in castle.GetComponentsInChildren<StorageContainer>())
            total += storage.CountItem(Cannonball);
        return total;
    }

    public static bool TryConsume(CarcassCastle castle)
    {
        foreach (var storage in castle.GetComponentsInChildren<StorageContainer>())
        {
            if (storage.TryConsumeItem(Cannonball, 1))
                return true;
        }

        return false;
    }
}
