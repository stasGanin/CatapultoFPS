using UnityEngine;

/// <summary>
/// Показывает viewmodel активного слота hotbar (пушка / кирка / арбалет / посох).
/// </summary>
public class EquipmentController : MonoBehaviour
{
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] GameObject _handCannonView;
    [SerializeField] CannonWeapon _cannonWeapon;
    [SerializeField] PickaxeTool _pickaxeTool;
    [SerializeField] CrossbowWeapon _crossbowWeapon;
    [SerializeField] StaffWeapon _staffWeapon;
    [SerializeField] ScattergunWeapon _scattergunWeapon;
    [SerializeField] EmberLauncherWeapon _emberLauncherWeapon;

    void Awake()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_cannonWeapon == null)
            _cannonWeapon = GetComponent<CannonWeapon>();
        if (_pickaxeTool == null)
            _pickaxeTool = GetComponent<PickaxeTool>();
        if (_crossbowWeapon == null)
            _crossbowWeapon = GetComponent<CrossbowWeapon>();
        if (_staffWeapon == null)
            _staffWeapon = GetComponent<StaffWeapon>();
        if (_scattergunWeapon == null)
            _scattergunWeapon = GetComponent<ScattergunWeapon>();
        if (_emberLauncherWeapon == null)
            _emberLauncherWeapon = GetComponent<EmberLauncherWeapon>();
    }

    public void BindCrossbow(CrossbowWeapon crossbow)
    {
        _crossbowWeapon = crossbow;
        Refresh();
    }

    public void BindStaff(StaffWeapon staff)
    {
        _staffWeapon = staff;
        Refresh();
    }

    public void BindScattergun(ScattergunWeapon scattergun)
    {
        _scattergunWeapon = scattergun;
        Refresh();
    }

    public void BindEmberLauncher(EmberLauncherWeapon launcher)
    {
        _emberLauncherWeapon = launcher;
        Refresh();
    }

    void OnEnable()
    {
        if (_inventory == null)
            return;
        _inventory.SelectionChanged += Refresh;
        _inventory.Changed += Refresh;
        _inventory.MenuOpenChanged += OnMenuChanged;
    }

    void OnDisable()
    {
        if (_inventory == null)
            return;
        _inventory.SelectionChanged -= Refresh;
        _inventory.Changed -= Refresh;
        _inventory.MenuOpenChanged -= OnMenuChanged;
    }

    void Start() => Refresh();

    void LateUpdate()
    {
        // Castle build mode toggles without inventory events — keep weapons in sync
        bool canUse = _inventory == null || !_inventory.BlocksGameplayInput;
        bool cannonOff = _cannonWeapon != null && _cannonWeapon.enabled != (SelectedIsCannon() && canUse);
        bool crossbowOff = _crossbowWeapon != null && _crossbowWeapon.enabled != (SelectedIsCrossbow() && canUse);
        bool staffOff = _staffWeapon != null && _staffWeapon.enabled != (SelectedIsStaff() && canUse);
        bool scatterOff = _scattergunWeapon != null && _scattergunWeapon.enabled != (SelectedIsScattergun() && canUse);
        bool emberOff = _emberLauncherWeapon != null && _emberLauncherWeapon.enabled != (SelectedIsEmberLauncher() && canUse);
        if (cannonOff || crossbowOff || staffOff || scatterOff || emberOff)
            Refresh();
    }

    bool SelectedIsCannon() => _inventory != null && _inventory.SelectedKind == ItemKind.HandCannon;
    bool SelectedIsCrossbow() => _inventory != null && _inventory.SelectedKind == ItemKind.Crossbow;
    bool SelectedIsStaff() => _inventory != null && _inventory.SelectedKind == ItemKind.Staff;
    bool SelectedIsScattergun() => _inventory != null && _inventory.SelectedKind == ItemKind.Scattergun;
    bool SelectedIsEmberLauncher() => _inventory != null && _inventory.SelectedKind == ItemKind.EmberLauncher;

    void OnMenuChanged(bool _) => Refresh();

    void Refresh()
    {
        ItemKind kind = _inventory != null && !TowerOperator.IsOperating ? _inventory.SelectedKind : ItemKind.None;
        bool canUse = _inventory == null || !_inventory.BlocksGameplayInput;

        bool showCannon = kind == ItemKind.HandCannon;
        bool showPickaxe = kind == ItemKind.Pickaxe;
        bool showCrossbow = kind == ItemKind.Crossbow;
        bool showStaff = kind == ItemKind.Staff;
        bool showScatter = kind == ItemKind.Scattergun;
        bool showEmber = kind == ItemKind.EmberLauncher;

        GameObject cannonView = _cannonWeapon != null ? _cannonWeapon.ViewModelObject : _handCannonView;
        if (cannonView == null)
            cannonView = _handCannonView;
        if (cannonView != null)
            cannonView.SetActive(showCannon);
        if (_handCannonView != null && _handCannonView != cannonView)
            _handCannonView.SetActive(false);

        GameObject pickaxeView = _pickaxeTool != null ? _pickaxeTool.ViewModelObject : null;
        if (pickaxeView != null)
            pickaxeView.SetActive(showPickaxe);

        GameObject crossbowView = _crossbowWeapon != null ? _crossbowWeapon.ViewModelObject : null;
        if (crossbowView != null)
            crossbowView.SetActive(showCrossbow);

        if (_cannonWeapon != null)
            _cannonWeapon.enabled = showCannon && canUse;

        if (_crossbowWeapon != null)
            _crossbowWeapon.enabled = showCrossbow && canUse;

        GameObject staffView = _staffWeapon != null ? _staffWeapon.ViewModelObject : null;
        if (staffView != null)
            staffView.SetActive(showStaff);
        if (_staffWeapon != null)
            _staffWeapon.enabled = showStaff && canUse;

        GameObject scatterView = _scattergunWeapon != null ? _scattergunWeapon.ViewModelObject : null;
        if (scatterView != null)
            scatterView.SetActive(showScatter);
        if (_scattergunWeapon != null)
            _scattergunWeapon.enabled = showScatter && canUse;

        GameObject emberView = _emberLauncherWeapon != null ? _emberLauncherWeapon.ViewModelObject : null;
        if (emberView != null)
            emberView.SetActive(showEmber);
        if (_emberLauncherWeapon != null)
            _emberLauncherWeapon.enabled = showEmber && canUse;

        // Не выключаем компонент — иначе легко «убить» Update. Только флаг экипировки.
        if (_pickaxeTool != null)
        {
            _pickaxeTool.enabled = true;
            _pickaxeTool.SetEquipped(showPickaxe && canUse);
        }
    }
}
