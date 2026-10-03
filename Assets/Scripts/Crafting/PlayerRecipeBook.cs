using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Recipes the player has learned (research table or blueprint item).
/// Recipes that don't require unlocking are always known.
/// </summary>
public sealed class PlayerRecipeBook : MonoBehaviour
{
    readonly HashSet<string> _learned = new HashSet<string>();

    public event Action<CraftRecipe> Learned;

    public bool Knows(CraftRecipe recipe)
    {
        return recipe != null && (!recipe.RequiresUnlock || _learned.Contains(recipe.Id));
    }

    /// <summary>Returns false when the recipe was already known.</summary>
    public bool Learn(CraftRecipe recipe)
    {
        if (recipe == null || Knows(recipe))
            return false;
        _learned.Add(recipe.Id);
        Learned?.Invoke(recipe);
        return true;
    }
}
