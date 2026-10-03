using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat HUD: own castle status (mage satiety, standing walls, chronum), ammo for the
/// selected weapon, and a hotkey legend toggled with F1.
/// </summary>
public sealed class CombatHud : MonoBehaviour
{
    const int SortingOrder = 44;
    const float RefreshInterval = 0.2f;
    const string ChronumResource = "Items/ChronumItem";
    static readonly Color SatietyColor = new Color(0.45f, 0.62f, 0.95f, 1f);
    static readonly Color SatietyLowColor = new Color(0.85f, 0.3f, 0.22f, 1f);
    const float SatietyLowFraction = 0.25f;
    // Ниже плашки F8-дебага в правом верхнем углу, чтобы не перекрывать её.
    const float CastlePanelTop = -76f;

    const string LegendText =
        "<b>[I]</b> Inventory   <b>[C]</b> Craft   <b>[B]</b> Build   <b>[M]</b> Map\n" +
        "<b>[E]</b> Use   <b>[1-9]</b> Hotbar   <b>[Esc]</b> Menu   <b>[F1]</b> Hide help";

    PlayerInventory _inventory;
    PlayerInputReader _input;
    ScattergunWeapon _scattergun;
    EmberLauncherWeapon _emberLauncher;
    ItemDefinition _chronum;

    GameObject _castlePanel;
    Image _satietyBar;
    Text _satietyLabel;
    Text _wallsLabel;
    Text _chronumLabel;
    GameObject _ammoRoot;
    Text _ammoLabel;
    GameObject _legend;
    Text _message;
    float _messageUntil;
    float _nextRefresh;
    const float MessageTime = 2.5f;

    void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _input = GetComponent<PlayerInputReader>();
        _chronum = Resources.Load<ItemDefinition>(ChronumResource);
        Build();
    }

    void OnEnable() => GameMessages.Posted += ShowMessage;
    void OnDisable() => GameMessages.Posted -= ShowMessage;

    void ShowMessage(string text)
    {
        _message.text = text;
        _messageUntil = Time.unscaledTime + MessageTime;
    }

    void Update()
    {
        float left = _messageUntil - Time.unscaledTime;
        _message.color = new Color(1f, 0.95f, 0.8f, Mathf.Clamp01(left / 0.5f));

        if (_input != null && _input.HelpTogglePressed)
            _legend.SetActive(!_legend.activeSelf);

        bool hidden = _inventory != null && _inventory.BlocksLook;
        _castlePanel.SetActive(!hidden);
        if (hidden)
            _ammoRoot.SetActive(false);

        if (hidden || Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + RefreshInterval;
        RefreshCastle();
        RefreshAmmo();
    }

    void RefreshCastle()
    {
        OwnMageStation mage = OwnMageStation.Active;
        float fraction = mage != null ? mage.Satiety / Mathf.Max(1f, mage.MaxSatiety) : 0f;
        UiFactory.SetBar(_satietyBar, fraction);
        _satietyBar.color = fraction <= SatietyLowFraction ? SatietyLowColor : SatietyColor;
        _satietyLabel.text = mage != null ? $"Mage  {Mathf.RoundToInt(fraction * 100f)}%" : "Mage  —";

        int total = 0;
        int standing = 0;
        var walls = CarcassWallBreakable.All;
        for (int i = 0; i < walls.Count; i++)
        {
            if (walls[i] == null || !walls[i].IsPlayerOwned)
                continue;
            total++;
            if (!walls[i].IsBreached)
                standing++;
        }

        _wallsLabel.text = $"Walls  {standing}/{total}";
        _wallsLabel.color = standing < total ? InventoryUiTheme.MissingRed : InventoryUiTheme.TextDark;

        int chronum = _inventory != null && _chronum != null ? _inventory.CountItem(_chronum) : 0;
        _chronumLabel.text = $"Chronum  {chronum}";
    }

    void RefreshAmmo()
    {
        // Оружие вешает EnemyCombatBootstrap — может появиться позже этого HUD.
        if (_scattergun == null)
            _scattergun = GetComponent<ScattergunWeapon>();
        if (_emberLauncher == null)
            _emberLauncher = GetComponent<EmberLauncherWeapon>();

        ItemDefinition ammo = null;
        if (_inventory != null)
        {
            ItemKind kind = _inventory.SelectedKind;
            if (kind == ItemKind.Scattergun && _scattergun != null)
                ammo = _scattergun.Ammo;
            else if (kind == ItemKind.EmberLauncher && _emberLauncher != null)
                ammo = _emberLauncher.Ammo;
        }

        _ammoRoot.SetActive(ammo != null);
        if (ammo == null)
            return;
        int count = _inventory.CountItem(ammo);
        _ammoLabel.text = count > 0 ? $"{ammo.DisplayName}  {count}" : $"{ammo.DisplayName}  EMPTY";
        _ammoLabel.color = count > 0 ? new Color(1f, 0.96f, 0.85f) : InventoryUiTheme.MissingRed;
    }

    void Build()
    {
        Transform root = UiFactory.CreateCanvas(transform, "CombatHudCanvas", SortingOrder).transform;

        Image panel = UiFactory.CreatePanel(root, "CastlePanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, CastlePanelTop), new Vector2(260f, 112f));
        _castlePanel = panel.gameObject;
        Transform p = panel.transform;
        _satietyLabel = Line(p, "Satiety", -12f);
        _satietyBar = UiFactory.CreateBar(p, "SatietyBar", new Vector2(18f, -40f), new Vector2(224f, 8f), SatietyColor);
        _wallsLabel = Line(p, "Walls", -54f);
        _chronumLabel = Line(p, "Chronum", -78f);

        RectTransform ammoRt = UiFactory.CreateRect(root, "Ammo", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(34f, -26f), new Vector2(220f, 28f));
        _ammoRoot = ammoRt.gameObject;
        _ammoLabel = UiFactory.CreateText(ammoRt, "AmmoText", 18, Color.white, TextAnchor.UpperLeft);
        _ammoLabel.fontStyle = FontStyle.Bold;
        var shadow = _ammoLabel.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        _ammoRoot.SetActive(false);

        RectTransform messageRt = UiFactory.CreateRect(root, "Message", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 40f));
        _message = UiFactory.CreateText(messageRt, "MessageText", 24, Color.clear, TextAnchor.MiddleCenter);
        _message.fontStyle = FontStyle.Bold;
        _message.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

        Image legend = UiFactory.CreatePanel(root, "Legend", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(520f, 62f));
        _legend = legend.gameObject;
        Text legendText = UiFactory.CreateText(legend.transform, "LegendText", 16, InventoryUiTheme.TextDark, TextAnchor.MiddleCenter);
        legendText.supportRichText = true;
        legendText.text = LegendText;
    }

    static Text Line(Transform parent, string name, float y)
    {
        RectTransform rt = UiFactory.CreateRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(224f, 22f));
        Text text = UiFactory.CreateText(rt, "Text", 17, InventoryUiTheme.TextDark, TextAnchor.UpperLeft);
        text.fontStyle = FontStyle.Bold;
        return text;
    }
}
