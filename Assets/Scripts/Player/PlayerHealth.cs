using System;
using UnityEngine;

/// <summary>Player hit points. Enemy projectiles deal tiny chip damage.</summary>
public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] float _maxHealth = 10000f;

    float _health;

    public float MaxHealth => _maxHealth;
    public float Health => _health;
    public bool IsDead => _health <= 0f;

    public event Action<float, float> HealthChanged;

    void Awake()
    {
        _health = _maxHealth;
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (IsDead || amount <= 0f)
            return;

        _health = Mathf.Max(0f, _health - amount);
        HealthChanged?.Invoke(_health, _maxHealth);
    }

    public void ResetFull()
    {
        _health = _maxHealth;
        HealthChanged?.Invoke(_health, _maxHealth);
    }
}
