using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hotbar furniture (chest) → courtyard pads light up, hover ghost, LMB places.
/// </summary>
[DefaultExecutionOrder(20)]
public sealed class FurniturePlacer : MonoBehaviour
{
    const float SnapRange = 14f;

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;

    FurnitureGrid _grid;
    FurnitureSlot _hovered;
    GameObject _ghost;
    bool _padsShown;

    public bool IsPlacing => WantsPlace();

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
    }

    void OnDisable() => HidePlacement();

    void Update()
    {
        if (!WantsPlace())
        {
            if (_padsShown || _ghost != null || _hovered != null)
                HidePlacement();
            return;
        }

        EnsureGrid();
        ShowPads();
        TickSnap();

        if (_input != null && _input.AttackPressed && _hovered != null && !PointerOverUi())
            TryPlace();
    }

    bool WantsPlace()
    {
        if (_inventory == null)
            return false;
        if (_inventory.SelectedKind != ItemKind.Furniture)
            return false;
        if (_inventory.IsMenuOpen || _inventory.IsCraftOpen || _inventory.IsMageOpen || _inventory.IsStorageOpen
            || _inventory.IsMapOpen || _inventory.IsSettingsOpen)
            return false;

        var castle = GetComponent<CastleBuildController>();
        if (castle != null && castle.Mode != CastleBuildMode.Closed)
            return false;

        var building = GetComponent<BuildingController>();
        if (building != null && building.Mode != BuildingMode.Closed)
            return false;

        return true;
    }

    void EnsureGrid()
    {
        if (_grid == null)
            _grid = FurnitureGrid.FindOnPlayerCastle();
    }

    void ShowPads()
    {
        if (_grid == null || _padsShown)
            return;
        _grid.SetPadsVisible(true);
        _padsShown = true;
    }

    void TickSnap()
    {
        if (_grid == null || _camera == null)
        {
            SetHovered(null);
            return;
        }

        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        SetHovered(_grid.FindAimSlot(ray, SnapRange));
    }

    void SetHovered(FurnitureSlot slot)
    {
        if (_hovered == slot)
        {
            SyncGhost();
            return;
        }

        if (_hovered != null)
            _hovered.SetHovered(false);
        _hovered = slot;
        if (_hovered != null)
            _hovered.SetHovered(true);
        SyncGhost();
    }

    void SyncGhost()
    {
        if (_hovered == null)
        {
            DestroyGhost();
            return;
        }

        if (_ghost == null)
            _ghost = FurnitureFactory.CreateGhost(_inventory.SelectedItem);
        if (_ghost == null)
            return;

        _ghost.SetActive(true);
        _ghost.transform.SetPositionAndRotation(_hovered.GetPlacePose(), _hovered.Anchor.rotation);
    }

    void TryPlace()
    {
        ItemDefinition item = _inventory.SelectedItem;
        if (item == null || _hovered == null || _hovered.IsOccupied)
            return;
        if (_inventory.CountItem(item) < 1)
            return;

        GameObject instance = FurnitureFactory.Create(item, _hovered.GetPlacePose(), _hovered.Anchor.rotation);
        if (instance == null)
            return;

        if (!_hovered.TryPlace(instance))
        {
            Destroy(instance);
            return;
        }

        _inventory.TryConsumeSelected(1);
        SetHovered(null);
    }

    void HidePlacement()
    {
        SetHovered(null);
        DestroyGhost();
        if (_padsShown && _grid != null)
            _grid.SetPadsVisible(false);
        _padsShown = false;
        _grid = null;
    }

    void DestroyGhost()
    {
        if (_ghost == null)
            return;
        Destroy(_ghost);
        _ghost = null;
    }

    static bool PointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
