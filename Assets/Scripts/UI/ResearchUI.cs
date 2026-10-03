using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Research tree opened from the research table: columns by tier, click a node to pay and learn it.
/// </summary>
public sealed class ResearchUI : MonoBehaviour
{
    const int SortingOrder = 160;
    const float ColumnWidth = 280f;
    const float NodeHeight = 132f;
    const float NodeGap = 14f;
    static readonly Color Researched = new Color(0.55f, 0.75f, 0.5f, 1f);
    static readonly Color Available = new Color(0.92f, 0.8f, 0.5f, 1f);
    static readonly Color Locked = new Color(0.6f, 0.57f, 0.52f, 0.85f);

    static ResearchUI _instance;

    PlayerInputReader _input;
    PlayerInventory _inventory;
    PlayerRecipeBook _book;
    ResearchTree _tree;
    GameObject _root;
    RectTransform _nodesRoot;
    readonly List<NodeView> _views = new List<NodeView>();
    bool _open;
    // Кадр открытия: тот же E, что открыл окно, не должен сразу его закрыть.
    int _openedFrame = -1;

    struct NodeView
    {
        public ResearchNode Node;
        public Image Background;
        public Text Status;
        public Button Button;
    }

    public static bool IsOpen => _instance != null && _instance._open;

    public static void Open()
    {
        if (_instance != null)
            _instance.OpenInternal();
        else
            Debug.LogError("ResearchUI: no instance on the player.");
    }

    public static void CloseIfOpen()
    {
        if (_instance != null)
            _instance.Close();
    }

    void Awake()
    {
        _instance = this;
        _input = GetComponent<PlayerInputReader>();
        _inventory = GetComponent<PlayerInventory>();
        _tree = Resources.Load<ResearchTree>(ResearchTree.ResourcePath);
        if (_tree == null)
            Debug.LogError($"ResearchUI: no ResearchTree at Resources/{ResearchTree.ResourcePath}.");
        Build();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        if (!_open || _input == null)
            return;
        // Тот же E, которым открыли, закрывает — как у мага и сундука.
        if (_input.CancelPressed || _input.InteractPressed)
            Close();
    }

    void OpenInternal()
    {
        if (_open)
            return;
        if (_book == null)
            _book = GetComponent<PlayerRecipeBook>();

        _inventory?.CloseMenuAndStorage();
        OwnMageStation.CloseOpen();
        GetComponent<CraftMenuController>()?.Close();
        GetComponent<CastleBuildController>()?.CloseAll();
        WorldMapUI.CloseIfOpen();
        SettingsMenuUI.CloseIfOpen();

        _open = true;
        _openedFrame = Time.frameCount;
        RebuildNodes();
        _root.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _inventory?.NotifyGameplayBlockChanged();
    }

    void LateUpdate()
    {
        // Ресурсы могли прийти в инвентарь, пока окно открыто.
        if (_open)
            RefreshNodes();
    }

    public void Close()
    {
        if (!_open || Time.frameCount == _openedFrame)
            return;
        _open = false;
        _root.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _inventory?.NotifyGameplayBlockChanged();
    }

    void RebuildNodes()
    {
        for (int i = _nodesRoot.childCount - 1; i >= 0; i--)
            Destroy(_nodesRoot.GetChild(i).gameObject);
        _views.Clear();
        if (_tree == null)
            return;

        var perTier = new Dictionary<int, int>();
        foreach (var node in _tree.Nodes)
        {
            if (node == null)
                continue;
            perTier.TryGetValue(node.Tier, out int row);
            perTier[node.Tier] = row + 1;
            var pos = new Vector2(node.Tier * (ColumnWidth + NodeGap), -row * (NodeHeight + NodeGap));
            _views.Add(CreateNode(node, pos));
        }

        RefreshNodes();
    }

    NodeView CreateNode(ResearchNode node, Vector2 position)
    {
        RectTransform rt = UiFactory.CreateRect(_nodesRoot, node.Id, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(ColumnWidth, NodeHeight));
        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = InventoryUiTheme.SlotSprite;
        bg.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() => TryResearch(node));

        Text title = Label(rt, "Title", 18, FontStyle.Bold, new Vector2(10f, -8f), node.DisplayName);
        title.color = InventoryUiTheme.TextDark;
        Label(rt, "Unlocks", 13, FontStyle.Italic, new Vector2(10f, -32f), "Unlocks: " + RecipeNames(node)).color = InventoryUiTheme.TextMuted;
        Label(rt, "Cost", 14, FontStyle.Normal, new Vector2(10f, -54f), "Cost: " + CostText(node)).color = InventoryUiTheme.TextDark;
        Text status = Label(rt, "Status", 14, FontStyle.Bold, new Vector2(10f, -100f), string.Empty);

        return new NodeView { Node = node, Background = bg, Status = status, Button = button };
    }

    void RefreshNodes()
    {
        foreach (var v in _views)
        {
            ResearchNode node = v.Node;
            if (node.IsResearched(_book))
                SetState(v, Researched, "Researched", InventoryUiTheme.ReadyGreen, false);
            else if (!node.PrerequisitesMet(_book))
                SetState(v, Locked, "Requires: " + PrereqNames(node), InventoryUiTheme.MissingRed, false);
            else if (!node.CanAfford(_inventory))
                SetState(v, Available, "Not enough resources", InventoryUiTheme.MissingRed, false);
            else
                SetState(v, Available, "Click to research", InventoryUiTheme.ReadyGreen, true);
        }
    }

    static void SetState(NodeView v, Color background, string status, Color statusColor, bool interactable)
    {
        v.Background.color = background;
        v.Status.text = status;
        v.Status.color = statusColor;
        v.Button.interactable = interactable;
    }

    void TryResearch(ResearchNode node)
    {
        if (!node.TryResearch(_inventory, _book))
            return;
        GameMessages.Post($"Researched: {node.DisplayName}");
        RefreshNodes();
    }

    static string RecipeNames(ResearchNode node)
    {
        var names = new List<string>();
        foreach (var r in node.Unlocks)
        {
            if (r != null)
                names.Add(r.DisplayName);
        }

        return string.Join(", ", names);
    }

    static string PrereqNames(ResearchNode node)
    {
        var names = new List<string>();
        foreach (var p in node.Prerequisites)
        {
            if (p != null)
                names.Add(p.DisplayName);
        }

        return string.Join(", ", names);
    }

    static string CostText(ResearchNode node)
    {
        var parts = new List<string>();
        foreach (var c in node.Cost)
        {
            if (c.Item != null)
                parts.Add($"{c.Item.DisplayName} {c.Count}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "free";
    }

    static Text Label(RectTransform parent, string name, int size, FontStyle style, Vector2 position, string value)
    {
        RectTransform rt = UiFactory.CreateRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(ColumnWidth - 20f, 22f));
        var text = rt.gameObject.AddComponent<Text>();
        text.font = UiFactory.Font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    void Build()
    {
        Canvas canvas = UiFactory.CreateCanvas(transform, "ResearchCanvas", SortingOrder);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        _root = canvas.gameObject;

        Image panel = UiFactory.CreatePanel(_root.transform, "ResearchPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 620f));
        panel.raycastTarget = true;
        Text title = UiFactory.CreateText(UiFactory.CreateRect(panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 40f)), "Text", 28, InventoryUiTheme.TextDark, TextAnchor.MiddleCenter);
        title.text = "Research";
        title.fontStyle = FontStyle.Bold;
        Text hint = UiFactory.CreateText(UiFactory.CreateRect(panel.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(900f, 24f)), "Text", 15, InventoryUiTheme.TextMuted, TextAnchor.MiddleCenter);
        hint.text = "Click a node to research   ·   E / Esc close";

        _nodesRoot = UiFactory.CreateRect(panel.transform, "Nodes", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -80f), new Vector2(920f, 480f));
        _root.SetActive(false);
    }
}
