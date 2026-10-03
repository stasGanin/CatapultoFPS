using System;
using UnityEngine;

/// <summary>
/// Hotbar (9) + bag (27). Implements IInventorySlots for chest transfers.
/// </summary>
public class PlayerInventory : MonoBehaviour, IInventorySlots
{
    public const int HotbarSize = 9;
    public const int BagSize = 27;
    public const int TotalSize = HotbarSize + BagSize;

    [SerializeField] PlayerInputReader _input;
    [SerializeField] ItemDefinition _startingCannon;
    [SerializeField] ItemDefinition _startingPickaxe;
    [SerializeField] PlayerLoadoutConfig _loadout;

    readonly InventorySlot[] _slots = new InventorySlot[TotalSize];
    int _selectedHotbarIndex;
    bool _menuOpen;
    BuildingController _building;
    CastleBuildController _castleBuild;
    FurniturePlacer _furniture;
    StorageContainer _openStorage;

    public event Action Changed;
    public event Action SelectionChanged;
    public event Action<bool> MenuOpenChanged;
    public event Action StorageOpenChanged;

    public int SlotCount => TotalSize;
    public int SelectedHotbarIndex => _selectedHotbarIndex;
    public bool IsMenuOpen => _menuOpen;
    public StorageContainer OpenStorage => _openStorage;
    public bool IsStorageOpen => _openStorage != null && !_openStorage.IsDestroyed;

    public bool IsCraftOpen => CraftMenu != null && CraftMenu.IsOpen;
    public bool IsMageOpen => OwnMageStation.OpenStation != null;

    public bool IsFurniturePlacing => Furniture != null && Furniture.IsPlacing;

    public bool BlocksGameplayInput =>
        IsDead
        || TowerOperator.IsOperating
        || ResearchUI.IsOpen
        || _menuOpen
        || IsCraftOpen
        || IsMageOpen
        || IsFurniturePlacing
        || IsMapOpen
        || IsSettingsOpen
        || (_building != null && _building.enabled && _building.BlocksWeapons)
        || (CastleBuild != null && CastleBuild.BlocksWeapons);
    public bool BlocksLook =>
        ResearchUI.IsOpen
        || _menuOpen
        || IsCraftOpen
        || IsMageOpen
        || IsMapOpen
        || IsSettingsOpen
        || (_building != null && _building.enabled && _building.BlocksLook)
        || (CastleBuild != null && CastleBuild.BlocksLook);

    public bool IsMapOpen => WorldMapUI.IsOpen;
    // PlayerHealth добавляется бутстрапом позже Awake инвентаря — ищем лениво.
    PlayerHealth _health;
    bool IsDead
    {
        get
        {
            if (_health == null)
                _health = GetComponent<PlayerHealth>();
            return _health != null && _health.IsDead;
        }
    }
    public bool IsSettingsOpen => SettingsMenuUI.IsOpen;

    CraftMenuController _craftMenu;
    CraftMenuController CraftMenu
    {
        get
        {
            if (_craftMenu == null)
                _craftMenu = GetComponent<CraftMenuController>();
            return _craftMenu;
        }
    }

    CastleBuildController CastleBuild
    {
        get
        {
            if (_castleBuild == null)
                _castleBuild = GetComponent<CastleBuildController>();
            return _castleBuild;
        }
    }

    FurniturePlacer Furniture
    {
        get
        {
            if (_furniture == null)
                _furniture = GetComponent<FurniturePlacer>();
            return _furniture;
        }
    }

    public InventorySlot GetSlot(int index) =>
        index >= 0 && index < TotalSize ? _slots[index] : default;

    public void SetSlot(int index, InventorySlot slot)
    {
        if (index < 0 || index >= TotalSize)
            return;
        _slots[index] = slot;
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    /// <summary>ПКМ по предмету в инвентаре. Пока «использовать» умеют только рецепты (Blueprint).</summary>
    public bool TryUseSlot(int index)
    {
        InventorySlot slot = GetSlot(index);
        if (slot.IsEmpty || slot.Item.Kind != ItemKind.Blueprint)
            return false;

        CraftRecipe recipe = slot.Item.TeachesRecipe;
        var book = GetComponent<PlayerRecipeBook>();
        if (recipe == null || book == null)
        {
            Debug.LogError($"PlayerInventory: blueprint '{slot.Item.DisplayName}' has no recipe or player has no PlayerRecipeBook.", this);
            return false;
        }

        if (!book.Learn(recipe))
        {
            GameMessages.Post($"Already known: {recipe.DisplayName}");
            return false;
        }

        slot.Count--;
        if (slot.Count <= 0)
            slot.Clear();
        SetSlot(index, slot);
        GameMessages.Post($"Recipe learned: {recipe.DisplayName}");
        return true;
    }

    public ItemDefinition SelectedItem
    {
        get
        {
            InventorySlot slot = _slots[_selectedHotbarIndex];
            return slot.IsEmpty ? null : slot.Item;
        }
    }

    public ItemKind SelectedKind => SelectedItem != null ? SelectedItem.Kind : ItemKind.None;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        _building = GetComponent<BuildingController>();
        _castleBuild = GetComponent<CastleBuildController>();

        if (_startingCannon != null)
            _slots[0] = new InventorySlot { Item = _startingCannon, Count = 1 };
        if (_startingPickaxe != null)
            _slots[1] = new InventorySlot { Item = _startingPickaxe, Count = 1 };

        GiveStartingLoadout();

        _selectedHotbarIndex = 0;
    }

    /// <summary>
    /// Стартовый набор кладём только в сумку: хотбар позже заполняет EnemyCombatBootstrap
    /// (арбалет, посох) через SetHotbarItem, и всё, что лежало там, перезаписывалось.
    /// </summary>
    void GiveStartingLoadout()
    {
        if (_loadout == null)
        {
            Debug.LogError("PlayerInventory: no PlayerLoadoutConfig assigned — player starts with an empty bag.", this);
            return;
        }

        foreach (var entry in _loadout.BagItems)
        {
            int added = AddToBag(entry.Item, entry.Count);
            if (entry.Item != null && added < entry.Count)
                Debug.LogWarning($"PlayerInventory: bag full, only {added}/{entry.Count} {entry.Item.DisplayName} given.", this);
        }
    }

    int AddToBag(ItemDefinition item, int count)
    {
        if (item == null || count <= 0)
            return 0;

        int remaining = count;
        for (int i = HotbarSize; i < TotalSize && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty)
                continue;
            int add = Mathf.Min(item.MaxStack, remaining);
            _slots[i] = new InventorySlot { Item = item, Count = add };
            remaining -= add;
        }

        return count - remaining;
    }

    void Update()
    {
        if (_input == null)
            return;

        if (_input.InventoryTogglePressed)
        {
            if (CastleBuild != null && CastleBuild.Mode != CastleBuildMode.Closed)
                return;
            if (_building == null || !_building.enabled || _building.Mode == BuildingMode.Closed)
            {
                CraftMenu?.Close();
                OwnMageStation.CloseOpen();
                if (_menuOpen)
                    CloseMenuAndStorage();
                else
                    SetMenuOpen(true);
            }
        }

        if (_input.CancelPressed && _menuOpen)
            CloseMenuAndStorage();

        // Цифры не листают хотбар под открытым меню/паузой.
        if (_input.HotbarSlotPressed >= 0 && !BlocksLook)
            SelectHotbar(_input.HotbarSlotPressed);

        if (_input.HotbarScrollDelta != 0 && !_menuOpen)
        {
            int next = _selectedHotbarIndex - _input.HotbarScrollDelta;
            if (next < 0) next = HotbarSize - 1;
            if (next >= HotbarSize) next = 0;
            SelectHotbar(next);
        }

        // Close storage if walked away
        if (_openStorage != null)
        {
            if (_openStorage.IsDestroyed || !IsNearStorage(_openStorage))
                CloseStorage();
        }
    }

    /// <summary>
    /// Shift+click: move stack to the other panel (bag↔hotbar, or ↔chest when open).
    /// </summary>
    public void QuickTransferSlot(IInventorySlots host, int index)
    {
        if (!_menuOpen || host == null || index < 0 || index >= host.SlotCount)
            return;

        InventorySlot slot = host.GetSlot(index);
        if (slot.IsEmpty)
            return;

        // Chest → player
        if (host is StorageContainer)
        {
            InventoryTransfer.TryDeposit(host, index, this, 0, TotalSize);
            return;
        }

        // Player → chest if open
        if (IsStorageOpen && ReferenceEquals(host, this))
        {
            InventoryTransfer.TryDeposit(this, index, _openStorage);
            return;
        }

        // Player bag ↔ hotbar
        if (!ReferenceEquals(host, this))
            return;

        if (index < HotbarSize)
            InventoryTransfer.TryDeposit(this, index, this, HotbarSize, BagSize);
        else
            InventoryTransfer.TryDeposit(this, index, this, 0, HotbarSize);
    }

    public void NotifyGameplayBlockChanged() => Changed?.Invoke();

    public bool TryOpenStorage(StorageContainer storage)
    {
        if (storage == null || storage.IsDestroyed)
            return false;
        if (!IsNearStorage(storage))
            return false;

        OwnMageStation.CloseOpen();
        CraftMenu?.Close();

        if (_openStorage != null && _openStorage != storage)
            UnsubscribeStorage(_openStorage);

        _openStorage = storage;
        _openStorage.Changed += OnStorageChanged;
        SetMenuOpen(true);
        StorageOpenChanged?.Invoke();
        return true;
    }

    public void CloseStorage()
    {
        if (_openStorage == null)
            return;
        UnsubscribeStorage(_openStorage);
        _openStorage = null;
        StorageOpenChanged?.Invoke();
        Changed?.Invoke();
    }

    public void CloseMenuAndStorage()
    {
        CloseStorage();
        SetMenuOpen(false);
    }

    bool IsNearStorage(StorageContainer storage)
    {
        if (storage == null)
            return false;
        return storage.IsInRange(transform.position);
    }

    void UnsubscribeStorage(StorageContainer storage)
    {
        if (storage != null)
            storage.Changed -= OnStorageChanged;
    }

    void OnStorageChanged() => Changed?.Invoke();

    public int CountItem(ItemDefinition item)
    {
        if (item == null)
            return 0;
        int total = 0;
        for (int i = 0; i < TotalSize; i++)
        {
            if (_slots[i].IsEmpty || !SameItem(_slots[i].Item, item))
                continue;
            total += _slots[i].Count;
        }
        return total;
    }

    public bool TryConsumeItem(ItemDefinition item, int count)
    {
        if (item == null || count <= 0)
            return false;
        if (CountItem(item) < count)
            return false;

        int remaining = count;
        for (int i = 0; i < TotalSize && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty || !SameItem(_slots[i].Item, item))
                continue;

            int take = Mathf.Min(_slots[i].Count, remaining);
            _slots[i].Count -= take;
            remaining -= take;
            if (_slots[i].Count <= 0)
                _slots[i] = default;
        }

        Changed?.Invoke();
        SelectionChanged?.Invoke();
        return remaining == 0;
    }

    public bool TryConsumeSelected(int count)
    {
        ItemDefinition item = SelectedItem;
        if (item == null || count <= 0)
            return false;

        InventorySlot slot = _slots[_selectedHotbarIndex];
        if (!slot.IsEmpty && SameItem(slot.Item, item) && slot.Count >= count)
        {
            slot.Count -= count;
            if (slot.Count <= 0)
                slot = default;
            _slots[_selectedHotbarIndex] = slot;
            Changed?.Invoke();
            SelectionChanged?.Invoke();
            return true;
        }

        return TryConsumeItem(item, count);
    }

    public void SetMenuOpen(bool open)
    {
        if (_menuOpen == open)
        {
            if (!open)
                CloseStorage();
            return;
        }

        _menuOpen = open;
        if (_menuOpen)
        {
            WorldMapUI.CloseIfOpen();
            SettingsMenuUI.CloseIfOpen();
        }
        if (!_menuOpen)
            CloseStorage();
        MenuOpenChanged?.Invoke(_menuOpen);
        ApplyCursor();
    }

    public void ToggleMenu() => SetMenuOpen(!_menuOpen);

    public void SelectHotbar(int index)
    {
        index = Mathf.Clamp(index, 0, HotbarSize - 1);
        if (_selectedHotbarIndex == index)
            return;

        _selectedHotbarIndex = index;
        SelectionChanged?.Invoke();
        Changed?.Invoke();
    }

    public void SetHotbarItem(int hotbarIndex, ItemDefinition item, int count = 1)
    {
        if (hotbarIndex < 0 || hotbarIndex >= HotbarSize || item == null)
            return;

        _slots[hotbarIndex] = new InventorySlot { Item = item, Count = Mathf.Max(1, count) };
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void SwapSlots(int fromIndex, int toIndex)
    {
        InventoryTransfer.Swap(this, fromIndex, this, toIndex);
    }

    public int TryAddItem(ItemDefinition item, int count)
    {
        if (item == null || count <= 0)
            return 0;

        int remaining = count;
        int maxStack = item.MaxStack;

        for (int i = 0; i < TotalSize && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty || !SameItem(_slots[i].Item, item))
                continue;
            if (_slots[i].Count >= maxStack)
                continue;

            int space = maxStack - _slots[i].Count;
            int add = Mathf.Min(space, remaining);
            _slots[i].Count += add;
            remaining -= add;
        }

        for (int i = 0; i < TotalSize && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty)
                continue;

            int add = Mathf.Min(maxStack, remaining);
            _slots[i] = new InventorySlot { Item = item, Count = add };
            remaining -= add;
        }

        int added = count - remaining;
        if (added > 0)
        {
            Changed?.Invoke();
            SelectionChanged?.Invoke();
        }

        return added;
    }

    public int CountFreeSpaceFor(ItemDefinition item)
    {
        if (item == null)
            return 0;
        int space = 0;
        int maxStack = item.MaxStack;
        for (int i = 0; i < TotalSize; i++)
        {
            if (_slots[i].IsEmpty)
            {
                space += maxStack;
                continue;
            }

            if (SameItem(_slots[i].Item, item) && _slots[i].Count < maxStack)
                space += maxStack - _slots[i].Count;
        }

        return space;
    }

    static bool SameItem(ItemDefinition a, ItemDefinition b)
    {
        if (a == null || b == null)
            return false;
        if (ReferenceEquals(a, b))
            return true;
        return a.Id == b.Id;
    }

    void ApplyCursor()
    {
        if (_menuOpen || IsCraftOpen || IsMageOpen || IsMapOpen || IsSettingsOpen)
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

    void OnEnable() => ApplyCursor();

    void OnDisable()
    {
        CloseStorage();
    }
}
