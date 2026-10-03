using UnityEngine;

/// <summary>
/// Wires player HP, crossbow (slot 3), and an enemy spawner cube next to the castle.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class EnemyCombatBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;
        if (player.GetComponent<EnemyCombatBootstrap>() == null)
            player.AddComponent<EnemyCombatBootstrap>();
    }

    void Awake()
    {
        EnsurePlayerCombat();
        EnsureSpawner();
    }

    void EnsurePlayerCombat()
    {
        if (GetComponent<PlayerHealth>() == null)
            gameObject.AddComponent<PlayerHealth>();
        if (GetComponent<ConsumableUser>() == null)
            gameObject.AddComponent<ConsumableUser>();
        if (GetComponent<ConsumableHandView>() == null)
            gameObject.AddComponent<ConsumableHandView>();
        if (GetComponent<PlayerRespawn>() == null)
            gameObject.AddComponent<PlayerRespawn>();
        if (GetComponent<PlayerMana>() == null)
            gameObject.AddComponent<PlayerMana>();
        // HP label lives on scene GameUI (Catapulto/UI/Generate…). Fallback only if none exists.
        if (FindFirstObjectByType<PlayerHealthHud>() == null)
            gameObject.AddComponent<PlayerHealthHud>();

        var crossbow = GetComponent<CrossbowWeapon>();
        if (crossbow == null)
            crossbow = gameObject.AddComponent<CrossbowWeapon>();

        var config = Resources.Load<WeaponConfig>("Weapons/CrossbowConfig");
        if (config != null)
            crossbow.SetConfig(config);

        var staff = GetComponent<StaffWeapon>();
        if (staff == null)
            staff = gameObject.AddComponent<StaffWeapon>();
        var staffConfig = Resources.Load<StaffConfig>("Weapons/StaffConfig");
        if (staffConfig != null)
            staff.SetConfig(staffConfig);

        var scattergun = GetComponent<ScattergunWeapon>();
        if (scattergun == null)
            scattergun = gameObject.AddComponent<ScattergunWeapon>();
        var ember = GetComponent<EmberLauncherWeapon>();
        if (ember == null)
            ember = gameObject.AddComponent<EmberLauncherWeapon>();

        var inventory = GetComponent<PlayerInventory>();
        var crossbowItem = Resources.Load<ItemDefinition>("Items/CrossbowItem");
        if (inventory != null && crossbowItem != null)
            inventory.SetHotbarItem(2, crossbowItem, 1);
        var staffItem = Resources.Load<ItemDefinition>("Items/StaffItem");
        if (inventory != null && staffItem != null)
            inventory.SetHotbarItem(3, staffItem, 1);

        var appleItem = Resources.Load<ItemDefinition>("Items/AppleItem");
        if (inventory != null && appleItem != null)
            inventory.SetHotbarItem(4, appleItem, 10);

        var equipment = GetComponent<EquipmentController>();
        equipment?.BindCrossbow(crossbow);
        equipment?.BindStaff(staff);
        equipment?.BindScattergun(scattergun);
        equipment?.BindEmberLauncher(ember);
    }

    void EnsureSpawner()
    {
        // Prefer triangular enemy castle (spawned by CastleBootstrap). Skip legacy cube.
        if (FindFirstObjectByType<EnemyCastle>() != null)
            return;
        if (FindFirstObjectByType<EnemySpawnerModule>() != null)
            return;
        // CastleBootstrap runs at -40 / Start; we only spawn cube if no map pads exist
    }
}
