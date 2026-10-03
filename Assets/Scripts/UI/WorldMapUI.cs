using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>World map overlay (M). Pads, routes, and the player marker.</summary>
[DefaultExecutionOrder(40)]
public sealed class WorldMapUI : MonoBehaviour
{
    static WorldMapUI _instance;

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;

    Font _font;
    GameObject _root;
    RectTransform _plot;
    RectTransform _playerMark;
    Text _hint;
    bool _open;

    float _minX;
    float _maxX;
    float _minZ;
    float _maxZ;

    public static bool IsOpen => _instance != null && _instance._open;

    public static void CloseIfOpen()
    {
        if (_instance != null && _instance._open)
            _instance.Close();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;
        if (player.GetComponent<WorldMapUI>() == null)
            player.AddComponent<WorldMapUI>();
    }

    void Awake()
    {
        _instance = this;
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureEventSystem();
        Build();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        if (_input == null)
            return;

        if (_input.MapPressed)
        {
            Toggle();
            return;
        }

        if (_open && _input.CancelPressed)
            Close();
    }

    void LateUpdate()
    {
        if (!_open || _playerMark == null)
            return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;
        _playerMark.anchoredPosition = WorldToPlot(player.transform.position);
        Vector3 fwd = player.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.01f)
            _playerMark.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
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
        GetComponent<CraftMenuController>()?.Close();
        GetComponent<BuildingController>()?.CloseAll();
        GetComponent<CastleBuildController>()?.CloseAll();
        SettingsMenuUI.CloseIfOpen();

        _open = true;
        RebuildPlot();
        SetVisible(true);
        ApplyCursor();
        _inventory?.NotifyGameplayBlockChanged();
    }

    public void Close()
    {
        if (!_open)
            return;
        _open = false;
        SetVisible(false);
        ApplyCursor();
        _inventory?.NotifyGameplayBlockChanged();
    }

    void SetVisible(bool on)
    {
        if (_root != null)
            _root.SetActive(on);
    }

    void ApplyCursor()
    {
        if (_open || SettingsMenuUI.IsOpen || (_inventory != null && (_inventory.IsMenuOpen || _inventory.IsCraftOpen || _inventory.IsMageOpen)))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void RebuildPlot()
    {
        if (_plot == null)
            return;

        for (int i = _plot.childCount - 1; i >= 0; i--)
        {
            var child = _plot.GetChild(i);
            if (child != null && child.gameObject != _playerMark.gameObject)
                Destroy(child.gameObject);
        }

        var nodes = FindObjectsByType<MapNodeMarker>(FindObjectsSortMode.None);
        ComputeBounds(nodes);

        for (int i = 0; i < nodes.Length; i++)
        {
            var a = nodes[i];
            if (a == null)
                continue;
            int[] links = a.LinkedNodeIndices;
            if (links == null)
                continue;
            for (int l = 0; l < links.Length; l++)
            {
                int idx = links[l];
                if (idx < 0 || idx >= nodes.Length || idx == i)
                    continue;
                DrawLink(WorldToPlot(a.transform.position), WorldToPlot(nodes[idx].transform.position));
            }
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            MapNodeMarker nearest = null;
            float best = float.MaxValue;
            for (int j = 0; j < nodes.Length; j++)
            {
                if (i == j || nodes[j] == null)
                    continue;
                float d = (nodes[i].transform.position - nodes[j].transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = nodes[j];
                }
            }

            if (nearest != null && i < IndexOf(nodes, nearest))
                DrawLink(WorldToPlot(nodes[i].transform.position), WorldToPlot(nearest.transform.position));
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null)
                continue;
            CreateNode(node);
        }

        if (_playerMark != null)
            _playerMark.SetAsLastSibling();
    }

    static int IndexOf(MapNodeMarker[] nodes, MapNodeMarker marker)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == marker)
                return i;
        }

        return 0;
    }

    void ComputeBounds(MapNodeMarker[] nodes)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 p = player != null ? player.transform.position : Vector3.zero;
        _minX = p.x;
        _maxX = p.x;
        _minZ = p.z;
        _maxZ = p.z;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null)
                continue;
            Vector3 w = nodes[i].transform.position;
            _minX = Mathf.Min(_minX, w.x);
            _maxX = Mathf.Max(_maxX, w.x);
            _minZ = Mathf.Min(_minZ, w.z);
            _maxZ = Mathf.Max(_maxZ, w.z);
        }

        float pad = 18f;
        _minX -= pad;
        _maxX += pad;
        _minZ -= pad;
        _maxZ += pad;
        if (_maxX - _minX < 40f)
        {
            float mid = (_minX + _maxX) * 0.5f;
            _minX = mid - 20f;
            _maxX = mid + 20f;
        }

        if (_maxZ - _minZ < 40f)
        {
            float mid = (_minZ + _maxZ) * 0.5f;
            _minZ = mid - 20f;
            _maxZ = mid + 20f;
        }
    }

    Vector2 WorldToPlot(Vector3 world)
    {
        RectTransform rt = _plot;
        float w = rt.rect.width;
        float h = rt.rect.height;
        float nx = Mathf.InverseLerp(_minX, _maxX, world.x);
        float nz = Mathf.InverseLerp(_minZ, _maxZ, world.z);
        return new Vector2((nx - 0.5f) * w, (nz - 0.5f) * h);
    }

    void DrawLink(Vector2 a, Vector2 b)
    {
        var go = new GameObject("Link", typeof(RectTransform));
        go.transform.SetParent(_plot, false);
        var rt = (RectTransform)go.transform;
        Vector2 delta = b - a;
        float len = delta.magnitude;
        rt.sizeDelta = new Vector2(Mathf.Max(4f, len), 3f);
        rt.anchoredPosition = (a + b) * 0.5f;
        float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, ang);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.55f, 0.68f, 0.82f, 0.55f);
        img.raycastTarget = false;
    }

    void CreateNode(MapNodeMarker node)
    {
        var go = new GameObject(node.NodeId, typeof(RectTransform));
        go.transform.SetParent(_plot, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(18f, 18f);
        rt.anchoredPosition = WorldToPlot(node.transform.position);
        var img = go.AddComponent<Image>();
        img.color = NodeColor(node.Kind);
        img.raycastTarget = false;

        var label = CreateText(go.transform, NodeLabel(node), 13, TextAnchor.UpperCenter);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0.5f, 0f);
        lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -4f);
        lrt.sizeDelta = new Vector2(140f, 32f);
        label.color = new Color(0.93f, 0.95f, 0.98f, 1f);
    }

    static Color NodeColor(MapNodeKind kind)
    {
        return kind switch
        {
            MapNodeKind.PlayerCastle => new Color(0.22f, 0.62f, 0.32f, 1f),
            MapNodeKind.BossCastle => new Color(0.72f, 0.16f, 0.12f, 1f),
            MapNodeKind.Settlement => new Color(0.82f, 0.66f, 0.22f, 1f),
            _ => new Color(0.72f, 0.32f, 0.28f, 1f),
        };
    }

    static string NodeLabel(MapNodeMarker node)
    {
        string kind = node.Kind switch
        {
            MapNodeKind.PlayerCastle => "Home",
            MapNodeKind.BossCastle => "Boss",
            MapNodeKind.Settlement => "Town",
            _ => "Enemy",
        };
        if (string.IsNullOrEmpty(node.NodeId) || node.NodeId == "node")
            return kind;
        return $"{kind}\n{node.NodeId}";
    }

    void Build()
    {
        var canvasGo = new GameObject("WorldMapCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("MapRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)_root.transform);

        var dim = new GameObject("Dimmer", typeof(RectTransform));
        dim.transform.SetParent(_root.transform, false);
        Stretch((RectTransform)dim.transform);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);
        var dimBtn = dim.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        dimBtn.onClick.AddListener(Close);

        var panel = new GameObject("MapPanel", typeof(RectTransform));
        panel.transform.SetParent(_root.transform, false);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(980f, 720f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.10f, 0.13f, 0.18f, 0.98f);

        var title = CreateText(panel.transform, "World Map", 26, TextAnchor.MiddleLeft);
        PinTop(title.rectTransform, 36f, 18f, 120f, 36f);
        title.fontStyle = FontStyle.Bold;
        title.color = Color.white;

        var close = CreateButton(panel.transform, "X", Close);
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-24f, -18f);
        closeRt.sizeDelta = new Vector2(40f, 40f);

        var plotGo = new GameObject("Plot", typeof(RectTransform));
        plotGo.transform.SetParent(panel.transform, false);
        _plot = (RectTransform)plotGo.transform;
        _plot.anchorMin = new Vector2(0.04f, 0.12f);
        _plot.anchorMax = new Vector2(0.96f, 0.86f);
        _plot.offsetMin = Vector2.zero;
        _plot.offsetMax = Vector2.zero;
        var plotImg = plotGo.AddComponent<Image>();
        plotImg.color = new Color(0.16f, 0.20f, 0.26f, 0.96f);

        var markGo = new GameObject("Player", typeof(RectTransform));
        markGo.transform.SetParent(_plot, false);
        _playerMark = (RectTransform)markGo.transform;
        _playerMark.sizeDelta = new Vector2(16f, 16f);
        var markImg = markGo.AddComponent<Image>();
        markImg.color = new Color(0.12f, 0.28f, 0.72f, 1f);
        markImg.raycastTarget = false;

        _hint = CreateText(panel.transform, "M / Esc  close    ·    blue marker is you    ·    green home · red enemy pads", 14, TextAnchor.MiddleCenter);
        PinBottom(_hint.rectTransform, 24f, 16f, 24f, 28f);
        _hint.color = new Color(0.72f, 0.76f, 0.82f, 1f);

        var legend = CreateText(panel.transform, "Green home   ·   Red enemy   ·   Gold town   ·   Dark boss", 13, TextAnchor.MiddleRight);
        PinTop(legend.rectTransform, 220f, 22f, 80f, 28f);
        legend.color = new Color(0.72f, 0.76f, 0.82f, 1f);
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.48f, 0.82f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var text = CreateText(go.transform, label, 16, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        text.color = Color.white;
        return btn;
    }

    Text CreateText(Transform parent, string value, int size, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.alignment = align;
        text.color = new Color(0.93f, 0.95f, 0.98f, 1f);
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void PinTop(RectTransform rt, float left, float top, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void PinBottom(RectTransform rt, float left, float bottom, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, bottom + height);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }
}
