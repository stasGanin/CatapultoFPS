using System;
using UnityEngine;

/// <summary>
/// Castle storage module / chest. Fixed slot grid, opened with E when nearby.
/// </summary>
[DisallowMultipleComponent]
public sealed class StorageContainer : MonoBehaviour, IInventorySlots, ICastleModuleBehavior, IPlayerInteractable
{
    public const int DefaultSlotCount = 36;

    [SerializeField] int _slotCount = DefaultSlotCount;
    [SerializeField] float _interactRange = 3.2f;
    [SerializeField] string _displayName = "Storage";

    InventorySlot[] _slots;
    bool _destroyed;

    public event Action Changed;

    public int SlotCount => _slots != null ? _slots.Length : 0;
    public string DisplayName => _displayName;
    public float InteractRange => _interactRange;
    public bool IsOperational => !_destroyed;
    public bool IsDestroyed => _destroyed;

    public void Configure(string displayName, int slotCount, float interactRange)
    {
        _displayName = string.IsNullOrEmpty(displayName) ? "Storage" : displayName;
        _slotCount = Mathf.Max(1, slotCount);
        _interactRange = Mathf.Max(0.5f, interactRange);
        EnsureSlots();
    }

    void Awake()
    {
        EnsureSlots();
    }

    void EnsureSlots()
    {
        int n = Mathf.Max(1, _slotCount);
        if (_slots == null || _slots.Length != n)
            _slots = new InventorySlot[n];
    }

    public InventorySlot GetSlot(int index)
    {
        EnsureSlots();
        if (index < 0 || index >= _slots.Length)
            return default;
        return _slots[index];
    }

    public void SetSlot(int index, InventorySlot slot)
    {
        EnsureSlots();
        if (index < 0 || index >= _slots.Length)
            return;
        _slots[index] = slot;
        Changed?.Invoke();
    }

    public void OnModuleDestroyed()
    {
        _destroyed = true;
        // Drop contents into the world
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
                continue;
            WorldLootPickup.Spawn(_slots[i].Item, _slots[i].Count, transform.position + Vector3.up * 0.5f, 1.2f);
            _slots[i] = default;
        }

        Changed?.Invoke();
    }

    public bool IsInRange(Vector3 worldPos)
    {
        float r = Mathf.Max(0.5f, _interactRange);
        return (transform.position - worldPos).sqrMagnitude <= r * r;
    }

    public bool CanInteract() => !_destroyed;

    public void Interact(PlayerInventory inventory)
    {
        if (inventory == null || _destroyed)
            return;
        if (inventory.OpenStorage == this)
            inventory.CloseMenuAndStorage();
        else
            inventory.TryOpenStorage(this);
    }

    public int TryAddItem(ItemDefinition item, int count)
    {
        EnsureSlots();
        if (item == null || count <= 0)
            return 0;

        int remaining = count;
        int maxStack = item.MaxStack;

        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty || _slots[i].Item != item)
                continue;
            if (_slots[i].Count >= maxStack)
                continue;
            int add = Mathf.Min(maxStack - _slots[i].Count, remaining);
            _slots[i].Count += add;
            remaining -= add;
        }

        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty)
                continue;
            int add = Mathf.Min(maxStack, remaining);
            _slots[i] = new InventorySlot { Item = item, Count = add };
            remaining -= add;
        }

        int added = count - remaining;
        if (added > 0)
            Changed?.Invoke();
        return added;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.75f, 0.65f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _interactRange);
    }
#endif
}
