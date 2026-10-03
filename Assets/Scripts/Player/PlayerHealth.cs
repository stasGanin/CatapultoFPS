using System;
using UnityEngine;

/// <summary>Player hit points, damage and death events. Respawn lives in PlayerRespawn.</summary>
public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    PlayerCombatConfig _config;
    float _health;

    public float MaxHealth => _config.MaxHealth;
    public float Health => _health;
    public bool IsDead => _health <= 0f;

    public event Action<float, float> HealthChanged;
    /// <summary>Amount and hit info — HUD uses it for the damage direction indicator.</summary>
    public event Action<float, DamageInfo> Damaged;
    public event Action Died;

    void Awake()
    {
        _config = PlayerCombatConfig.Load();
        _health = _config.MaxHealth;
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (IsDead || amount <= 0f)
            return;

        _health = Mathf.Max(0f, _health - amount);
        Damaged?.Invoke(amount, info);
        HealthChanged?.Invoke(_health, _config.MaxHealth);
        if (IsDead)
            Died?.Invoke();
    }

    public void ResetFull()
    {
        _health = _config.MaxHealth;
        HealthChanged?.Invoke(_health, _config.MaxHealth);
    }
}
