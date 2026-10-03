using UnityEngine;

/// <summary>Swap / move helpers between any two slot hosts.</summary>
public static class InventoryTransfer
{
    public static void Swap(IInventorySlots a, int ai, IInventorySlots b, int bi)
    {
        if (a == null || b == null)
            return;
        if (ai < 0 || ai >= a.SlotCount || bi < 0 || bi >= b.SlotCount)
            return;
        if (ReferenceEquals(a, b) && ai == bi)
            return;

        InventorySlot sa = a.GetSlot(ai);
        InventorySlot sb = b.GetSlot(bi);

        if (!sa.IsEmpty && !sb.IsEmpty && Same(sa.Item, sb.Item) && sb.Count < sb.Item.MaxStack)
        {
            int space = sb.Item.MaxStack - sb.Count;
            int move = Mathf.Min(space, sa.Count);
            sb.Count += move;
            sa.Count -= move;
            if (sa.Count <= 0)
                sa = default;
            a.SetSlot(ai, sa);
            b.SetSlot(bi, sb);
            return;
        }

        a.SetSlot(ai, sb);
        b.SetSlot(bi, sa);
    }

    /// <summary>
    /// Move as much as possible from source slot into target container (stack then empty cells).
    /// Returns true if anything moved.
    /// </summary>
    public static bool TryDeposit(IInventorySlots from, int fromIndex, IInventorySlots to, int toStart = 0, int toCount = -1)
    {
        if (from == null || to == null)
            return false;
        if (fromIndex < 0 || fromIndex >= from.SlotCount)
            return false;

        InventorySlot src = from.GetSlot(fromIndex);
        if (src.IsEmpty)
            return false;

        int end = toCount < 0 ? to.SlotCount : Mathf.Min(to.SlotCount, toStart + toCount);
        toStart = Mathf.Clamp(toStart, 0, to.SlotCount);
        bool moved = false;

        // Fill existing stacks
        for (int i = toStart; i < end && !src.IsEmpty; i++)
        {
            InventorySlot dst = to.GetSlot(i);
            if (dst.IsEmpty || !Same(dst.Item, src.Item) || dst.Count >= dst.Item.MaxStack)
                continue;

            int space = dst.Item.MaxStack - dst.Count;
            int add = Mathf.Min(space, src.Count);
            dst.Count += add;
            src.Count -= add;
            to.SetSlot(i, dst);
            moved = true;
            if (src.Count <= 0)
                src = default;
        }

        // Empty cells
        for (int i = toStart; i < end && !src.IsEmpty; i++)
        {
            InventorySlot dst = to.GetSlot(i);
            if (!dst.IsEmpty)
                continue;

            int add = Mathf.Min(src.Item.MaxStack, src.Count);
            to.SetSlot(i, new InventorySlot { Item = src.Item, Count = add });
            src.Count -= add;
            moved = true;
            if (src.Count <= 0)
                src = default;
        }

        from.SetSlot(fromIndex, src);
        return moved;
    }

    /// <summary>Pull first non-empty stack from range into an empty destination slot.</summary>
    public static bool TryWithdrawFirst(IInventorySlots from, int fromStart, int fromCount, IInventorySlots to, int toIndex)
    {
        if (from == null || to == null)
            return false;
        if (toIndex < 0 || toIndex >= to.SlotCount)
            return false;
        if (!to.GetSlot(toIndex).IsEmpty)
            return false;

        int end = fromCount < 0 ? from.SlotCount : Mathf.Min(from.SlotCount, fromStart + fromCount);
        fromStart = Mathf.Clamp(fromStart, 0, from.SlotCount);

        for (int i = fromStart; i < end; i++)
        {
            InventorySlot src = from.GetSlot(i);
            if (src.IsEmpty)
                continue;

            to.SetSlot(toIndex, src);
            from.SetSlot(i, default);
            return true;
        }

        return false;
    }

    public static bool Same(ItemDefinition x, ItemDefinition y)
    {
        if (x == null || y == null)
            return false;
        if (ReferenceEquals(x, y))
            return true;
        return x.Id == y.Id;
    }
}
