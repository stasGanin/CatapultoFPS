using UnityEngine;
using UnityEngine.EventSystems;

public enum CastleBuildMode
{
    Closed = 0,
    Menu = 1,
    Placing = 2
}

/// <summary>
/// B opens the castle menu. Section snaps to the 13 m grid; wall/window/door snap to shared bays.
/// </summary>
public sealed class CastleBuildController : MonoBehaviour
{
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] CastleModuleMenuUI _menuUi;
    [SerializeField] ItemDefinition _stoneItem;
    [SerializeField] float _snapRange = 28f;

    CastleBuildMode _mode = CastleBuildMode.Closed;
    CastleModuleDefinition _selected;
    CarcassBay _highlightedBay;
    GameObject _ghost;
    int _snapX;
    int _snapY;
    int _snapZ;
    bool _hasSectionSnap;
    static Material _ghostOk;
    static Material _ghostBad;

    public CastleBuildMode Mode => _mode;
    public bool IsMenuOpen => _mode == CastleBuildMode.Menu;
    public bool IsPlacing => _mode == CastleBuildMode.Placing;
    public bool BlocksWeapons => _mode != CastleBuildMode.Closed;
    public bool BlocksLook => _mode == CastleBuildMode.Menu;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_stoneItem == null)
            _stoneItem = Resources.Load<ItemDefinition>("Items/StoneItem");
        if (_menuUi == null)
            _menuUi = GetComponent<CastleModuleMenuUI>();
        if (_menuUi == null)
            _menuUi = gameObject.AddComponent<CastleModuleMenuUI>();
    }

    void Update()
    {
        if (_input == null)
            return;

        HandleHotkeys();

        if (_mode == CastleBuildMode.Placing)
            TickPlacing();
    }

    void HandleHotkeys()
    {
        if (_input.CancelPressed)
        {
            if (_mode != CastleBuildMode.Closed)
                CloseAll();
            return;
        }

        if (_input.InventoryTogglePressed && _mode != CastleBuildMode.Closed)
        {
            CloseAll();
            _inventory?.SetMenuOpen(true);
            return;
        }

        if (_input.BuildMenuPressed)
        {
            if (_mode == CastleBuildMode.Closed)
                OpenMenu();
            else if (_mode == CastleBuildMode.Menu)
                CloseAll();
            else
                OpenMenu();
            return;
        }

        if (_mode == CastleBuildMode.Menu && _input.SecondaryPressed)
            CloseAll();
    }

    void TickPlacing()
    {
        if (_selected == null || _camera == null)
            return;

        if (_input.SecondaryPressed)
        {
            OpenMenu();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TickSnapAndGhost();

        if (_input.AttackPressed)
            TryPlace();
    }

    public void OpenMenu()
    {
        DestroyGhost();
        ClearHighlight();
        _selected = null;
        _inventory?.SetMenuOpen(false);
        GetComponent<CraftMenuController>()?.Close();
        OwnMageStation.CloseOpen();
        SetMode(CastleBuildMode.Menu);
        _menuUi?.Show(true);
        ApplyCursor();
    }

    public void CloseAll()
    {
        DestroyGhost();
        ClearHighlight();
        _selected = null;
        _menuUi?.Show(false);
        SetMode(CastleBuildMode.Closed);
        ApplyCursor();
    }

    public void SelectModule(CastleModuleDefinition definition)
    {
        if (definition == null)
            return;

        _selected = definition;
        _menuUi?.Show(false);
        EnsureGhost();
        SetMode(CastleBuildMode.Placing);
        ApplyCursor();
    }

    void SetMode(CastleBuildMode mode)
    {
        if (_mode == mode)
            return;
        _mode = mode;
        _inventory?.NotifyGameplayBlockChanged();
    }

    void TickSnapAndGhost()
    {
        var castle = CarcassCastle.FindPlayerOwned();
        if (_ghost == null || castle == null)
            return;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool valid = false;

        if (_selected.Kind == CastleModuleKind.Section)
        {
            ClearHighlight();
            _hasSectionSnap = castle.FindBestSectionCell(ray, _snapRange, out _snapX, out _snapY, out _snapZ);
            valid = _hasSectionSnap && CanAfford();
            if (_hasSectionSnap)
            {
                _ghost.SetActive(true);
                _ghost.transform.SetPositionAndRotation(
                    castle.CellWorldCenter(_snapX, _snapY, _snapZ),
                    castle.transform.rotation);
            }
            else
            {
                PlaceGhostAlongRay(ray, false);
            }
        }
        else
        {
            CarcassBay bay = castle.FindBestBay(ray, _snapRange);
            if (bay != _highlightedBay)
            {
                if (_highlightedBay != null)
                    _highlightedBay.SetPreviewHidden(false);
                _highlightedBay = bay;
                if (_highlightedBay != null)
                    _highlightedBay.SetPreviewHidden(true);
            }

            valid = bay != null && CanAfford();
            if (bay != null)
            {
                _ghost.SetActive(true);
                _ghost.transform.SetPositionAndRotation(bay.transform.position, bay.transform.rotation);
            }
            else
            {
                PlaceGhostAlongRay(ray, false);
            }
        }

        SetGhostValid(valid);
    }

    void PlaceGhostAlongRay(Ray ray, bool valid)
    {
        _ghost.SetActive(true);
        _ghost.transform.position = ray.origin + ray.direction * 8f;
        SetGhostValid(valid);
    }

    void TryPlace()
    {
        var castle = CarcassCastle.FindPlayerOwned();
        if (castle == null || _selected == null || !CanAfford())
            return;

        int cost = _selected.StoneCost;
        if (cost > 0 && !_inventory.TryConsumeItem(_stoneItem, cost))
            return;

        bool placed = false;
        if (_selected.Kind == CastleModuleKind.Section)
        {
            if (_hasSectionSnap)
                placed = castle.TryAddSection(_snapX, _snapY, _snapZ, fillOuterWalls: false, CarcassMetrics.WallDir.N, 0);
        }
        else if (_highlightedBay != null)
        {
            placed = castle.TrySetBay(_highlightedBay, _selected.Kind);
        }

        if (!placed)
        {
            if (cost > 0)
                _inventory.TryAddItem(_stoneItem, cost);
            return;
        }

        ClearHighlight();
        EnsureGhost();
    }

    bool CanAfford()
    {
        if (_selected == null || _selected.StoneCost <= 0)
            return true;
        return _stoneItem != null && _inventory != null &&
               _inventory.CountItem(_stoneItem) >= _selected.StoneCost;
    }

    void EnsureGhost()
    {
        DestroyGhost();
        if (_selected == null)
            return;

        _ghost = _selected.Kind == CastleModuleKind.Section
            ? CarcassKit.CreateSectionGhost()
            : CarcassKit.CreateWallGhost(_selected.Kind);
        ApplyGhostMaterials(_ghost);
        SetGhostValid(false);
    }

    static void ApplyGhostMaterials(GameObject ghost)
    {
        EnsureGhostMats();
        foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null)
                continue;
            r.sharedMaterial = _ghostOk;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    static void EnsureGhostMats()
    {
        if (_ghostOk != null && _ghostBad != null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        _ghostOk = MakeGhostMat(shader, new Color(0.2f, 0.95f, 0.35f, 0.5f));
        _ghostBad = MakeGhostMat(shader, new Color(0.95f, 0.25f, 0.2f, 0.45f));
    }

    static Material MakeGhostMat(Shader shader, Color c)
    {
        var mat = new Material(shader);
        MakeTransparent(mat);
        mat.color = c;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        return mat;
    }

    static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
    }

    void SetGhostValid(bool valid)
    {
        if (_ghost == null)
            return;
        EnsureGhostMats();
        var mat = valid ? _ghostOk : _ghostBad;
        foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
        {
            if (r != null)
                r.sharedMaterial = mat;
        }
    }

    void DestroyGhost()
    {
        if (_ghost != null)
            Destroy(_ghost);
        _ghost = null;
    }

    void ClearHighlight()
    {
        if (_highlightedBay != null)
            _highlightedBay.SetPreviewHidden(false);
        _highlightedBay = null;
        _hasSectionSnap = false;
    }

    void ApplyCursor()
    {
        if (_mode == CastleBuildMode.Menu)
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

    void OnDisable()
    {
        if (_mode != CastleBuildMode.Closed)
        {
            DestroyGhost();
            ClearHighlight();
            _selected = null;
            _menuUi?.Show(false);
            _mode = CastleBuildMode.Closed;
        }
    }
}
