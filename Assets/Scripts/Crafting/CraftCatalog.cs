using UnityEngine;

[CreateAssetMenu(menuName = "Catapulto/Crafting/Catalog", fileName = "CraftCatalog")]
public sealed class CraftCatalog : ScriptableObject
{
    [SerializeField] CraftRecipe[] _recipes;

    public CraftRecipe[] Recipes => _recipes ?? System.Array.Empty<CraftRecipe>();
}
