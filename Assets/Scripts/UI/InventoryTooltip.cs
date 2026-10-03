using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Item tooltip that follows the cursor over inventory slots: name, kind, stack, description.
/// One shared overlay, created on first use.
/// </summary>
public sealed class InventoryTooltip : MonoBehaviour
{
    // Поверх инвентаря, сундука и крафта.
    const int SortingOrder = 400;
    const float Width = 320f;
    const float Padding = 14f;
    const float LineGap = 4f;
    static readonly Vector2 CursorOffset = new Vector2(18f, -18f);

    static InventoryTooltip _instance;

    RectTransform _panel;
    Text _title;
    Text _kind;
    Text _description;
    Canvas _canvas;

    public static void Show(ItemDefinition item, int count)
    {
        if (item == null)
            return;
        if (_instance == null)
            _instance = Create();
        _instance.Fill(item, count);
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance._panel.gameObject.SetActive(false);
    }

    static InventoryTooltip Create()
    {
        var go = new GameObject("InventoryTooltip");
        DontDestroyOnLoad(go);
        var tooltip = go.AddComponent<InventoryTooltip>();
        tooltip.Build();
        return tooltip;
    }

    void Fill(ItemDefinition item, int count)
    {
        _title.text = item.DisplayName;
        string stack = item.MaxStack > 1 ? $"   ·   {count} / {item.MaxStack}" : string.Empty;
        _kind.text = KindLabel(item) + stack;
        _description.text = item.Description;
        _description.gameObject.SetActive(!string.IsNullOrEmpty(item.Description));

        float y = Padding;
        y = Place(_title, y);
        y = Place(_kind, y);
        if (_description.gameObject.activeSelf)
            y = Place(_description, y + LineGap);
        _panel.sizeDelta = new Vector2(Width, y + Padding - LineGap);

        _panel.gameObject.SetActive(true);
        FollowCursor();
    }

    float Place(Text text, float top)
    {
        var rt = text.rectTransform;
        float height = text.preferredHeight;
        rt.anchoredPosition = new Vector2(Padding, -top);
        rt.sizeDelta = new Vector2(Width - Padding * 2f, height);
        return top + height + LineGap;
    }

    void LateUpdate()
    {
        if (_panel.gameObject.activeSelf)
            FollowCursor();
    }

    /// <summary>Справа-снизу от курсора; у края экрана переворачиваемся, чтобы тултип не обрезался.</summary>
    void FollowCursor()
    {
        if (Mouse.current == null)
            return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        float scale = _canvas.scaleFactor;
        Vector2 size = _panel.sizeDelta * scale;
        Vector2 offset = CursorOffset * scale;

        bool flipX = mouse.x + offset.x + size.x > Screen.width;
        bool flipY = mouse.y + offset.y - size.y < 0f;
        _panel.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        _panel.position = mouse + new Vector2(flipX ? -offset.x : offset.x, flipY ? -offset.y : offset.y);
    }

    static string KindLabel(ItemDefinition item)
    {
        switch (item.Kind)
        {
            case ItemKind.HandCannon:
            case ItemKind.Crossbow:
            case ItemKind.Staff:
            case ItemKind.Scattergun:
            case ItemKind.EmberLauncher:
                return "Weapon";
            case ItemKind.Pickaxe:
                return "Tool";
            case ItemKind.Resource:
                return "Resource";
            case ItemKind.Furniture:
                return "Furniture";
            default:
                return "Item";
        }
    }

    void Build()
    {
        _canvas = UiFactory.CreateCanvas(transform, "TooltipCanvas", SortingOrder);
        Image panel = UiFactory.CreatePanel(_canvas.transform, "Tooltip", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, 100f));
        _panel = panel.rectTransform;
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = InventoryUiTheme.OutlineGold;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        _title = Line("Title", 20, InventoryUiTheme.TextDark, FontStyle.Bold);
        _kind = Line("Kind", 14, InventoryUiTheme.TextMuted, FontStyle.Italic);
        _description = Line("Description", 16, InventoryUiTheme.TextDark, FontStyle.Normal);
        _description.horizontalOverflow = HorizontalWrapMode.Wrap;
        _panel.gameObject.SetActive(false);
    }

    Text Line(string name, int size, Color color, FontStyle style)
    {
        RectTransform rt = UiFactory.CreateRect(_panel, name, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Width - Padding * 2f, 20f));
        var text = rt.gameObject.AddComponent<Text>();
        text.font = UiFactory.Font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
