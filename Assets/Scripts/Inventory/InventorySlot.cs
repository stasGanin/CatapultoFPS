using System;
using UnityEngine;

/// <summary>
/// Одна ячейка инвентаря / hotbar.
/// </summary>
[Serializable]
public struct InventorySlot
{
    public ItemDefinition Item;
    public int Count;

    public bool IsEmpty => Item == null || Count <= 0;

    public void Clear()
    {
        Item = null;
        Count = 0;
    }

    public string ShortLabel
    {
        get
        {
            if (IsEmpty)
                return string.Empty;
            if (Count > 1)
                return $"{Item.DisplayName}\nx{Count}";
            return Item.DisplayName;
        }
    }
}
