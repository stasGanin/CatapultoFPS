using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Вкладка «Таланты»: строка на ветку, колонка на глубину; клик по узлу тратит очко таланта.</summary>
public sealed class TalentsUI : MonoBehaviour
{
    const int SortingOrder = 150;
    const float LabelWidth = 150f;
    const float NodeWidth = 250f;
    const float NodeHeight = 130f;
    const float NodeGap = 16f;
    static readonly Color Maxed = new Color(0.55f, 0.75f, 0.5f, 1f);
    static readonly Color Available = new Color(0.92f, 0.8f, 0.5f, 1f);
    static readonly Color Locked = new Color(0.6f, 0.57f, 0.52f, 0.85f);

    struct NodeView
    {
        public TalentNode Node;
        public Image Background;
        public Text Rank;
        public Text Status;
        public Button Button;
    }

    PlayerInputReader _input;
    PlayerInventory _inventory;
    PlayerTalents _talents;
    PlayerProgression _progression;
    GameObject _root;
    RectTransform _nodesRoot;
    Text _summary;
    readonly List<NodeView> _views = new List<NodeView>();
    bool _open;

    public bool IsOpen => _open;

    void Awake()
    {
        _input = GetComponent<PlayerInputReader>();
        _inventory = GetComponent<PlayerInventory>();
        _talents = GetComponent<PlayerTalents>();
        _progression = GetComponent<PlayerProgression>();
        Build();
    }

    void OnEnable()
    {
        if (_talents != null)
            _talents.Changed += RefreshNodes;
        if (_progression != null)
            _progression.Changed += RefreshNodes;
    }

    void OnDisable()
    {
        if (_talents != null)
            _talents.Changed -= RefreshNodes;
        if (_progression != null)
            _progression.Changed -= RefreshNodes;
    }

    void Update()
    {
        if (!_open)
            return;
        if (_input != null && _input.CancelPressed)
            Close();
    }

    public void Open()
    {
        if (_open)
            return;
        _open = true;
        _root.SetActive(true);
        RefreshNodes();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _inventory?.NotifyGameplayBlockChanged();
    }

    /// <summary>relockCursor=false — когда окно закрыли ради другого меню, которому курсор нужен свободным.</summary>
    public void Close(bool relockCursor = true)
    {
        if (!_open)
            return;
        _open = false;
        _root.SetActive(false);
        if (relockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        _inventory?.NotifyGameplayBlockChanged();
    }

    void Build()
    {
        Canvas canvas = UiFactory.CreateCanvas(transform, "TalentsCanvas", SortingOrder);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        _root = canvas.gameObject;

        Image panel = UiFactory.CreatePanel(_root.transform, "TalentsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1180f, 640f));
        panel.raycastTarget = true;
        Text title = UiFactory.CreateText(UiFactory.CreateRect(panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 40f)), "Text", 28, InventoryUiTheme.TextPrimary, TextAnchor.MiddleCenter);
        title.text = "Talents";
        title.fontStyle = FontStyle.Bold;
        _summary = UiFactory.CreateText(UiFactory.CreateRect(panel.transform, "Summary", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(900f, 28f)), "Text", 18, InventoryUiTheme.Accent, TextAnchor.MiddleCenter);
        Text hint = UiFactory.CreateText(UiFactory.CreateRect(panel.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 24f)), "Text", 15, InventoryUiTheme.TextSecondary, TextAnchor.MiddleCenter);
        hint.text = "Click a talent to spend a point   ·   XP comes from XP fruits   ·   Tab / Esc close";

        _nodesRoot = UiFactory.CreateRect(panel.transform, "Nodes", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -120f), new Vector2(1060f, 440f));
        BuildNodes();
        _root.SetActive(false);
    }

    void BuildNodes()
    {
        TalentTree tree = _talents != null ? _talents.Tree : null;
        if (tree == null)
            return;

        var branches = new List<string>();
        foreach (var node in tree.Nodes)
        {
            int row = branches.IndexOf(node.Branch);
            if (row < 0)
            {
                row = branches.Count;
                branches.Add(node.Branch);
                CreateBranchLabel(node.Branch, row);
            }

            var pos = new Vector2(LabelWidth + node.Depth * (NodeWidth + NodeGap), -row * (NodeHeight + NodeGap));
            _views.Add(CreateNode(node, pos));
        }
    }

    void CreateBranchLabel(string branch, int row)
    {
        RectTransform rt = UiFactory.CreateRect(_nodesRoot, "Branch_" + branch, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -row * (NodeHeight + NodeGap)), new Vector2(LabelWidth - 10f, NodeHeight));
        Text text = UiFactory.CreateText(rt, "Text", 22, InventoryUiTheme.Accent, TextAnchor.MiddleLeft);
        text.fontStyle = FontStyle.Bold;
        text.text = branch;
    }

    NodeView CreateNode(TalentNode node, Vector2 position)
    {
        RectTransform rt = UiFactory.CreateRect(_nodesRoot, node.Id, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(NodeWidth, NodeHeight));
        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = InventoryUiTheme.SlotSprite;
        bg.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() => TryBuy(node));

        Label(rt, "Title", 18, FontStyle.Bold, new Vector2(10f, -8f), node.DisplayName).color = InventoryUiTheme.TextPrimary;
        Text rank = Label(rt, "Rank", 16, FontStyle.Bold, new Vector2(10f, -32f), string.Empty);
        rank.color = InventoryUiTheme.Accent;
        Label(rt, "Description", 14, FontStyle.Italic, new Vector2(10f, -56f), node.Description).color = InventoryUiTheme.TextSecondary;
        Text status = Label(rt, "Status", 14, FontStyle.Bold, new Vector2(10f, -102f), string.Empty);

        return new NodeView { Node = node, Background = bg, Rank = rank, Status = status, Button = button };
    }

    void RefreshNodes()
    {
        if (_summary != null && _progression != null)
            _summary.text = $"Level {_progression.Level}   ·   XP {_progression.Xp}/{_progression.XpToNextLevel}   ·   Talent points: {_progression.TalentPoints}";

        foreach (var v in _views)
        {
            int rank = _talents.GetRank(v.Node);
            v.Rank.text = $"Rank {rank}/{v.Node.MaxRank}";
            if (rank >= v.Node.MaxRank)
                SetState(v, Maxed, "Maxed", InventoryUiTheme.ReadyGreen, false);
            else if (!_talents.PrerequisitesMet(v.Node))
                SetState(v, Locked, "Requires: " + PrerequisiteNames(v.Node), InventoryUiTheme.MissingRed, false);
            else if (_progression == null || _progression.TalentPoints <= 0)
                SetState(v, Available, "No talent points", InventoryUiTheme.MissingRed, false);
            else
                SetState(v, Available, "Click to learn (1 point)", InventoryUiTheme.ReadyGreen, true);
        }
    }

    static void SetState(NodeView v, Color background, string status, Color statusColor, bool interactable)
    {
        v.Background.color = background;
        v.Status.text = status;
        v.Status.color = statusColor;
        v.Button.interactable = interactable;
    }

    void TryBuy(TalentNode node)
    {
        if (_talents.TryBuy(node))
            GameMessages.Post($"Talent: {node.DisplayName} {_talents.GetRank(node)}/{node.MaxRank}");
    }

    string PrerequisiteNames(TalentNode node)
    {
        var names = new List<string>();
        foreach (string id in node.PrerequisiteIds)
        {
            TalentNode prerequisite = _talents.Tree.Find(id);
            if (prerequisite != null)
                names.Add(prerequisite.DisplayName);
        }

        return string.Join(", ", names);
    }

    static Text Label(RectTransform parent, string name, int size, FontStyle style, Vector2 position, string value)
    {
        RectTransform rt = UiFactory.CreateRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(NodeWidth - 20f, 44f));
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
}
