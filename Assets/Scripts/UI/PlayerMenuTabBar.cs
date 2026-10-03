using UnityEngine;
using UnityEngine.UI;

/// <summary>Полоса вкладок над меню игрока; видна, пока открыта любая вкладка. Состояние берёт у <see cref="PlayerMenuController"/>.</summary>
public sealed class PlayerMenuTabBar : MonoBehaviour
{
    const int SortingOrder = 300;
    const float TabWidth = 170f;
    const float TabHeight = 46f;
    const float TabGap = 8f;

    static readonly string[] Titles = { "Inventory", "Craft", "Talents" };

    PlayerMenuController _menu;
    PlayerProgression _progression;
    GameObject _root;
    Image[] _tabImages;
    Text _levelText;

    void Awake()
    {
        _menu = GetComponent<PlayerMenuController>();
        _progression = GetComponent<PlayerProgression>();
        Build();
    }

    void Update()
    {
        PlayerMenuTab? current = _menu != null ? _menu.CurrentTab : null;
        if (_root.activeSelf != current.HasValue)
            _root.SetActive(current.HasValue);
        if (!current.HasValue)
            return;

        for (int i = 0; i < _tabImages.Length; i++)
            _tabImages[i].color = i == (int)current.Value ? InventoryUiTheme.TabActive : InventoryUiTheme.ButtonNormal;
        if (_progression != null)
            _levelText.text = $"Lv {_progression.Level}   XP {_progression.Xp}/{_progression.XpToNextLevel}   Points {_progression.TalentPoints}";
    }

    void Build()
    {
        Canvas canvas = UiFactory.CreateCanvas(transform, "PlayerMenuTabsCanvas", SortingOrder);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        _root = canvas.gameObject;

        float totalWidth = Titles.Length * TabWidth + (Titles.Length - 1) * TabGap;
        _tabImages = new Image[Titles.Length];
        for (int i = 0; i < Titles.Length; i++)
        {
            PlayerMenuTab tab = (PlayerMenuTab)i;
            float x = -totalWidth * 0.5f + i * (TabWidth + TabGap);
            RectTransform rt = UiFactory.CreateRect(_root.transform, "Tab_" + Titles[i], new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(x, -20f), new Vector2(TabWidth, TabHeight));
            var image = rt.gameObject.AddComponent<Image>();
            var button = rt.gameObject.AddComponent<Button>();
            InventoryUiTheme.StyleButton(image, button);
            button.onClick.AddListener(() => _menu.OpenTab(tab));
            Text label = UiFactory.CreateText(UiFactory.CreateRect(rt, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TabWidth, TabHeight)), "Text", 20, InventoryUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            label.text = Titles[i];
            label.fontStyle = FontStyle.Bold;
            _tabImages[i] = image;
        }

        _levelText = UiFactory.CreateText(UiFactory.CreateRect(_root.transform, "Level", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(600f, 26f)), "Text", 17, InventoryUiTheme.Accent, TextAnchor.MiddleCenter);
        _root.SetActive(false);
    }
}
