using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// B-menu. Wire canvas/panel/grid/tooltip in the scene or prefab.
/// Catalog icons still spawn into the grid from a button prefab (or runtime fallback).
/// </summary>
public sealed class BuildingMenuUI : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] BuildingController _controller;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] ItemDefinition _stoneItem;

    [Header("Scene / Prefab")]
    [SerializeField] GameObject _canvasGo;
    [SerializeField] GameObject _root;
    [SerializeField] Transform _gridRoot;
    [SerializeField] GameObject _tooltip;
    [SerializeField] Text _tooltipText;
    [SerializeField] Text _stoneCountText;
    [SerializeField] GameObject _iconPrefab;

    Font _font;
    readonly List<BuildingDefinition> _bound = new List<BuildingDefinition>();

    public void Bind(BuildingController controller, PlayerInventory inventory)
    {
        if (_inventory != null)
            _inventory.Changed -= RefreshStoneLabel;

        _controller = controller;
        _inventory = inventory;

        if (isActiveAndEnabled && _inventory != null)
            _inventory.Changed += RefreshStoneLabel;

        RefreshStoneLabel();
    }

    void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<BuildingController>() ?? FindFirstObjectByType<BuildingController>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();
        if (_stoneItem == null)
            _stoneItem = Resources.Load<ItemDefinition>("Items/StoneItem");

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        EnsureEventSystem();

        if (_canvasGo == null || _root == null || _gridRoot == null)
            BuildUiRuntime();

        AutoWireMissing();
        PopulateGrid();
        Show(false);
    }

    void AutoWireMissing()
    {
        if (_canvasGo == null && transform is RectTransform)
            _canvasGo = GetComponentInParent<Canvas>()?.gameObject;
        if (_root == null)
        {
            var t = transform.Find("BuildPanel");
            if (t != null)
                _root = t.gameObject;
        }

        if (_gridRoot == null && _root != null)
        {
            var t = _root.transform.Find("Grid");
            if (t != null)
                _gridRoot = t;
        }

        if (_tooltip == null && _canvasGo != null)
        {
            var t = _canvasGo.transform.Find("BuildTooltip");
            if (t != null)
                _tooltip = t.gameObject;
        }

        if (_tooltipText == null && _tooltip != null)
            _tooltipText = _tooltip.GetComponentInChildren<Text>(true);

        if (_stoneCountText == null && _root != null)
        {
            var t = _root.transform.Find("StoneCount");
            if (t != null)
                _stoneCountText = t.GetComponent<Text>();
        }
    }

    static void EnsureEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Object.Destroy(legacy);
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    void OnEnable()
    {
        if (_inventory != null)
            _inventory.Changed += RefreshStoneLabel;
    }

    void OnDisable()
    {
        if (_inventory != null)
            _inventory.Changed -= RefreshStoneLabel;
    }

    public void Show(bool visible)
    {
        if (_controller == null)
            _controller = GetComponent<BuildingController>() ?? FindFirstObjectByType<BuildingController>();

        if (visible && _bound.Count == 0)
            PopulateGrid();

        if (_canvasGo != null)
            _canvasGo.SetActive(visible);
        if (_root != null)
            _root.SetActive(visible);

        if (visible)
        {
            EnsureEventSystem();
            RefreshStoneLabel();
            HideTooltip();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void RefreshStoneLabel()
    {
        if (_stoneCountText == null || _inventory == null)
            return;
        int n = _stoneItem != null ? _inventory.CountItem(_stoneItem) : 0;
        _stoneCountText.text = $"Stone: {n}";
    }

    void BuildUiRuntime()
    {
        _canvasGo = new GameObject("BuildingCanvas");
        _canvasGo.transform.SetParent(null, false);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        _canvasGo.AddComponent<GraphicRaycaster>();

        _root = CreatePanel(_canvasGo.transform, "BuildPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(560f, 460f));
        var panelImg = _root.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        panelImg.raycastTarget = true;

        var title = CreateText(_root.transform, "Title", "Building", 28, TextAnchor.UpperCenter);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -12f);
        titleRt.sizeDelta = new Vector2(-24f, 36f);

        _stoneCountText = CreateText(_root.transform, "StoneCount", "Stone: 0", 18, TextAnchor.UpperRight);
        var stoneRt = _stoneCountText.rectTransform;
        stoneRt.anchorMin = new Vector2(1f, 1f);
        stoneRt.anchorMax = new Vector2(1f, 1f);
        stoneRt.pivot = new Vector2(1f, 1f);
        stoneRt.anchoredPosition = new Vector2(-16f, -16f);
        stoneRt.sizeDelta = new Vector2(160f, 28f);

        var hint = CreateText(_root.transform, "Hint", "LMB select · RMB / Esc close · I inventory", 14, TextAnchor.LowerCenter);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 10f);
        hintRt.sizeDelta = new Vector2(-20f, 24f);
        hint.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(_root.transform, false);
        _gridRoot = gridGo.transform;
        var gridRt = (RectTransform)gridGo.transform;
        gridRt.anchorMin = new Vector2(0.5f, 0.5f);
        gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.sizeDelta = new Vector2(480f, 300f);
        gridRt.anchoredPosition = new Vector2(0f, 10f);

        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(96f, 96f);
        grid.spacing = new Vector2(12f, 12f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        _tooltip = CreatePanel(_canvasGo.transform, "BuildTooltip",
            new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(240f, 70f));
        var tipBg = _tooltip.AddComponent<Image>();
        tipBg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        tipBg.raycastTarget = false;
        _tooltipText = CreateText(_tooltip.transform, "TipText", "", 16, TextAnchor.MiddleLeft);
        var tipRt = _tooltipText.rectTransform;
        StretchFull(tipRt);
        tipRt.offsetMin = new Vector2(10f, 6f);
        tipRt.offsetMax = new Vector2(-10f, -6f);
        _tooltip.SetActive(false);
    }

    void PopulateGrid()
    {
        if (_gridRoot == null)
            return;

        for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            Destroy(_gridRoot.GetChild(i).gameObject);
        _bound.Clear();

        BuildingDefinition[] entries = null;
        if (_controller != null && _controller.Catalog != null)
            entries = _controller.Catalog.Entries;
        if (entries == null || entries.Length == 0)
        {
            var catalog = Resources.Load<BuildingCatalog>("Building/BuildingCatalog");
            entries = catalog != null ? catalog.Entries : null;
        }

        if (entries == null || entries.Length == 0)
        {
            var foundation = Resources.Load<BuildingDefinition>("Building/Foundation");
            entries = foundation != null ? new[] { foundation } : System.Array.Empty<BuildingDefinition>();
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null)
                continue;
            CreateIcon(_gridRoot, entries[i]);
        }

        if (_bound.Count == 0)
            Debug.LogError("BuildingMenuUI: catalog is empty — check Resources/Building/BuildingCatalog");
    }

    void OnDestroy()
    {
        // Only destroy runtime-created orphan canvas (not scene/prefab owned)
        if (_canvasGo != null && _canvasGo.transform.parent == null && !Application.isEditor)
            Destroy(_canvasGo);
    }

    void CreateIcon(Transform parent, BuildingDefinition def)
    {
        GameObject go;
        if (_iconPrefab != null)
        {
            go = Instantiate(_iconPrefab, parent);
            go.name = def.Id;
        }
        else
        {
            go = new GameObject(def.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
        }

        var img = go.GetComponent<Image>();
        if (img == null)
            img = go.AddComponent<Image>();
        img.color = def.IconColor;
        img.raycastTarget = true;

        if (go.GetComponent<Outline>() == null)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        var cost = go.transform.Find("Cost")?.GetComponent<Text>();
        if (cost == null)
        {
            cost = CreateText(go.transform, "Cost", def.StoneCost.ToString(), 14, TextAnchor.LowerRight);
            var lrt = cost.rectTransform;
            lrt.anchorMin = new Vector2(1f, 0f);
            lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(1f, 0f);
            lrt.anchoredPosition = new Vector2(-4f, 4f);
            lrt.sizeDelta = new Vector2(40f, 20f);
        }
        else
            cost.text = def.StoneCost.ToString();

        var nameShort = go.transform.Find("Name")?.GetComponent<Text>();
        if (nameShort == null)
        {
            nameShort = CreateText(go.transform, "Name", def.DisplayName, 12, TextAnchor.UpperCenter);
            var nrt = nameShort.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = new Vector2(0f, -4f);
            nrt.sizeDelta = new Vector2(-6f, 18f);
        }
        else
            nameShort.text = def.DisplayName;

        var btn = go.GetComponent<Button>();
        if (btn == null)
            btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();

        BuildingDefinition captured = def;
        btn.onClick.AddListener(() =>
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<BuildingController>();
            _controller?.SelectDefinition(captured);
        });

        var trigger = go.GetComponent<BuildIconHover>();
        if (trigger == null)
            trigger = go.AddComponent<BuildIconHover>();
        trigger.Bind(this, def, () =>
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<BuildingController>();
            _controller?.SelectDefinition(captured);
        });

        _bound.Add(def);
    }

    public void ShowTooltip(BuildingDefinition def, Vector2 screenPos)
    {
        if (_tooltip == null || def == null)
            return;
        _tooltip.SetActive(true);
        int have = _stoneItem != null && _inventory != null ? _inventory.CountItem(_stoneItem) : 0;
        if (_tooltipText != null)
            _tooltipText.text = $"{def.DisplayName}\nCost: {def.StoneCost} Stone  ({have} owned)";
        var rt = (RectTransform)_tooltip.transform;
        rt.position = screenPos + new Vector2(18f, -18f);
    }

    public void HideTooltip()
    {
        if (_tooltip != null)
            _tooltip.SetActive(false);
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return go;
    }

    Text CreateText(Transform parent, string name, string value, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

/// <summary>Hover + click helper for build icons.</summary>
public sealed class BuildIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
{
    BuildingMenuUI _ui;
    BuildingDefinition _def;
    System.Action _onClick;

    public void Bind(BuildingMenuUI ui, BuildingDefinition def, System.Action onClick)
    {
        _ui = ui;
        _def = def;
        _onClick = onClick;
    }

    public void OnPointerEnter(PointerEventData eventData) =>
        _ui?.ShowTooltip(_def, eventData.position);

    public void OnPointerMove(PointerEventData eventData) =>
        _ui?.ShowTooltip(_def, eventData.position);

    public void OnPointerExit(PointerEventData eventData) =>
        _ui?.HideTooltip();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        _onClick?.Invoke();
    }
}
