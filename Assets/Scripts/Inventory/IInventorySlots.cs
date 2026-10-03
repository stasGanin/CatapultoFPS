using System;

/// <summary>Common slot storage for player inventory and chests.</summary>
public interface IInventorySlots
{
    int SlotCount { get; }
    InventorySlot GetSlot(int index);
    void SetSlot(int index, InventorySlot slot);
    event Action Changed;
}
