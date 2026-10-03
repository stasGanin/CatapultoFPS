using UnityEngine;

/// <summary>E on a workbench / smelter opens the craft menu with that station's recipes.</summary>
public sealed class CraftStationInteractable : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] CraftStation _station = CraftStation.Workbench;
    [SerializeField] string _label = "Workbench";

    public string InteractLabel => _label;

    public void Configure(CraftStation station, string label)
    {
        _station = station;
        _label = label;
    }

    public bool CanInteract() => isActiveAndEnabled;

    public void Interact(PlayerInventory inventory)
    {
        var menu = inventory != null ? inventory.GetComponent<CraftMenuController>() : null;
        if (menu == null)
        {
            Debug.LogError("CraftStationInteractable: player has no CraftMenuController.", this);
            return;
        }

        menu.OpenAt(_station);
    }
}
