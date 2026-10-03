using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Купленные ранги талантов и итоговые бонусы. Статический <see cref="Local"/> нужен системам,
/// у которых нет ссылки на игрока (урон в DamageUtility).
/// </summary>
public sealed class PlayerTalents : MonoBehaviour
{
    readonly Dictionary<string, int> _ranks = new Dictionary<string, int>();

    TalentTree _tree;
    PlayerProgression _progression;

    public static PlayerTalents Local { get; private set; }
    public TalentTree Tree => _tree;
    public event Action Changed;

    void Awake()
    {
        Local = this;
        _tree = Resources.Load<TalentTree>(TalentTree.ResourcePath);
        if (_tree == null)
            Debug.LogError($"PlayerTalents: no TalentTree at Resources/{TalentTree.ResourcePath}.");
        _progression = GetComponent<PlayerProgression>();
    }

    void OnDestroy()
    {
        if (Local == this)
            Local = null;
    }

    public int GetRank(TalentNode node) => node != null && _ranks.TryGetValue(node.Id, out int rank) ? rank : 0;

    public bool PrerequisitesMet(TalentNode node)
    {
        foreach (string id in node.PrerequisiteIds)
        {
            TalentNode prerequisite = _tree != null ? _tree.Find(id) : null;
            if (prerequisite != null && GetRank(prerequisite) < 1)
                return false;
        }

        return true;
    }

    public bool CanBuy(TalentNode node) =>
        node != null && GetRank(node) < node.MaxRank && PrerequisitesMet(node)
        && _progression != null && _progression.TalentPoints > 0;

    public bool TryBuy(TalentNode node)
    {
        if (!CanBuy(node) || !_progression.TrySpendPoint())
            return false;
        _ranks[node.Id] = GetRank(node) + 1;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Сумма бонусов по всем рангам для стата.</summary>
    public float Bonus(TalentStat stat)
    {
        if (_tree == null)
            return 0f;
        float total = 0f;
        foreach (var node in _tree.Nodes)
        {
            if (node.Stat == stat)
                total += node.ValuePerRank * GetRank(node);
        }

        return total;
    }

    /// <summary>Множитель 1 + бонус; без игрока (меню, тесты) — нейтральные 1.</summary>
    public static float Multiplier(TalentStat stat) => 1f + FlatBonus(stat);

    public static float FlatBonus(TalentStat stat) => Local != null ? Local.Bonus(stat) : 0f;
}
