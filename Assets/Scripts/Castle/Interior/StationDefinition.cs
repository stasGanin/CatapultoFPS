using System;
using UnityEngine;

/// <summary>Recipe for an interior station: footprint on the 1 m floor grid and build cost.</summary>
[CreateAssetMenu(menuName = "Catapulto/Castle/Station Definition", fileName = "Station")]
public sealed class StationDefinition : ScriptableObject
{
    [SerializeField] string _id = "station";
    [SerializeField] string _displayName = "Station";
    [SerializeField] StationKind _kind = StationKind.Workbench;
    [Tooltip("Размер в клетках сетки пола (1 клетка = 1 м): x — ширина, y — глубина.")]
    [SerializeField] Vector2Int _footprint = new Vector2Int(2, 1);
    [SerializeField, Min(0.1f)] float _height = 1f;
    [SerializeField] Color _iconColor = new Color(0.45f, 0.32f, 0.2f);
    [SerializeField] CraftIngredient[] _cost = Array.Empty<CraftIngredient>();

    public string Id => _id;
    public string DisplayName => _displayName;
    public StationKind Kind => _kind;
    public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, _footprint.x), Mathf.Max(1, _footprint.y));
    public float Height => _height;
    public Color IconColor => _iconColor;
    public CraftIngredient[] Cost => _cost ?? Array.Empty<CraftIngredient>();

    public bool CanAfford(PlayerInventory inventory)
    {
        if (inventory == null)
            return false;
        foreach (var c in Cost)
        {
            if (c.Item != null && inventory.CountItem(c.Item) < c.Count)
                return false;
        }

        return true;
    }

    public bool TryPay(PlayerInventory inventory)
    {
        if (!CanAfford(inventory))
            return false;
        foreach (var c in Cost)
        {
            if (c.Item != null && c.Count > 0)
                inventory.TryConsumeItem(c.Item, c.Count);
        }

        return true;
    }

    /// <summary>Снос = переставить: возвращаем полную стоимость.</summary>
    public void Refund(PlayerInventory inventory)
    {
        foreach (var c in Cost)
        {
            if (c.Item != null && c.Count > 0)
                inventory.TryAddItem(c.Item, c.Count);
        }
    }

    public string CostLabel()
    {
        var parts = new System.Text.StringBuilder();
        foreach (var c in Cost)
        {
            if (c.Item == null)
                continue;
            if (parts.Length > 0)
                parts.Append('\n');
            parts.Append(c.Item.DisplayName).Append(' ').Append(c.Count);
        }

        return parts.ToString();
    }
}
