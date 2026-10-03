using UnityEngine;

/// <summary>Player survivability balance: hit points and respawn timing.</summary>
[CreateAssetMenu(menuName = "Catapulto/Player/Player Combat Config", fileName = "PlayerCombatConfig")]
public sealed class PlayerCombatConfig : ScriptableObject
{
    [SerializeField, Min(1f)] float _maxHealth = 100f;
    [Tooltip("Сколько секунд игрок лежит, прежде чем возродиться у своего мага.")]
    [SerializeField, Min(0f)] float _respawnDelay = 4f;

    public float MaxHealth => _maxHealth;
    public float RespawnDelay => _respawnDelay;

    /// <summary>Компонент здоровья добавляется игроку в рантайме, поэтому конфиг грузится из Resources.</summary>
    public static PlayerCombatConfig Load()
    {
        var config = Resources.Load<PlayerCombatConfig>("Player/PlayerCombatConfig");
        if (config != null)
            return config;
        Debug.LogError("PlayerCombatConfig not found at Resources/Player/PlayerCombatConfig. Using code defaults.");
        return CreateInstance<PlayerCombatConfig>();
    }
}
