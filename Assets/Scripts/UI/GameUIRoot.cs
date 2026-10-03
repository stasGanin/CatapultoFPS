using UnityEngine;

/// <summary>
/// Scene-side UI root. Owns editable canvases; binds to the Player at runtime.
/// Generate via menu: Catapulto / UI / Generate Prefabs And Place In Scene
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class GameUIRoot : MonoBehaviour
{
    [SerializeField] PlayerHealthHud _healthHud;
    [SerializeField] CrosshairUI _crosshair;
    [SerializeField] InventoryUI _inventoryUi;
    [SerializeField] BuildingMenuUI _buildingMenu;

    public PlayerHealthHud HealthHud => _healthHud;
    public CrosshairUI Crosshair => _crosshair;
    public InventoryUI InventoryUi => _inventoryUi;
    public BuildingMenuUI BuildingMenu => _buildingMenu;

    void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        var health = player.GetComponent<PlayerHealth>();
        var inventory = player.GetComponent<PlayerInventory>();
        var building = player.GetComponent<BuildingController>();

        if (_healthHud != null)
        {
            _healthHud.Bind(health);
            var mana = player.GetComponent<PlayerMana>();
            if (mana != null)
                _healthHud.BindMana(mana);
        }
        if (_crosshair != null)
            _crosshair.Bind(inventory, building);
        if (_inventoryUi != null)
            _inventoryUi.Bind(inventory);
        if (_buildingMenu != null)
            _buildingMenu.Bind(building, inventory);
    }

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || _healthHud == null)
            return;
        var mana = player.GetComponent<PlayerMana>();
        if (mana != null)
            _healthHud.BindMana(mana);
    }
}
