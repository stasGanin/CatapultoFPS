using UnityEngine;

/// <summary>Balance values for one enemy type. Variants are separate assets of this type.</summary>
[CreateAssetMenu(menuName = "Catapulto/Enemies/Enemy Config", fileName = "EnemyConfig")]
public sealed class EnemyConfig : ScriptableObject
{
    [Header("Живучесть")]
    [SerializeField, Min(1f)] float _maxHealth = 20f;

    [Header("Движение")]
    [SerializeField, Min(0f)] float _moveSpeed = 5f;
    [Tooltip("Дистанция, на которой стрелок держится от цели.")]
    [SerializeField, Min(0f)] float _standoffDistance = 10f;
    [Tooltip("Высота полёта над землёй (только летающие).")]
    [SerializeField, Min(0f)] float _hoverHeight = 1.6f;

    [Header("Восприятие")]
    [Tooltip("С какой дистанции враг переключается на игрока.")]
    [SerializeField, Min(0f)] float _aggroRange = 26f;
    [Tooltip("Сколько секунд враг помнит игрока после потери видимости.")]
    [SerializeField, Min(0f)] float _loseSightGrace = 1.2f;
    [Tooltip("Идёт на осаду замка игрока, только если тот ближе этой дистанции к дому. Иначе охраняет свой замок.")]
    [SerializeField, Min(0f)] float _raidRange = 90f;

    [Header("Атака")]
    [SerializeField, Min(0f)] float _attackRange = 24f;
    [SerializeField, Min(0f)] float _damage = 4f;
    [Tooltip("Урон по стенам замка игрока.")]
    [SerializeField, Min(0f)] float _siegeDamage = 2f;
    [Tooltip("Замах перед атакой: враг светится и замирает, игрок успевает среагировать.")]
    [SerializeField, Min(0f)] float _telegraphTime = 0.45f;
    [SerializeField, Min(1)] int _burstCount = 1;
    [SerializeField, Min(0f)] float _burstInterval = 0.16f;
    [SerializeField, Min(0f)] float _cooldown = 2f;
    [Tooltip("Случайный разброс паузы, чтобы враги не атаковали синхронно.")]
    [SerializeField, Min(0f)] float _cooldownJitter = 0.4f;
    [SerializeField, Min(0f)] float _projectileSpeed = 11f;
    [Tooltip("Доля скорости игрока, на которую стрелок упреждает. 0 — стреляет в текущую позицию.")]
    [SerializeField, Range(0f, 1f)] float _leadFactor = 0.35f;

    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public float StandoffDistance => _standoffDistance;
    public float HoverHeight => _hoverHeight;
    public float AggroRange => _aggroRange;
    public float LoseSightGrace => _loseSightGrace;
    public float RaidRange => _raidRange;
    public float AttackRange => _attackRange;
    public float Damage => _damage;
    public float SiegeDamage => _siegeDamage;
    public float TelegraphTime => _telegraphTime;
    public int BurstCount => _burstCount;
    public float BurstInterval => _burstInterval;
    public float Cooldown => _cooldown;
    public float CooldownJitter => _cooldownJitter;
    public float ProjectileSpeed => _projectileSpeed;
    public float LeadFactor => _leadFactor;

    public float RollCooldown() => _cooldown + Random.Range(-_cooldownJitter, _cooldownJitter);

    /// <summary>Враги создаются в рантайме без префаба, поэтому конфиг грузится из Resources.</summary>
    public static EnemyConfig Load(string resourcePath)
    {
        var config = Resources.Load<EnemyConfig>(resourcePath);
        if (config != null)
            return config;

        Debug.LogError($"EnemyConfig not found at Resources/{resourcePath}. Using code defaults.");
        return CreateInstance<EnemyConfig>();
    }
}
