using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Использование расходников и их откаты. Два пути: ПКМ с руки (занимает UseDuration — под анимацию)
/// и ПКМ в инвентаре (мгновенно). Откат общий на предмет, поэтому одинаков в сумке и на панели.
/// </summary>
[DefaultExecutionOrder(25)]
public sealed class ConsumableUser : MonoBehaviour
{
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] PlayerHealth _health;

    readonly Dictionary<ItemDefinition, float> _cooldownEnd = new Dictionary<ItemDefinition, float>();

    ItemDefinition _eatingItem;
    float _eatEndTime;

    public bool IsEating => _eatingItem != null;
    /// <summary>0..1 прогресс поедания — для анимации в руке.</summary>
    public float EatProgress => IsEating ? 1f - Mathf.Clamp01((_eatEndTime - Time.time) / Mathf.Max(0.01f, _eatingItem.UseDuration)) : 0f;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_health == null)
            _health = GetComponent<PlayerHealth>();
    }

    void OnDisable() => _eatingItem = null;

    void Update()
    {
        bool canUse = _inventory != null && !_inventory.BlocksGameplayInput && !_health.IsDead;
        if (IsEating)
        {
            // Сменили слот или открыли меню — поедание прервано, яблоко не тратится.
            if (!canUse || _inventory.SelectedItem != _eatingItem)
            {
                _eatingItem = null;
                return;
            }

            if (Time.time >= _eatEndTime)
                FinishEating();
            return;
        }

        if (canUse && _input != null && _input.SecondaryPressed)
            TryStartEating();
    }

    /// <summary>Остаток отката в секундах; 0, если предмет готов.</summary>
    public float GetCooldownRemaining(ItemDefinition item)
    {
        if (item == null || !_cooldownEnd.TryGetValue(item, out float end))
            return 0f;
        return Mathf.Max(0f, end - Time.time);
    }

    /// <summary>Доля оставшегося отката 0..1 (1 — только что запустили) — для заливки в слоте.</summary>
    public float GetCooldownFraction(ItemDefinition item)
    {
        if (item == null || item.CooldownSeconds <= 0f)
            return 0f;
        return Mathf.Clamp01(GetCooldownRemaining(item) / item.CooldownSeconds);
    }

    /// <summary>ПКМ по слоту в инвентаре: расходник применяется сразу, без времени поедания.</summary>
    public bool TryUseFromInventory(int slotIndex)
    {
        InventorySlot slot = _inventory.GetSlot(slotIndex);
        if (slot.IsEmpty || !slot.Item.IsConsumable || !CanUse(slot.Item))
            return false;

        Apply(slot.Item);
        slot.Count--;
        if (slot.Count <= 0)
            slot.Clear();
        _inventory.SetSlot(slotIndex, slot);
        return true;
    }

    void TryStartEating()
    {
        ItemDefinition item = _inventory.SelectedItem;
        if (item == null || !item.IsConsumable || !CanUse(item))
            return;

        _eatingItem = item;
        _eatEndTime = Time.time + item.UseDuration;
    }

    void FinishEating()
    {
        ItemDefinition item = _eatingItem;
        _eatingItem = null;
        if (_inventory.TryConsumeSelected(1))
            Apply(item);
    }

    bool CanUse(ItemDefinition item)
    {
        if (GetCooldownRemaining(item) > 0f)
            return false;
        if (item.HealAmount > 0f && _health.Health >= _health.MaxHealth)
        {
            GameMessages.Post("Already at full health");
            return false;
        }

        return true;
    }

    void Apply(ItemDefinition item)
    {
        if (item.HealAmount > 0f)
            _health.Heal(item.HealAmount);
        if (item.CooldownSeconds > 0f)
            _cooldownEnd[item] = Time.time + item.CooldownSeconds;
    }
}
