using UnityEngine;

/// <summary>
/// Shared UI palette: neutral dark panels with light frames, light text, one warm accent.
/// Frame sprites are white; the colors below tint them (border = tint, fill = darker tint).
/// </summary>
public static class InventoryUiTheme
{
    public static readonly Color PanelTint = new Color(0.56f, 0.58f, 0.62f, 0.96f);
    public static readonly Color SlotNormal = new Color(0.52f, 0.55f, 0.6f, 0.95f);
    public static readonly Color SlotSelected = new Color(0.96f, 0.78f, 0.42f, 1f);
    public static readonly Color TextPrimary = new Color(0.93f, 0.92f, 0.89f, 1f);
    public static readonly Color TextSecondary = new Color(0.66f, 0.68f, 0.72f, 1f);
    public static readonly Color HotbarPanel = new Color(0.56f, 0.58f, 0.62f, 0.9f);
    public static readonly Color Accent = new Color(0.96f, 0.78f, 0.42f, 1f);
    public static readonly Color ButtonNormal = new Color(0.46f, 0.49f, 0.55f, 1f);
    public static readonly Color ButtonHover = new Color(0.58f, 0.62f, 0.69f, 1f);
    public static readonly Color ButtonPressed = new Color(0.36f, 0.38f, 0.43f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.42f, 0.43f, 0.46f, 0.5f);
    public static readonly Color ReadyGreen = new Color(0.47f, 0.82f, 0.52f, 1f);
    public static readonly Color MissingRed = new Color(0.94f, 0.44f, 0.38f, 1f);
    /// <summary>Затемнение мира под модальными окнами.</summary>
    public static readonly Color ScreenDim = new Color(0.03f, 0.035f, 0.04f, 0.6f);
    /// <summary>Подложка полос (HP, мана, сытость).</summary>
    public static readonly Color BarTrack = new Color(0.1f, 0.11f, 0.12f, 0.9f);
    /// <summary>Активная вкладка: приглушённый акцент, чтобы светлый текст читался.</summary>
    public static readonly Color TabActive = new Color(0.78f, 0.64f, 0.36f, 1f);

    /// <summary>
    /// Наведение/нажатие как множитель к базовому цвету Image (кнопка, слот, вкладка):
    /// один ColorBlock на все виды кнопок, без двойного тонирования.
    /// </summary>
    /// <summary>Окно/панель: рамочный спрайт + нейтральный тон.</summary>
    public static void StylePanel(UnityEngine.UI.Image image)
    {
        image.sprite = PanelSprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = PanelTint;
    }

    /// <summary>Обычная кнопка: рамочный спрайт, базовый цвет и общий отклик на наведение.</summary>
    public static void StyleButton(UnityEngine.UI.Image image, UnityEngine.UI.Button button)
    {
        image.sprite = ButtonSprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = ButtonNormal;
        ApplyInteractionTint(button);
    }

    /// <summary>Плитка с собственным цветом (иконки меню стройки): рамка слота в этом цвете.</summary>
    public static void StyleTile(UnityEngine.UI.Image image, UnityEngine.UI.Button button, Color color)
    {
        image.sprite = SlotSprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = color;
        ApplyInteractionTint(button);
    }

    public static void ApplyInteractionTint(UnityEngine.UI.Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

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
                _panel = Resources.Load<Sprite>("UI/frame_panel");
            return _panel;
        }
    }

    public static Sprite SlotSprite
    {
        get
        {
            if (_slot == null)
                _slot = Resources.Load<Sprite>("UI/frame_slot");
            return _slot;
        }
    }

    public static Sprite CraftPanelSprite
    {
        get
        {
            if (_craftPanel == null)
                _craftPanel = Resources.Load<Sprite>("UI/frame_panel_large");
            return _craftPanel != null ? _craftPanel : PanelSprite;
        }
    }

    public static Sprite ButtonSprite
    {
        get
        {
            if (_button == null)
                _button = Resources.Load<Sprite>("UI/frame_button");
            return _button != null ? _button : SlotSprite;
        }
    }

    public static Sprite TabSprite
    {
        get
        {
            if (_tab == null)
                _tab = Resources.Load<Sprite>("UI/frame_tab");
            return _tab != null ? _tab : SlotSprite;
        }
    }
}
