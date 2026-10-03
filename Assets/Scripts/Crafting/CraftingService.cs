using UnityEngine;

/// <summary>Resolves whether a recipe can be crafted and performs the swap.</summary>
public static class CraftingService
{
    public static int Owned(PlayerInventory inventory, ItemDefinition item)
    {
        return inventory != null && item != null ? inventory.CountItem(item) : 0;
    }

    public static int MaxCraftable(PlayerInventory inventory, CraftRecipe recipe)
    {
        if (inventory == null || recipe == null || recipe.Output == null)
            return 0;

        int max = 99;
        var ings = recipe.Ingredients;
        for (int i = 0; i < ings.Length; i++)
        {
            var ing = ings[i];
            if (ing.Item == null || ing.Count <= 0)
                continue;
            max = Mathf.Min(max, Owned(inventory, ing.Item) / ing.Count);
        }

        if (max <= 0)
            return 0;

        int fit = inventory.CountFreeSpaceFor(recipe.Output);
        int perCraft = recipe.OutputCount;
        if (perCraft <= 0)
            return 0;
        return Mathf.Min(max, fit / perCraft);
    }

    public static bool CanCraft(PlayerInventory inventory, CraftRecipe recipe, int times)
    {
        return times > 0 && MaxCraftable(inventory, recipe) >= times;
    }

    public static bool TryCraft(PlayerInventory inventory, CraftRecipe recipe, int times)
    {
        if (!CanCraft(inventory, recipe, times))
            return false;

        var ings = recipe.Ingredients;
        for (int i = 0; i < ings.Length; i++)
        {
            var ing = ings[i];
            if (ing.Item == null || ing.Count <= 0)
                continue;
            if (!inventory.TryConsumeItem(ing.Item, ing.Count * times))
                return false;
        }

        int made = recipe.OutputCount * times;
        int added = inventory.TryAddItem(recipe.Output, made);
        return added == made;
    }
}
