using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>B-menu: section, solid wall, window, door.</summary>
public sealed class CastleModuleMenuUI : MonoBehaviour
{
    [SerializeField] CastleBuildController _controller;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] ItemDefinition _stoneItem;
    [SerializeField] CastleModuleDefinition[] _modules;

    GameObject _canvasGo;
    GameObject _root;
    Text _stoneLabel;
    Font _font;

    void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<CastleBuildController>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_stoneItem == null)
            _stoneItem = Resources.Load<ItemDefinition>("Items/StoneItem");
        if (_modules == null || _modules.Length == 0)
        {
            _modules = LoadBuildableModules();
        }

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        EnsureEventSystem();
        BuildUi();
        Show(false);
    }

    void OnEnable()
    {
        if (_inventory != null)
            _inventory.Changed += RefreshStone;
    }

    void OnDisable()
    {
        if (_inventory != null)
            _inventory.Changed -= RefreshStone;
    }

    public void Show(bool visible)
    {
        if (_canvasGo != null)
            _canvasGo.SetActive(visible);
        if (_root != null)
            _root.SetActive(visible);
        if (visible)
        {
            RefreshStone();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void RefreshStone()
    {
        if (_stoneLabel == null || _inventory == null)
            return;
        int n = _stoneItem != null ? _inventory.CountItem(_stoneItem) : 0;
        _stoneLabel.text = $"Stone: {n}";
    }

    void BuildUi()
    {
        _canvasGo = new GameObject("CastleModuleCanvas");
        _canvasGo.transform.SetParent(null, false);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;
        UiScale.Configure(_canvasGo.AddComponent<CanvasScaler>());
        _canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("Panel", typeof(RectTransform));
        _root.transform.SetParent(_canvasGo.transform, false);
        var rt = (RectTransform)_root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 400f);
        var bg = _root.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

        var title = CreateText(_root.transform, "Castle", 26, TextAnchor.UpperCenter);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -14f);
        titleRt.sizeDelta = new Vector2(-20f, 36f);

        _stoneLabel = CreateText(_root.transform, "Stone: 0", 18, TextAnchor.UpperRight);
        var stoneRt = _stoneLabel.rectTransform;
        stoneRt.anchorMin = stoneRt.anchorMax = new Vector2(1f, 1f);
        stoneRt.pivot = new Vector2(1f, 1f);
        stoneRt.anchoredPosition = new Vector2(-16f, -16f);
        stoneRt.sizeDelta = new Vector2(140f, 28f);

        var hint = CreateText(_root.transform, "LMB select · R rotate station · RMB / Esc close", 14, TextAnchor.LowerCenter);
        hint.color = new Color(0.7f, 0.7f, 0.7f);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 12f);
        hintRt.sizeDelta = new Vector2(-20f, 24f);

        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(_root.transform, false);
        var gridRt = (RectTransform)gridGo.transform;
        gridRt.anchorMin = gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.anchoredPosition = new Vector2(0f, 8f);
        gridRt.sizeDelta = new Vector2(540f, 252f);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(96f, 96f);
        grid.spacing = new Vector2(12f, 12f);
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < _modules.Length; i++)
        {
            if (_modules[i] != null)
                CreateIcon(gridGo.transform, _modules[i]);
        }

        foreach (var station in LoadStations())
            CreateStationIcon(gridGo.transform, station);

        CreateDemolishIcon(gridGo.transform);
    }

    void CreateIcon(Transform parent, CastleModuleDefinition def)
    {
        var go = new GameObject(def.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = def.IconColor;
        img.raycastTarget = true;

        var cost = CreateText(go.transform, def.StoneCost.ToString(), 14, TextAnchor.LowerRight);
        var crt = cost.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-6f, 6f);
        crt.sizeDelta = new Vector2(40f, 22f);

        var name = CreateText(go.transform, def.DisplayName, 13, TextAnchor.UpperCenter);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0f, 1f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, -4f);
        nrt.sizeDelta = new Vector2(-8f, 20f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        CastleModuleDefinition captured = def;
        btn.onClick.AddListener(() =>
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<CastleBuildController>();
            _controller?.SelectModule(captured);
        });
    }

    public CastleModuleDefinition FindDefinition(CastleModuleKind kind)
    {
        if (_modules == null)
            _modules = LoadBuildableModules();
        for (int i = 0; i < _modules.Length; i++)
        {
            if (_modules[i] != null && _modules[i].Kind == kind)
                return _modules[i];
        }

        return null;
    }

    static StationDefinition[] LoadStations()
    {
        var stations = Resources.LoadAll<StationDefinition>("Castle/Stations");
        System.Array.Sort(stations, (a, b) => a.Kind.CompareTo(b.Kind));
        return stations;
    }

    void CreateStationIcon(Transform parent, StationDefinition def)
    {
        var go = new GameObject(def.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = def.IconColor;

        var name = CreateText(go.transform, def.DisplayName, 13, TextAnchor.UpperCenter);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0f, 1f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, -4f);
        nrt.sizeDelta = new Vector2(-6f, 20f);

        var cost = CreateText(go.transform, def.CostLabel(), 11, TextAnchor.LowerCenter);
        var crt = cost.rectTransform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = new Vector2(1f, 0.7f);
        crt.offsetMin = new Vector2(3f, 4f);
        crt.offsetMax = new Vector2(-3f, 0f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<CastleBuildController>();
            _controller?.SelectStation(def);
        });
    }

    void CreateDemolishIcon(Transform parent)
    {
        var go = new GameObject("demolish", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.62f, 0.2f, 0.16f, 1f);

        var name = CreateText(go.transform, "Demolish", 13, TextAnchor.MiddleCenter);
        var nrt = name.rectTransform;
        nrt.anchorMin = Vector2.zero;
        nrt.anchorMax = Vector2.one;
        nrt.sizeDelta = Vector2.zero;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<CastleBuildController>();
            _controller?.SelectDemolish();
        });
    }

    Text CreateText(Transform parent, string value, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = anchor;
        text.text = value;
        text.raycastTarget = false;
        return text;
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
                Destroy(legacy);
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    static bool IsBuildTabKind(CastleModuleKind kind)
    {
        return kind == CastleModuleKind.Section
               || kind == CastleModuleKind.Wall
               || kind == CastleModuleKind.Window
               || kind == CastleModuleKind.Door;
    }

    static int SortKey(CastleModuleKind kind)
    {
        switch (kind)
        {
            case CastleModuleKind.Section: return 0;
            case CastleModuleKind.Wall: return 1;
            case CastleModuleKind.Window: return 2;
            case CastleModuleKind.Door: return 3;
            default: return 9;
        }
    }

    static CastleModuleDefinition[] LoadBuildableModules()
    {
        var all = Resources.LoadAll<CastleModuleDefinition>("Castle/Modules");
        var usable = new List<CastleModuleDefinition>();
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && IsBuildTabKind(all[i].Kind))
                    usable.Add(all[i]);
            }
        }

        usable.Sort((a, b) => SortKey(a.Kind).CompareTo(SortKey(b.Kind)));
        if (usable.Count == 0)
            Debug.LogError("CastleModuleMenuUI: no Section/Wall/Window/Door recipes in Resources/Castle/Modules");
        return usable.ToArray();
    }

    void OnDestroy()
    {
        if (_canvasGo != null)
            Destroy(_canvasGo);
    }
}
