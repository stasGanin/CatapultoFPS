using System;
using UnityEngine;

/// <summary>Player hit points, damage and death events. Respawn lives in PlayerRespawn.</summary>
public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    /// <summary>Сколько секунд после урона не работает регенерация от таланта.</summary>
    const float RegenDelayAfterDamage = 5f;

    PlayerCombatConfig _config;
    PlayerTalents _talents;
    float _health;
    float _lastMaxHealth;
    float _lastDamageTime = float.NegativeInfinity;

    public float MaxHealth => _config.MaxHealth + PlayerTalents.FlatBonus(TalentStat.MaxHealth);
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
        _lastMaxHealth = _config.MaxHealth;
    }

    void Start()
    {
        _talents = GetComponent<PlayerTalents>();
        if (_talents != null)
            _talents.Changed += OnTalentsChanged;
    }

    void OnDestroy()
    {
        if (_talents != null)
            _talents.Changed -= OnTalentsChanged;
    }

    void Update()
    {
        float regen = PlayerTalents.FlatBonus(TalentStat.HealthRegen);
        if (regen > 0f && !IsDead && _health < MaxHealth && Time.time - _lastDamageTime >= RegenDelayAfterDamage)
            Heal(regen * Time.deltaTime);
    }

    /// <summary>Рост максимума поднимает и текущее здоровье на ту же величину — иначе талант «пустой» до лечения.</summary>
    void OnTalentsChanged()
    {
        float max = MaxHealth;
        if (!IsDead && max > _lastMaxHealth)
            _health += max - _lastMaxHealth;
        _lastMaxHealth = max;
        HealthChanged?.Invoke(_health, max);
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (IsDead || amount <= 0f)
            return;

        _health = Mathf.Max(0f, _health - amount);
        _lastDamageTime = Time.time;
        Damaged?.Invoke(amount, info);
        HealthChanged?.Invoke(_health, _config.MaxHealth);
        if (IsDead)
            Died?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        _health = Mathf.Min(MaxHealth, _health + amount);
        HealthChanged?.Invoke(_health, MaxHealth);
    }

    public void ResetFull()
    {
        _health = MaxHealth;
        HealthChanged?.Invoke(_health, MaxHealth);
    }
}
