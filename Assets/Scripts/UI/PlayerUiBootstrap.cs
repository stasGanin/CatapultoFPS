using UnityEngine;

/// <summary>
/// Ensures hotbar / inventory / crosshair / HP UI exist after map regen wiped GameUI.
/// </summary>
[DefaultExecutionOrder(-90)]
public sealed class PlayerUiBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        if (player.GetComponent<PlayerUiBootstrap>() == null)
            player.AddComponent<PlayerUiBootstrap>();
        if (player.GetComponent<PlayerStorageInteractor>() == null)
            player.AddComponent<PlayerStorageInteractor>();
        if (player.GetComponent<PlayerMana>() == null)
            player.AddComponent<PlayerMana>();
        if (player.GetComponent<CraftMenuController>() == null)
            player.AddComponent<CraftMenuController>();
        if (player.GetComponent<FurniturePlacer>() == null)
            player.AddComponent<FurniturePlacer>();
        if (player.GetComponent<RepairKitTool>() == null)
            player.AddComponent<RepairKitTool>();
    }

    void Awake()
    {
        if (GetComponent<PlayerStorageInteractor>() == null)
            gameObject.AddComponent<PlayerStorageInteractor>();
        if (GetComponent<PlayerMana>() == null)
            gameObject.AddComponent<PlayerMana>();
        if (GetComponent<CraftMenuController>() == null)
            gameObject.AddComponent<CraftMenuController>();
        if (GetComponent<FurniturePlacer>() == null)
            gameObject.AddComponent<FurniturePlacer>();
        if (GetComponent<RepairKitTool>() == null)
            gameObject.AddComponent<RepairKitTool>();
        if (GetComponent<CraftUI>() == null && FindFirstObjectByType<CraftUI>() == null)
            gameObject.AddComponent<CraftUI>();
        if (GetComponent<MageUI>() == null && FindFirstObjectByType<MageUI>() == null)
            gameObject.AddComponent<MageUI>();
        if (GetComponent<InteractPromptUI>() == null && FindFirstObjectByType<InteractPromptUI>() == null)
            gameObject.AddComponent<InteractPromptUI>();
        if (GetComponent<WorldMapUI>() == null)
            gameObject.AddComponent<WorldMapUI>();
        if (GetComponent<SettingsMenuUI>() == null)
            gameObject.AddComponent<SettingsMenuUI>();
        GameSettings.EnsureLoaded();

        // Prefer scene GameUI if present; otherwise rebuild on the player (runtime fallbacks).
        bool hasGameUi = FindFirstObjectByType<GameUIRoot>() != null;

        if (!hasGameUi)
        {
            if (GetComponent<InventoryUI>() == null)
                gameObject.AddComponent<InventoryUI>();
            if (GetComponent<CrosshairUI>() == null)
                gameObject.AddComponent<CrosshairUI>();
            if (GetComponent<PlayerHealthHud>() == null)
                gameObject.AddComponent<PlayerHealthHud>();
        }
        else
        {
            // GameUI exists but player may still need binders if stripped
            if (GetComponent<InventoryUI>() == null && FindFirstObjectByType<InventoryUI>() == null)
                gameObject.AddComponent<InventoryUI>();
            if (GetComponent<CrosshairUI>() == null && FindFirstObjectByType<CrosshairUI>() == null)
                gameObject.AddComponent<CrosshairUI>();
            if (GetComponent<PlayerHealthHud>() == null && FindFirstObjectByType<PlayerHealthHud>() == null)
                gameObject.AddComponent<PlayerHealthHud>();
        }
    }
}
