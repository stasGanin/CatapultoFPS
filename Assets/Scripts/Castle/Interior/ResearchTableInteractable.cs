using UnityEngine;

/// <summary>E on the research table opens the research tree.</summary>
public sealed class ResearchTableInteractable : MonoBehaviour, IPlayerInteractable
{
    public string InteractLabel => "Research";

    public bool CanInteract() => isActiveAndEnabled;

    public void Interact(PlayerInventory inventory) => ResearchUI.Open();
}
