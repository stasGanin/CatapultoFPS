using UnityEngine;

/// <summary>List of buildable pieces shown in the B menu.</summary>
[CreateAssetMenu(menuName = "Catapulto/Building/Catalog", fileName = "BuildingCatalog")]
public sealed class BuildingCatalog : ScriptableObject
{
    [SerializeField] BuildingDefinition[] _entries = System.Array.Empty<BuildingDefinition>();

    public BuildingDefinition[] Entries => _entries;
}
