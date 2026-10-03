using UnityEngine;

/// <summary>Люк под башней: E сажает игрока за пушку.</summary>
public sealed class TowerHatch : MonoBehaviour, IPlayerInteractable
{
    CastleTower _tower;

    public string InteractLabel => "Man the cannon";

    public void Bind(CastleTower tower) => _tower = tower;

    public bool CanInteract() => isActiveAndEnabled && _tower != null && !TowerOperator.IsOperating;

    public void Interact(PlayerInventory inventory)
    {
        if (inventory != null && _tower != null)
            TowerOperator.Begin(inventory, _tower);
    }
}
