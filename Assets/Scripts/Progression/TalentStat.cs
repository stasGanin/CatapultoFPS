/// <summary>Что именно улучшает талант. Доли (Damage, MoveSpeed, HealingPower, XpGain) считаются множителем 1 + сумма, остальное — плоской прибавкой.</summary>
public enum TalentStat
{
    Damage = 0,
    MaxHealth = 1,
    MaxMana = 2,
    MoveSpeed = 3,
    HealingPower = 4,
    HealthRegen = 5,
    XpGain = 6
}
