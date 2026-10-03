using System;
using UnityEngine;

public enum CraftCategory
{
    All = 0,
    Materials = 1,
    Ammo = 2,
    Consumables = 3,
    Furniture = 4,
    Weapons = 5
}

[Serializable]
public struct CraftIngredient
{
    public ItemDefinition Item;
    public int Count;
}

[CreateAssetMenu(menuName = "Catapulto/Crafting/Recipe", fileName = "CraftRecipe")]
public sealed class CraftRecipe : ScriptableObject
{
    [SerializeField] string _id = "recipe";
    [SerializeField] string _displayName = "Recipe";
    [SerializeField, TextArea(2, 5)] string _description;
    [SerializeField] CraftCategory _category = CraftCategory.Materials;
    [SerializeField] ItemDefinition _output;
    [SerializeField, Min(1)] int _outputCount = 1;
    [SerializeField] CraftIngredient[] _ingredients;
    [Tooltip("Где крафтится: Hand — меню C, остальное — на поставленной станции.")]
    [SerializeField] CraftStation _station = CraftStation.Hand;
    [Tooltip("Закрыт, пока не изучен на изучалке или по выпавшему рецепту.")]
    [SerializeField] bool _requiresUnlock;

    public string Id => _id;
    public string DisplayName => string.IsNullOrEmpty(_displayName) && _output != null
        ? _output.DisplayName
        : _displayName;
    public string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(_description))
                return _description;
            return _output != null ? _output.Description : string.Empty;
        }
    }
    public CraftCategory Category => _category;
    public ItemDefinition Output => _output;
    public int OutputCount => Mathf.Max(1, _outputCount);
    public CraftIngredient[] Ingredients => _ingredients ?? Array.Empty<CraftIngredient>();
    public CraftStation Station => _station;
    public bool RequiresUnlock => _requiresUnlock;
}
