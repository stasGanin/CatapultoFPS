/// <summary>
/// Look-at target for E. The HUD prompt shows "[E] {InteractLabel}".
/// </summary>
public interface IPlayerInteractable
{
    /// <summary>Short action/target name for the prompt, e.g. "Open door".</summary>
    string InteractLabel { get; }
    bool CanInteract();
    void Interact(PlayerInventory inventory);
}
