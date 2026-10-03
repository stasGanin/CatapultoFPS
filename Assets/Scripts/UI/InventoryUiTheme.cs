using UnityEngine;

/// <summary>Shared beige parchment palette for inventory / chest UI.</summary>
public static class InventoryUiTheme
{
    public static readonly Color PanelTint = new Color(0.96f, 0.93f, 0.88f, 0.94f);
    public static readonly Color SlotNormal = new Color(0.90f, 0.85f, 0.76f, 0.92f);
    public static readonly Color SlotSelected = new Color(0.82f, 0.72f, 0.52f, 0.98f);
    public static readonly Color TextDark = new Color(0.28f, 0.24f, 0.18f, 1f);
    public static readonly Color TextMuted = new Color(0.45f, 0.40f, 0.32f, 0.95f);
    public static readonly Color HotbarPanel = new Color(0.94f, 0.90f, 0.84f, 0.82f);
    public static readonly Color OutlineGold = new Color(0.62f, 0.48f, 0.28f, 1f);
    public static readonly Color ButtonNormal = new Color(0.78f, 0.62f, 0.36f, 1f);
    public static readonly Color ButtonHover = new Color(0.90f, 0.74f, 0.46f, 1f);
    public static readonly Color ButtonPressed = new Color(0.58f, 0.44f, 0.24f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.62f, 0.58f, 0.52f, 0.55f);
    public static readonly Color ReadyGreen = new Color(0.28f, 0.52f, 0.28f, 1f);
    public static readonly Color MissingRed = new Color(0.72f, 0.28f, 0.22f, 1f);

    static Sprite _panel;
    static Sprite _slot;
    static Sprite _craftPanel;
    static Sprite _button;
    static Sprite _tab;

    public static Sprite PanelSprite
    {
        get
        {
            if (_panel == null)
                _panel = Resources.Load<Sprite>("UI/panel_parchment");
            return _panel;
        }
    }

    public static Sprite SlotSprite
    {
        get
        {
            if (_slot == null)
                _slot = Resources.Load<Sprite>("UI/slot_beige");
            return _slot;
        }
    }

    public static Sprite CraftPanelSprite
    {
        get
        {
            if (_craftPanel == null)
                _craftPanel = Resources.Load<Sprite>("UI/panel_craft");
            return _craftPanel != null ? _craftPanel : PanelSprite;
        }
    }

    public static Sprite ButtonSprite
    {
        get
        {
            if (_button == null)
                _button = Resources.Load<Sprite>("UI/button_gold");
            return _button != null ? _button : SlotSprite;
        }
    }

    public static Sprite TabSprite
    {
        get
        {
            if (_tab == null)
                _tab = Resources.Load<Sprite>("UI/tab_beige");
            return _tab != null ? _tab : SlotSprite;
        }
    }
}
