using System;
using UnityEngine;

/// <summary>Уровень героя. Опыт даётся только за действия (плоды опыта), не за убийства; каждый уровень — очко таланта.</summary>
public sealed class PlayerProgression : MonoBehaviour
{
    const int BaseXpToLevel = 30;
    const int XpPerLevelGrowth = 10;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int TalentPoints { get; private set; }

    public int XpToNextLevel => BaseXpToLevel + XpPerLevelGrowth * (Level - 1);

    public event Action Changed;

    /// <summary>Начисляет опыт с учётом таланта XpGain; переносит излишек на следующие уровни.</summary>
    public void AddXp(int amount)
    {
        if (amount <= 0)
            return;
        Xp += Mathf.Max(1, Mathf.RoundToInt(amount * PlayerTalents.Multiplier(TalentStat.XpGain)));
        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            Level++;
            TalentPoints++;
            GameMessages.Post($"Level {Level}! +1 talent point");
        }

        Changed?.Invoke();
    }

    public bool TrySpendPoint()
    {
        if (TalentPoints <= 0)
            return false;
        TalentPoints--;
        Changed?.Invoke();
        return true;
    }
}
