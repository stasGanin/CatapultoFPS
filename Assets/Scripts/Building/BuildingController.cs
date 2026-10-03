using UnityEngine;
using UnityEngine.EventSystems;

public enum BuildingMode
{
    Closed = 0,
    Menu = 1,
    Placing = 2
}

/// <summary>
/// Building state machine: B opens menu → pick piece → ghost place.
/// RMB while placing returns to menu; Esc closes; I closes build and opens inventory.
/// </summary>
public sealed class BuildingController : MonoBehaviour
{
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] BuildingCatalog _catalog;
    [SerializeField] ItemDefinition _stoneItem;
    [SerializeField] BuildingMenuUI _menuUi;

    BuildingMode _mode = BuildingMode.Closed;
    BuildingDefinition _selected;
    BuildingPlacer _placer;

    public BuildingMode Mode => _mode;
    public BuildingCatalog Catalog => _catalog;
    public bool IsMenuOpen => _mode == BuildingMode.Menu;
    public bool IsPlacing => _mode == BuildingMode.Placing;
    public bool BlocksLook => _mode == BuildingMode.Menu;
    public bool BlocksWeapons => _mode != BuildingMode.Closed;

    public event System.Action<BuildingMode> ModeChanged;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_menuUi == null)
            _menuUi = GetComponent<BuildingMenuUI>() ?? FindFirstObjectByType<BuildingMenuUI>();
        if (_catalog == null)
            _catalog = Resources.Load<BuildingCatalog>("Building/BuildingCatalog");
        if (_stoneItem == null)
            _stoneItem = Resources.Load<ItemDefinition>("Items/StoneItem");

        if (_catalog == null)
            Debug.LogError("BuildingController: BuildingCatalog not found at Resources/Building/BuildingCatalog", this);
        if (_menuUi == null)
            Debug.LogError("BuildingController: BuildingMenuUI missing (generate GameUI via Catapulto/UI menu)", this);
    }

    BuildingMenuUI MenuUi
    {
        get
        {
            if (_menuUi == null)
                _menuUi = GetComponent<BuildingMenuUI>() ?? FindFirstObjectByType<BuildingMenuUI>();
            return _menuUi;
        }
    }

    void OnDestroy()
    {
        _placer?.Dispose();
        _placer = null;
    }

    void Update()
    {
        if (_input == null)
            return;

        HandleGlobalHotkeys();

        if (_mode == BuildingMode.Placing)
            TickPlacing();
    }

    void HandleGlobalHotkeys()
    {
        // Escape always closes any open UI
        if (_input.CancelPressed)
        {
            if (_mode != BuildingMode.Closed)
                CloseAll();
            else if (_inventory != null && _inventory.IsMenuOpen)
                _inventory.SetMenuOpen(false);
            return;
        }

        // I: if building UI open → close it and open inventory
        if (_input.InventoryTogglePressed)
        {
            if (_mode != BuildingMode.Closed)
            {
                CloseAll();
                _inventory?.SetMenuOpen(true);
                return;
            }
            // else PlayerInventory handles toggle
        }

        // B toggles / opens building menu
        if (_input.BuildMenuPressed)
        {
            // На паузе стройка недоступна; открытая карта уступает место меню стройки.
            if (SettingsMenuUI.IsOpen)
                return;
            WorldMapUI.CloseIfOpen();
            if (_mode == BuildingMode.Closed)
                OpenMenu();
            else if (_mode == BuildingMode.Menu)
                CloseAll();
            else if (_mode == BuildingMode.Placing)
                OpenMenu();
            return;
        }

        // RMB in menu closes build menu
        if (_mode == BuildingMode.Menu && _input.SecondaryPressed)
            CloseAll();
    }

    void TickPlacing()
    {
        if (_placer == null)
            return;

        if (_input.RotateBuildingPressed)
            _placer.Rotate(1);
        if (_input.RotateBuildingScroll != 0)
            _placer.Rotate(_input.RotateBuildingScroll > 0 ? 1 : -1);

        // RMB → back to building menu
        if (_input.SecondaryPressed)
        {
            OpenMenu();
            return;
        }

        // Don't place through UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        _placer.Tick();

        if (_input.AttackPressed && _placer.IsValid)
            _placer.TryPlace(_inventory, _stoneItem);
    }

    public void OpenMenu()
    {
        _placer?.Dispose();
        _placer = null;
        _selected = null;

        _inventory?.SetMenuOpen(false);
        GetComponent<CraftMenuController>()?.Close();
        OwnMageStation.CloseOpen();
        SetMode(BuildingMode.Menu);
        var ui = MenuUi;
        if (ui == null)
        {
            Debug.LogError("BuildingController.OpenMenu: no BuildingMenuUI", this);
            return;
        }
        ui.Show(true);
        ApplyCursor();
    }

    public void CloseAll()
    {
        _placer?.Dispose();
        _placer = null;
        _selected = null;
        MenuUi?.Show(false);
        SetMode(BuildingMode.Closed);
        ApplyCursor();
    }

    public void SelectDefinition(BuildingDefinition definition)
    {
        if (definition == null)
            return;

        _selected = definition;
        MenuUi?.Show(false);

        _placer?.Dispose();
        _placer = new BuildingPlacer(_camera, definition);
        _placer.SetVisible(true);
        SetMode(BuildingMode.Placing);
        ApplyCursor();
    }

    void SetMode(BuildingMode mode)
    {
        if (_mode == mode)
            return;
        _mode = mode;
        ModeChanged?.Invoke(_mode);
        _inventory?.NotifyGameplayBlockChanged();
    }

    void ApplyCursor()
    {
        if (_mode == BuildingMode.Menu || (_inventory != null && _inventory.IsMenuOpen))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
