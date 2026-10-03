using System;
using UnityEngine;

/// <summary>Player mana pool. Regenerates every frame; weapons spend via TrySpend.</summary>
public sealed class PlayerMana : MonoBehaviour
{
    [SerializeField] float _maxMana = 100f;
    [SerializeField] float _regenPerSecond = 10f;

    float _mana;

    public float MaxMana => _maxMana + PlayerTalents.FlatBonus(TalentStat.MaxMana);
    public float Mana => _mana;
    public bool IsEmpty => _mana <= 0.01f;

    public event Action<float, float> ManaChanged;

    void Awake()
    {
        _mana = _maxMana;
    }

    void Update()
    {
        float max = MaxMana;
        if (_mana >= max || _regenPerSecond <= 0f)
            return;
        _mana = Mathf.Min(max, _mana + _regenPerSecond * Time.deltaTime);
        ManaChanged?.Invoke(_mana, max);
    }

    public bool CanAfford(float amount) => amount <= 0f || _mana + 0.001f >= amount;

    public bool TrySpend(float amount)
    {
        if (!CanAfford(amount))
            return false;
        if (amount > 0f)
        {
            _mana = Mathf.Max(0f, _mana - amount);
            ManaChanged?.Invoke(_mana, _maxMana);
        }

        return true;
    }
}
