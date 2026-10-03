/// <summary>
/// Look-at target for E. Prompt is always "Press E to interact".
/// </summary>
public interface IPlayerInteractable
{
    bool CanInteract();
    void Interact(PlayerInventory inventory);
}
