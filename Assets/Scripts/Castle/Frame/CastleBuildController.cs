using UnityEngine;
using UnityEngine.EventSystems;

public enum CastleBuildMode
{
    Closed = 0,
    Menu = 1,
    Placing = 2,
    Demolish = 3
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
    [Tooltip("Доля стоимости модуля, которая возвращается при сносе. 1 — полный возврат: снос = переставить.")]
    [SerializeField, Range(0f, 1f)] float _demolishRefund = 1f;
    [SerializeField] float _demolishRange = 8f;

    CastleBuildMode _mode = CastleBuildMode.Closed;
    CastleModuleDefinition _selected;
    CarcassBay _highlightedBay;
    GameObject _ghost;
    int _snapX;
    int _snapY;
    int _snapZ;
    bool _hasSectionSnap;
    CarcassBay _demolishTarget;
    StationBuildTool _stations;
    static readonly Color DemolishTint = new Color(1f, 0.3f, 0.25f, 1f);
    static Material _ghostOk;
    static Material _ghostBad;

    public CastleBuildMode Mode => _mode;
    public bool IsMenuOpen => _mode == CastleBuildMode.Menu;
    public bool IsPlacing => _mode == CastleBuildMode.Placing;
    /// <summary>Подсказка под прицелом в режиме сноса; null — ничего не показываем.</summary>
    public static string DemolishPrompt { get; private set; }
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
        _stations = new StationBuildTool(_inventory);
    }

    void Update()
    {
        if (_input == null)
            return;

        HandleHotkeys();

        if (_mode == CastleBuildMode.Placing)
            TickPlacing();
        else if (_mode == CastleBuildMode.Demolish)
            TickDemolish();
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
            // На паузе стройка недоступна; открытая карта уступает место меню стройки.
            if (SettingsMenuUI.IsOpen)
                return;
            WorldMapUI.CloseIfOpen();
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
        if ((_selected == null && !_stations.IsActive) || _camera == null)
            return;

        if (_input.SecondaryPressed)
        {
            OpenMenu();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (_stations.IsActive)
        {
            if (_input.RotateBuildingPressed)
                _stations.Rotate();
            var castle = CarcassCastle.FindPlayerOwned();
            _stations.Tick(castle, _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));
            if (_input.AttackPressed)
                _stations.TryPlace(castle);
            return;
        }

        TickSnapAndGhost();

        if (_input.AttackPressed)
            TryPlace();
    }

    public void SelectStation(StationDefinition definition)
    {
        if (definition == null)
            return;
        _selected = null;
        DestroyGhost();
        ClearHighlight();
        _stations.Begin(definition);
        _menuUi?.Show(false);
        SetMode(CastleBuildMode.Placing);
        ApplyCursor();
    }

    public void SelectDemolish()
    {
        _selected = null;
        _stations.End();
        DestroyGhost();
        ClearHighlight();
        _menuUi?.Show(false);
        SetMode(CastleBuildMode.Demolish);
        ApplyCursor();
    }

    void TickDemolish()
    {
        if (_input.SecondaryPressed)
        {
            OpenMenu();
            return;
        }

        // Станции перекрывают стены за ними: сначала проверяем их.
        PlacedStation station = _stations.FindDemolishTarget(
            _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), _demolishRange);
        _stations.SetDemolishTarget(station);
        if (station != null)
        {
            SetDemolishTarget(null);
            DemolishPrompt = _stations.DemolishPrompt(station);
            if (_input.AttackPressed)
                _stations.Demolish(station);
            return;
        }

        SetDemolishTarget(FindDemolishTarget());
        DemolishPrompt = _demolishTarget != null
            ? $"[LMB] Demolish {_demolishTarget.Occupant} (+{RefundFor(_demolishTarget.Occupant)} stone)"
            : "Aim at a wall, window or door";
        if (_demolishTarget != null && _input.AttackPressed)
            Demolish(_demolishTarget);
    }

    /// <summary>Только модули стен (стена/окно/дверь) своего замка, точно под прицелом.</summary>
    CarcassBay FindDemolishTarget()
    {
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, _demolishRange, ~0, QueryTriggerInteraction.Ignore))
            return null;
        var bay = hit.collider.GetComponentInParent<CarcassBay>();
        if (bay == null || bay.IsEmpty)
            return null;
        var castle = bay.GetComponentInParent<CarcassCastle>();
        return castle != null && castle.IsPlayerOwned ? bay : null;
    }

    void SetDemolishTarget(CarcassBay bay)
    {
        if (bay == _demolishTarget)
            return;
        TintModule(_demolishTarget, false);
        _demolishTarget = bay;
        TintModule(_demolishTarget, true);
    }

    void Demolish(CarcassBay bay)
    {
        int refund = RefundFor(bay.Occupant);
        Vector3 fxPoint = bay.transform.position + Vector3.up * 1.2f;
        _demolishTarget = null;
        bay.ClearModule();
        if (refund > 0)
            _inventory.TryAddItem(_stoneItem, refund);
        HitSparkVfx.PlayDust(fxPoint, bay.transform.right, 18);
        DemolishPrompt = null;
    }

    /// <summary>Стены между старой и новой секцией теперь внутри — снимаем их и возвращаем ресурсы.</summary>
    void RefundSharedWalls(CarcassCastle castle, int x, int y, int z)
    {
        var removed = new System.Collections.Generic.List<CastleModuleKind>(4);
        castle.ClearSharedWalls(x, y, z, removed);
        int refund = 0;
        for (int i = 0; i < removed.Count; i++)
            refund += RefundFor(removed[i]);
        if (refund > 0)
            _inventory.TryAddItem(_stoneItem, refund);
    }

    int RefundFor(CastleModuleKind kind)
    {
        var definition = _menuUi != null ? _menuUi.FindDefinition(kind) : null;
        return definition != null ? Mathf.FloorToInt(definition.StoneCost * _demolishRefund) : 0;
    }

    static void TintModule(CarcassBay bay, bool on)
    {
        if (bay == null || bay.Module == null)
            return;
        var block = new MaterialPropertyBlock();
        foreach (var r in bay.Module.GetComponentsInChildren<Renderer>())
        {
            if (!on)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            block.SetColor("_BaseColor", DemolishTint);
            block.SetColor("_Color", DemolishTint);
            r.SetPropertyBlock(block);
        }
    }

    public void OpenMenu()
    {
        DestroyGhost();
        ClearHighlight();
        _stations?.End();
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
        _stations?.End();
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
        _stations.End();
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
            {
                placed = castle.TryAddSection(_snapX, _snapY, _snapZ, fillOuterWalls: false, CarcassMetrics.WallDir.N, 0);
                if (placed)
                    RefundSharedWalls(castle, _snapX, _snapY, _snapZ);
            }
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

    public static void ApplyGhostMaterials(GameObject ghost)
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

    void SetGhostValid(bool valid) => TintGhost(_ghost, valid);

    public static void TintGhost(GameObject ghost, bool valid)
    {
        if (ghost == null)
            return;
        EnsureGhostMats();
        var mat = valid ? _ghostOk : _ghostBad;
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
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
        SetDemolishTarget(null);
        DemolishPrompt = null;
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
            _stations?.End();
            _selected = null;
            _menuUi?.Show(false);
            _mode = CastleBuildMode.Closed;
        }
    }
}
