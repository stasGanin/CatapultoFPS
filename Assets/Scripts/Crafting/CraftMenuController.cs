using System;
using UnityEngine;

/// <summary>
/// C toggles craft. Esc / I / B close it. Mirrors inventory cursor lock.
/// </summary>
[DefaultExecutionOrder(-20)]
public sealed class CraftMenuController : MonoBehaviour
{
    public const string CatalogResource = "Crafting/CraftCatalog";

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] CraftCatalog _catalog;
    [SerializeField] CraftUI _ui;

    bool _open;

    public bool IsOpen => _open;
    public CraftCatalog Catalog => _catalog;
    public PlayerInventory Inventory => _inventory;

    public event Action<bool> OpenChanged;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_catalog == null)
            _catalog = Resources.Load<CraftCatalog>(CatalogResource);
        if (_ui == null)
            _ui = GetComponent<CraftUI>() ?? FindFirstObjectByType<CraftUI>();
    }

    void Start()
    {
        if (_ui == null)
            _ui = FindFirstObjectByType<CraftUI>();
        _ui?.Bind(this);
        ApplyCursor();
    }

    void Update()
    {
        if (_input == null)
            return;

        if (_input.CraftMenuPressed)
        {
            Toggle();
            return;
        }

        if (!_open)
            return;

        if (_input.CancelPressed || _input.BuildMenuPressed)
            Close();
    }

    public void Toggle()
    {
        if (_open)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (_open)
            return;

        _inventory?.CloseMenuAndStorage();
        OwnMageStation.CloseOpen();
        GetComponent<BuildingController>()?.CloseAll();
        GetComponent<CastleBuildController>()?.CloseAll();
        WorldMapUI.CloseIfOpen();
        SettingsMenuUI.CloseIfOpen();

        _open = true;
        ApplyCursor();
        OpenChanged?.Invoke(true);
        _inventory?.NotifyGameplayBlockChanged();
    }

    public void Close()
    {
        if (!_open)
            return;
        _open = false;
        ApplyCursor();
        OpenChanged?.Invoke(false);
        _inventory?.NotifyGameplayBlockChanged();
    }

    void ApplyCursor()
    {
        if (_open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (_inventory != null && (_inventory.IsMenuOpen || _inventory.IsMageOpen || _inventory.IsMapOpen || _inventory.IsSettingsOpen))
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        if (_open)
            Close();
    }
}
