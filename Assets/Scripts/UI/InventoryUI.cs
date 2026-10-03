using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Beige parchment hotbar + bag + optional chest panel. Hotkeys: I, Esc, 1–9, scroll, E on chest.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // Размер панелей считается от сетки слотов, а не долями панели: иначе слоты вылезают за рамку.
    const float BagCell = 56f;
    const float StorageCell = 52f;
    const float CellSpacing = 6f;
    const float PanelPadding = 24f;
    const float PanelHeader = 48f;
    const float HintHeight = 22f;
    const int BagRows = PlayerInventory.BagSize / PlayerInventory.HotbarSize;
    const int StorageColumns = 6;
    const int StorageRows = 6;

    static Vector2 GridPanelSize(int columns, int rows, float cell)
    {
        float w = columns * cell + (columns - 1) * CellSpacing + PanelPadding * 2f;
        float h = rows * cell + (rows - 1) * CellSpacing + PanelPadding + PanelHeader;
        return new Vector2(w, h);
    }

    [Header("Scene / Prefab")]
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] GameObject _bagRoot;
    [SerializeField] Transform _dragLayer;
    [SerializeField] Transform _hotbarRoot;
    [SerializeField] Transform _bagGridRoot;
    [SerializeField] InventorySlotView[] _prebuiltSlots;

    [Header("Fallback templates (optional)")]
    [SerializeField] InventorySlotView _slotPrefab;

    readonly List<InventorySlotView> _playerViews = new(PlayerInventory.TotalSize);
    readonly List<InventorySlotView> _storageViews = new(32);
    Font _font;
    bool _built;
    GameObject _storageRoot;
    Transform _storageGridRoot;
    Text _storageTitle;
    Text _hintLabel;

    public PlayerInventory Inventory => _inventory;
    public Transform DragLayer => _dragLayer;

    public void Bind(PlayerInventory inventory)
    {
        if (_inventory != null)
        {
            _inventory.Changed -= RefreshAll;
            _inventory.SelectionChanged -= RefreshAll;
            _inventory.MenuOpenChanged -= OnMenuOpenChanged;
            _inventory.StorageOpenChanged -= OnStorageOpenChanged;
        }

        _inventory = inventory;
        if (isActiveAndEnabled && _inventory != null)
        {
            _inventory.Changed += RefreshAll;
            _inventory.SelectionChanged += RefreshAll;
            _inventory.MenuOpenChanged += OnMenuOpenChanged;
            _inventory.StorageOpenChanged += OnStorageOpenChanged;
        }

        OnMenuOpenChanged(_inventory != null && _inventory.IsMenuOpen);
        OnStorageOpenChanged();
        RefreshAll();
    }

    void Awake()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();

        EnsureEventSystem();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        CollectOrBuild();
    }

    void OnEnable()
    {
        if (_inventory == null)
            return;
        _inventory.Changed += RefreshAll;
        _inventory.SelectionChanged += RefreshAll;
        _inventory.MenuOpenChanged += OnMenuOpenChanged;
        _inventory.StorageOpenChanged += OnStorageOpenChanged;
    }

    void OnDisable()
    {
        if (_inventory == null)
            return;
        _inventory.Changed -= RefreshAll;
        _inventory.SelectionChanged -= RefreshAll;
        _inventory.MenuOpenChanged -= OnMenuOpenChanged;
        _inventory.StorageOpenChanged -= OnStorageOpenChanged;
    }

    void Start()
    {
        OnMenuOpenChanged(_inventory != null && _inventory.IsMenuOpen);
        OnStorageOpenChanged();
        RefreshAll();
    }

    public void SelectHotbarFromUi(int hotbarIndex) => _inventory?.SelectHotbar(hotbarIndex);

    void OnMenuOpenChanged(bool open)
    {
        if (_bagRoot != null)
            _bagRoot.SetActive(open);
        if (!open && _storageRoot != null)
            _storageRoot.SetActive(false);
        else if (open)
            OnStorageOpenChanged();
    }

    void OnStorageOpenChanged()
    {
        EnsureStoragePanel();
        bool show = _inventory != null && _inventory.IsMenuOpen && _inventory.IsStorageOpen;
        if (_storageRoot != null)
            _storageRoot.SetActive(show);

        RebuildStorageViews();
        RefreshAll();
    }

    void RefreshAll()
    {
        if (_inventory == null)
            return;

        for (int i = 0; i < _playerViews.Count; i++)
        {
            bool isHotbar = i < PlayerInventory.HotbarSize;
            bool selected = isHotbar && i == _inventory.SelectedHotbarIndex;
            _playerViews[i].Refresh(_inventory.GetSlot(i), selected);
        }

        var storage = _inventory.OpenStorage;
        if (storage != null)
        {
            if (_storageTitle != null)
                _storageTitle.text = storage.DisplayName;
            for (int i = 0; i < _storageViews.Count; i++)
                _storageViews[i].Refresh(storage.GetSlot(i), false);
        }
    }

    void CollectOrBuild()
    {
        if (_built)
            return;

        _playerViews.Clear();

        if (_prebuiltSlots != null && _prebuiltSlots.Length >= PlayerInventory.TotalSize)
        {
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                var view = _prebuiltSlots[i];
                if (view == null)
                    continue;
                view.Bind(this, _inventory, i, i < PlayerInventory.HotbarSize);
                _playerViews.Add(view);
            }

            _built = _playerViews.Count == PlayerInventory.TotalSize;
            if (_built)
            {
                RecolorLegacyPanels();
                EnsureStoragePanel();
                return;
            }
        }

        if (_hotbarRoot != null && _bagGridRoot != null)
        {
            var hotbarSlots = _hotbarRoot.GetComponentsInChildren<InventorySlotView>(true);
            var bagSlots = _bagGridRoot.GetComponentsInChildren<InventorySlotView>(true);
            if (hotbarSlots.Length >= PlayerInventory.HotbarSize &&
                bagSlots.Length >= PlayerInventory.BagSize)
            {
                for (int i = 0; i < PlayerInventory.HotbarSize; i++)
                {
                    hotbarSlots[i].Bind(this, _inventory, i, true);
                    _playerViews.Add(hotbarSlots[i]);
                }

                for (int i = 0; i < PlayerInventory.BagSize; i++)
                {
                    bagSlots[i].Bind(this, _inventory, PlayerInventory.HotbarSize + i, false);
                    _playerViews.Add(bagSlots[i]);
                }

                _built = true;
                RecolorLegacyPanels();
                EnsureStoragePanel();
                return;
            }
        }

        BuildUiRuntime();
        _built = true;
    }

    void RecolorLegacyPanels()
    {
        ApplyPanelStyle(_bagRoot);
        if (_hotbarRoot != null)
            ApplyPanelStyle(_hotbarRoot.gameObject, InventoryUiTheme.HotbarPanel);
    }

    void EnsureStoragePanel()
    {
        if (_storageRoot != null)
            return;

        Transform canvas = null;
        if (_dragLayer != null)
            canvas = _dragLayer.parent;
        else if (_bagRoot != null)
            canvas = _bagRoot.transform.parent;
        if (canvas == null)
            return;

        if (_dragLayer == null)
        {
            _dragLayer = new GameObject("DragLayer", typeof(RectTransform)).transform;
            _dragLayer.SetParent(canvas, false);
            StretchFull((RectTransform)_dragLayer);
        }

        _storageRoot = CreatePanel(canvas, "StoragePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-400f, 10f), GridPanelSize(StorageColumns, StorageRows, StorageCell));
        ApplyPanelStyle(_storageRoot);

        _storageTitle = CreateText(_storageRoot.transform, "Chest", 20, TextAnchor.UpperCenter);
        var titleRt = _storageTitle.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 32f);
        titleRt.anchoredPosition = new Vector2(0f, -10f);
        _storageTitle.color = InventoryUiTheme.TextPrimary;

        var gridHost = new GameObject("StorageGrid", typeof(RectTransform));
        gridHost.transform.SetParent(_storageRoot.transform, false);
        _storageGridRoot = gridHost.transform;
        var gridRt = (RectTransform)gridHost.transform;
        gridRt.anchorMin = Vector2.zero;
        gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = new Vector2(PanelPadding, PanelPadding);
        gridRt.offsetMax = new Vector2(-PanelPadding, -PanelHeader);

        var grid = gridHost.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(StorageCell, StorageCell);
        grid.spacing = new Vector2(CellSpacing, CellSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = StorageColumns;
        grid.childAlignment = TextAnchor.UpperCenter;

        _storageRoot.SetActive(false);
        if (_bagRoot != null)
            _dragLayer.SetAsLastSibling();
    }

    void RebuildStorageViews()
    {
        EnsureStoragePanel();
        if (_storageGridRoot == null)
            return;

        for (int i = _storageGridRoot.childCount - 1; i >= 0; i--)
            Destroy(_storageGridRoot.GetChild(i).gameObject);
        _storageViews.Clear();

        var storage = _inventory != null ? _inventory.OpenStorage : null;
        if (_storageTitle != null && storage != null)
            _storageTitle.text = storage.DisplayName;
        if (storage == null)
            return;

        for (int i = 0; i < storage.SlotCount; i++)
        {
            var view = CreateSlotView(_storageGridRoot, false, string.Empty);
            view.Bind(this, storage, i, false);
            _storageViews.Add(view);
        }
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    void BuildUiRuntime()
    {
        var canvasGo = new GameObject("InventoryCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        UiScale.Configure(canvasGo.AddComponent<CanvasScaler>());
        canvasGo.AddComponent<GraphicRaycaster>();

        _dragLayer = new GameObject("DragLayer", typeof(RectTransform)).transform;
        _dragLayer.SetParent(canvasGo.transform, false);
        StretchFull((RectTransform)_dragLayer);

        var hotbar = CreatePanel(canvasGo.transform, "Hotbar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(560f, 78f));
        _hotbarRoot = hotbar.transform;
        ApplyPanelStyle(hotbar, InventoryUiTheme.HotbarPanel);
        var hotbarLayout = hotbar.AddComponent<HorizontalLayoutGroup>();
        hotbarLayout.spacing = 6f;
        hotbarLayout.childAlignment = TextAnchor.MiddleCenter;
        hotbarLayout.padding = new RectOffset(10, 10, 10, 10);

        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
            _playerViews.Add(CreateSlotView(hotbar.transform, true, (i + 1).ToString()));

        _bagRoot = CreatePanel(canvasGo.transform, "BagPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(160f, 20f), GridPanelSize(PlayerInventory.HotbarSize, BagRows, BagCell) + new Vector2(0f, HintHeight));
        ApplyPanelStyle(_bagRoot);

        var title = CreateText(_bagRoot.transform, "Inventory", 22, TextAnchor.UpperCenter);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 36f);
        titleRt.anchoredPosition = new Vector2(0f, -8f);
        title.color = InventoryUiTheme.TextPrimary;

            _hintLabel = CreateText(_bagRoot.transform, "I / Esc  close    ·    C  craft    ·    Shift+click transfer    ·    E  chest", 13, TextAnchor.LowerCenter);
        var hintRt = _hintLabel.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.offsetMin = new Vector2(PanelPadding, PanelPadding * 0.5f);
        hintRt.offsetMax = new Vector2(-PanelPadding, PanelPadding * 0.5f + HintHeight);
        _hintLabel.color = InventoryUiTheme.TextSecondary;

        var gridHost = new GameObject("BagGrid", typeof(RectTransform));
        gridHost.transform.SetParent(_bagRoot.transform, false);
        _bagGridRoot = gridHost.transform;
        var gridRt = (RectTransform)gridHost.transform;
        gridRt.anchorMin = Vector2.zero;
        gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = new Vector2(PanelPadding, PanelPadding + HintHeight);
        gridRt.offsetMax = new Vector2(-PanelPadding, -PanelHeader);

        var grid = gridHost.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(BagCell, BagCell);
        grid.spacing = new Vector2(CellSpacing, CellSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = PlayerInventory.HotbarSize;
        grid.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < PlayerInventory.BagSize; i++)
            _playerViews.Add(CreateSlotView(gridHost.transform, false, string.Empty));

        // Bind player hosts
        for (int i = 0; i < _playerViews.Count; i++)
            _playerViews[i].Bind(this, _inventory, i, i < PlayerInventory.HotbarSize);

        _bagRoot.SetActive(false);
        EnsureStoragePanel();
        _dragLayer.SetAsLastSibling();
    }

    InventorySlotView CreateSlotView(Transform parent, bool isHotbar, string keyHint)
    {
        if (_slotPrefab != null)
        {
            var view = Instantiate(_slotPrefab, parent);
            view.name = isHotbar ? $"Hotbar_{keyHint}" : "Slot";
            if (!string.IsNullOrEmpty(keyHint))
                view.SetKeyHint(keyHint);
            return view;
        }

        var go = new GameObject(isHotbar ? $"Hotbar_{keyHint}" : "Slot", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var bg = go.AddComponent<Image>();
        Sprite slotSpr = InventoryUiTheme.SlotSprite;
        if (slotSpr != null)
        {
            bg.sprite = slotSpr;
            bg.type = Image.Type.Sliced;
            bg.color = InventoryUiTheme.SlotNormal;
        }
        else
            bg.color = InventoryUiTheme.SlotNormal;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = InventoryUiTheme.Accent;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = new Vector2(0.12f, 0.22f);
        iconRt.anchorMax = new Vector2(0.88f, 0.88f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        var icon = iconGo.AddComponent<Image>();
        icon.color = Color.white;
        icon.enabled = false;
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        var label = CreateText(go.transform, string.Empty, 14, TextAnchor.LowerRight);
        label.gameObject.name = "Count";
        var labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0.05f, 0f);
        labelRt.anchorMax = new Vector2(0.95f, 0.32f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        label.color = InventoryUiTheme.TextPrimary;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;

        if (!string.IsNullOrEmpty(keyHint))
        {
            var hint = CreateText(go.transform, keyHint, 11, TextAnchor.UpperLeft);
            hint.gameObject.name = "KeyHint";
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0.7f);
            hintRt.anchorMax = new Vector2(0.4f, 1f);
            hintRt.offsetMin = new Vector2(3f, 0f);
            hintRt.offsetMax = Vector2.zero;
            hint.color = InventoryUiTheme.TextSecondary;
            hint.raycastTarget = false;
        }

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 56f;
        le.preferredHeight = 56f;
        le.minWidth = 52f;
        le.minHeight = 52f;

        var viewGo = go.AddComponent<InventorySlotView>();
        viewGo.Bind(this, 0, isHotbar, bg, icon, label, outline);
        if (!string.IsNullOrEmpty(keyHint))
            viewGo.SetKeyHint(keyHint);
        return viewGo;
    }

    Text CreateText(Transform parent, string content, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = InventoryUiTheme.TextPrimary;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    static void ApplyPanelStyle(GameObject go, Color? tint = null)
    {
        if (go == null)
            return;
        var img = go.GetComponent<Image>();
        if (img == null)
            img = go.AddComponent<Image>();
        Sprite panel = InventoryUiTheme.PanelSprite;
        if (panel != null)
        {
            img.sprite = panel;
            img.type = Image.Type.Sliced;
            img.color = tint ?? InventoryUiTheme.PanelTint;
        }
        else
            img.color = tint ?? InventoryUiTheme.PanelTint;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, anchorMin.y < 0.1f ? 0f : 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
