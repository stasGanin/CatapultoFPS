using System;
using UnityEngine;

/// <summary>One node of the research tree: pay resources once, learn its recipes forever.</summary>
[CreateAssetMenu(menuName = "Catapulto/Crafting/Research Node", fileName = "ResearchNode")]
public sealed class ResearchNode : ScriptableObject
{
    [SerializeField] string _id = "node";
    [SerializeField] string _displayName = "Research";
    [SerializeField, TextArea(2, 4)] string _description;
    [Tooltip("Колонка в дереве: 0 — первый ряд исследований, дальше — глубже.")]
    [SerializeField, Min(0)] int _tier;
    [SerializeField] ResearchNode[] _prerequisites = Array.Empty<ResearchNode>();
    [SerializeField] CraftRecipe[] _unlocks = Array.Empty<CraftRecipe>();
    [SerializeField] CraftIngredient[] _cost = Array.Empty<CraftIngredient>();

    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description ?? string.Empty;
    public int Tier => _tier;
    public ResearchNode[] Prerequisites => _prerequisites ?? Array.Empty<ResearchNode>();
    public CraftRecipe[] Unlocks => _unlocks ?? Array.Empty<CraftRecipe>();
    public CraftIngredient[] Cost => _cost ?? Array.Empty<CraftIngredient>();

    /// <summary>Узел изучен, когда известны все его рецепты (в т.ч. если часть пришла из выпавшего рецепта).</summary>
    public bool IsResearched(PlayerRecipeBook book)
    {
        if (book == null)
            return false;
        foreach (var r in Unlocks)
        {
            if (r != null && !book.Knows(r))
                return false;
        }

        return true;
    }

    public bool PrerequisitesMet(PlayerRecipeBook book)
    {
        foreach (var p in Prerequisites)
        {
            if (p != null && !p.IsResearched(book))
                return false;
        }

        return true;
    }

    public bool CanAfford(PlayerInventory inventory)
    {
        foreach (var c in Cost)
        {
            if (c.Item != null && (inventory == null || inventory.CountItem(c.Item) < c.Count))
                return false;
        }

        return true;
    }

    public bool TryResearch(PlayerInventory inventory, PlayerRecipeBook book)
    {
        if (IsResearched(book) || !PrerequisitesMet(book) || !CanAfford(inventory))
            return false;
        foreach (var c in Cost)
        {
            if (c.Item != null && c.Count > 0)
                inventory.TryConsumeItem(c.Item, c.Count);
        }

        foreach (var r in Unlocks)
            book.Learn(r);
        return true;
    }
}
