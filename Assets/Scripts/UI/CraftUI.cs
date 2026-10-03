using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Craft window (C). List + detail card, same parchment language as inventory.
/// Hotkeys: C / Esc close · click recipe · Enter craft · Tab category.
/// </summary>
[DefaultExecutionOrder(-10)]
public sealed class CraftUI : MonoBehaviour
{
    [SerializeField] CraftMenuController _menu;
    [SerializeField] PlayerInventory _inventory;

    Font _font;
    GameObject _root;
    Transform _listRoot;
    Text _title;
    Text _desc;
    Text _outputLabel;
    Text _ingredients;
    Text _qtyLabel;
    Text _hint;
    Text _status;
    Image _outputIcon;
    Button _craftButton;
    Text _craftLabel;
    readonly List<Row> _rows = new();
    readonly List<Button> _tabButtons = new();

    CraftCategory _category = CraftCategory.All;
    CraftRecipe _selected;
    int _qty = 1;
    bool _built;

    struct Row
    {
        public CraftRecipe Recipe;
        public Button Button;
        public Image Background;
        public Text Name;
        public Text State;
        public Image Icon;
    }

    public void Bind(CraftMenuController menu)
    {
        Unhook();
        _menu = menu;
        _inventory = menu != null ? menu.Inventory : _inventory;
        Hook();
        RefreshOpen();
        RefreshAll();
    }

    void Awake()
    {
        if (_menu == null)
            _menu = GetComponent<CraftMenuController>() ?? FindFirstObjectByType<CraftMenuController>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureEventSystem();
        Build();
    }

    void OnEnable()
    {
        Hook();
        RefreshOpen();
    }

    void OnDisable() => Unhook();

    void Start()
    {
        if (_menu != null)
            Bind(_menu);
        RefreshOpen();
        RefreshAll();
    }

    void Update()
    {
        if (_menu == null || !_menu.IsOpen)
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.tabKey.wasPressedThisFrame)
            CycleCategory();
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            TryCraft();
        if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
            SetQty(_qty + 1);
        if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
            SetQty(_qty - 1);
        if (kb.upArrowKey.wasPressedThisFrame)
            MoveSelection(-1);
        if (kb.downArrowKey.wasPressedThisFrame)
            MoveSelection(1);
    }

    void Hook()
    {
        if (_menu != null)
        {
            _menu.OpenChanged -= OnOpenChanged;
            _menu.OpenChanged += OnOpenChanged;
        }

        if (_inventory != null)
        {
            _inventory.Changed -= RefreshAll;
            _inventory.Changed += RefreshAll;
        }
    }

    void Unhook()
    {
        if (_menu != null)
            _menu.OpenChanged -= OnOpenChanged;
        if (_inventory != null)
            _inventory.Changed -= RefreshAll;
    }

    void OnOpenChanged(bool _) => RefreshOpen();

    void RefreshOpen()
    {
        if (_root != null)
            _root.SetActive(_menu != null && _menu.IsOpen);
        if (_menu != null && _menu.IsOpen)
            RefreshAll();
    }

    void CycleCategory()
    {
        int next = ((int)_category + 1) % 6;
        SetCategory((CraftCategory)next);
    }

    void SetCategory(CraftCategory category)
    {
        _category = category;
        RebuildRows();
        RefreshAll();
    }

    void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
            return;
        int index = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Recipe == _selected)
            {
                index = i;
                break;
            }
        }

        index = Mathf.Clamp(index + delta, 0, _rows.Count - 1);
        Select(_rows[index].Recipe);
    }

    void Select(CraftRecipe recipe)
    {
        _selected = recipe;
        _qty = 1;
        RefreshAll();
    }

    void SetQty(int qty)
    {
        int max = Mathf.Max(1, CraftingService.MaxCraftable(_inventory, _selected));
        _qty = Mathf.Clamp(qty, 1, max);
        RefreshDetail();
        RefreshCraftButton();
    }

    void TryCraft()
    {
        if (_selected == null || _inventory == null)
            return;
        if (!CraftingService.TryCraft(_inventory, _selected, _qty))
            return;
        int max = CraftingService.MaxCraftable(_inventory, _selected);
        if (max <= 0)
            _qty = 1;
        else if (_qty > max)
            _qty = max;
        RefreshAll();
    }

    void RebuildRows()
    {
        if (_listRoot == null || _menu == null || _menu.Catalog == null)
            return;

        for (int i = _listRoot.childCount - 1; i >= 0; i--)
            Destroy(_listRoot.GetChild(i).gameObject);
        _rows.Clear();

        var recipes = _menu.Catalog.Recipes;
        for (int i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            if (recipe == null || recipe.Output == null)
                continue;
            if (_category != CraftCategory.All && recipe.Category != _category)
                continue;
            _rows.Add(CreateRow(recipe));
        }

        if (_selected == null || !Contains(_selected))
            _selected = _rows.Count > 0 ? _rows[0].Recipe : null;
    }

    bool Contains(CraftRecipe recipe)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Recipe == recipe)
                return true;
        }

        return false;
    }

    void RefreshAll()
    {
        if (!_built)
            return;
        if (_rows.Count == 0)
            RebuildRows();
        RefreshTabs();
        RefreshRows();
        RefreshDetail();
        RefreshCraftButton();
    }

    void RefreshTabs()
    {
        CraftCategory[] cats =
        {
            CraftCategory.All, CraftCategory.Materials, CraftCategory.Ammo,
            CraftCategory.Consumables, CraftCategory.Furniture, CraftCategory.Weapons
        };
        for (int i = 0; i < _tabButtons.Count && i < cats.Length; i++)
        {
            bool on = _category == cats[i];
            var img = _tabButtons[i].targetGraphic as Image;
            if (img != null)
                img.color = on ? Color.white : new Color(0.82f, 0.76f, 0.64f, 1f);
            var label = _tabButtons[i].GetComponentInChildren<Text>();
            if (label != null)
                label.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    void RefreshRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool selected = row.Recipe == _selected;
            int max = CraftingService.MaxCraftable(_inventory, row.Recipe);
            row.Background.color = selected ? InventoryUiTheme.SlotSelected : InventoryUiTheme.SlotNormal;
            row.Name.text = row.Recipe.DisplayName;
            row.Name.color = InventoryUiTheme.TextDark;
            row.State.text = max > 0 ? "Ready" : "Missing";
            row.State.color = max > 0 ? InventoryUiTheme.ReadyGreen : InventoryUiTheme.TextMuted;
            ApplyItemIcon(row.Icon, row.Recipe.Output);
        }
    }

    void RefreshDetail()
    {
        if (_selected == null)
        {
            _title.text = "Select a recipe";
            _desc.text = "Click a recipe on the left. Green Ready means you have the materials.";
            _outputLabel.text = string.Empty;
            _ingredients.text = string.Empty;
            _qtyLabel.text = "1";
            _status.text = string.Empty;
            if (_outputIcon != null)
                _outputIcon.enabled = false;
            return;
        }

        ApplyItemIcon(_outputIcon, _selected.Output);
        int outCount = _selected.OutputCount * _qty;
        _title.text = _selected.DisplayName;
        _desc.text = string.IsNullOrEmpty(_selected.Description)
            ? "No description."
            : _selected.Description;
        _outputLabel.text = $"Creates  {outCount}×  {_selected.Output.DisplayName}";

        var ings = _selected.Ingredients;
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < ings.Length; i++)
        {
            var ing = ings[i];
            if (ing.Item == null)
                continue;
            int need = ing.Count * _qty;
            int have = CraftingService.Owned(_inventory, ing.Item);
            string mark = have >= need ? "●" : "○";
            lines.Append(mark).Append(' ').Append(ing.Item.DisplayName)
                .Append("   ").Append(have).Append(" / ").Append(need).Append('\n');
        }

        _ingredients.text = lines.ToString();
        _qtyLabel.text = _qty.ToString();

        int max = CraftingService.MaxCraftable(_inventory, _selected);
        _status.text = max > 0
            ? $"Can craft  {max}"
            : "Not enough materials  or  inventory full";
        _status.color = max > 0 ? InventoryUiTheme.ReadyGreen : InventoryUiTheme.MissingRed;
    }

    void RefreshCraftButton()
    {
        if (_craftButton == null)
            return;
        bool can = CraftingService.CanCraft(_inventory, _selected, _qty);
        _craftButton.interactable = can;
        if (_craftLabel != null)
            _craftLabel.text = can ? "Craft    [Enter]" : "Can't craft";
    }

    static void ApplyItemIcon(Image image, ItemDefinition item)
    {
        if (image == null)
            return;
        if (item == null)
        {
            image.enabled = false;
            return;
        }

        image.enabled = true;
        Sprite spr = item.Icon;
        if (spr != null)
        {
            image.sprite = spr;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        else
        {
            image.sprite = null;
            image.color = item.IconColor;
        }
    }

    void Build()
    {
        if (_built)
            return;
        _built = true;

        var canvasGo = new GameObject("CraftCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("CraftRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)_root.transform, 0f, 0f, 0f, 0f);

        var dim = new GameObject("Dimmer", typeof(RectTransform));
        dim.transform.SetParent(_root.transform, false);
        Stretch((RectTransform)dim.transform, 0f, 0f, 0f, 0f);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.07f, 0.05f, 0.03f, 0.52f);
        var dimBtn = dim.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        dimBtn.onClick.AddListener(() => _menu?.Close());

        var panel = CreatePanel(_root.transform, "CraftPanel", new Vector2(1040f, 700f),
            InventoryUiTheme.CraftPanelSprite);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;

        var header = CreateText(panel.transform, "Craft", 26, TextAnchor.MiddleLeft);
        PinTop(header.rectTransform, 100f, 96f, 112f, 34f);
        header.fontStyle = FontStyle.Bold;

        var close = CreateButton(panel.transform, "X", new Vector2(40f, 40f), () => _menu?.Close());
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-100f, -92f);

        BuildTabs(panel.transform);
        BuildList(panel.transform);
        BuildDetail(panel.transform);

        _hint = CreateText(panel.transform,
            "C / Esc  close    ·    Tab  category    ·    ↑ ↓  recipe    ·    + / −  quantity    ·    Enter  craft",
            13, TextAnchor.MiddleCenter);
        PinBottom(_hint.rectTransform, 108f, 60f, 108f, 26f);
        _hint.color = InventoryUiTheme.TextMuted;

        _root.SetActive(false);
        RebuildRows();
    }

    void BuildTabs(Transform parent)
    {
        var bar = new GameObject("Tabs", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        PinTop((RectTransform)bar.transform, 100f, 140f, 100f, 42f);
        var layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(0, 0, 0, 0);

        AddTab(bar.transform, "All", CraftCategory.All);
        AddTab(bar.transform, "Mat", CraftCategory.Materials);
        AddTab(bar.transform, "Ammo", CraftCategory.Ammo);
        AddTab(bar.transform, "Kits", CraftCategory.Consumables);
        AddTab(bar.transform, "Furn", CraftCategory.Furniture);
        AddTab(bar.transform, "Wpn", CraftCategory.Weapons);
    }

    void AddTab(Transform parent, string label, CraftCategory category)
    {
        var btn = CreateButton(parent, label, Vector2.zero, () => SetCategory(category), InventoryUiTheme.TabSprite);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 36f;
        le.flexibleWidth = 1f;
        var labelText = btn.GetComponentInChildren<Text>();
        if (labelText != null)
        {
            labelText.fontSize = 14;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 11;
            labelText.resizeTextMaxSize = 14;
        }
        _tabButtons.Add(btn);
    }

    void BuildList(Transform parent)
    {
        var listPanel = CreatePanel(parent, "RecipeList", Vector2.zero);
        Stretch((RectTransform)listPanel.transform, 96f, 84f, -528f, -196f);

        var title = CreateText(listPanel.transform, "Recipes", 16, TextAnchor.MiddleLeft);
        PinTop(title.rectTransform, 12f, 8f, 12f, 24f);
        title.fontStyle = FontStyle.Bold;

        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(listPanel.transform, false);
        Stretch((RectTransform)scrollGo.transform, 8f, 8f, -8f, -40f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        var mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var maskImg = scrollGo.AddComponent<Image>();
        maskImg.color = new Color(1f, 1f, 1f, 0.02f);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(scrollGo.transform, false);
        var contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        scroll.content = contentRt;
        scroll.viewport = (RectTransform)scrollGo.transform;
        _listRoot = content.transform;
    }

    void BuildDetail(Transform parent)
    {
        var detail = CreatePanel(parent, "Detail", Vector2.zero);
        Stretch((RectTransform)detail.transform, 532f, 84f, -96f, -196f);

        var iconGo = new GameObject("OutputIcon", typeof(RectTransform));
        iconGo.transform.SetParent(detail.transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
        iconRt.pivot = new Vector2(0f, 1f);
        iconRt.anchoredPosition = new Vector2(20f, -20f);
        iconRt.sizeDelta = new Vector2(72f, 72f);
        _outputIcon = iconGo.AddComponent<Image>();
        _outputIcon.color = InventoryUiTheme.SlotNormal;
        var slotBg = iconGo.AddComponent<Outline>();
        slotBg.effectColor = InventoryUiTheme.OutlineGold;
        slotBg.effectDistance = new Vector2(1.5f, -1.5f);

        _title = CreateText(detail.transform, "Select a recipe", 22, TextAnchor.UpperLeft);
        PinTop(_title.rectTransform, 108f, 18f, 20f, 28f);
        _title.fontStyle = FontStyle.Bold;

        _outputLabel = CreateText(detail.transform, string.Empty, 14, TextAnchor.UpperLeft);
        PinTop(_outputLabel.rectTransform, 108f, 48f, 20f, 22f);
        _outputLabel.color = InventoryUiTheme.TextMuted;

        _desc = CreateText(detail.transform, string.Empty, 15, TextAnchor.UpperLeft);
        PinTop(_desc.rectTransform, 20f, 100f, 20f, 72f);

        var need = CreateText(detail.transform, "Requires", 16, TextAnchor.UpperLeft);
        PinTop(need.rectTransform, 20f, 178f, 20f, 24f);
        need.fontStyle = FontStyle.Bold;

        _ingredients = CreateText(detail.transform, string.Empty, 16, TextAnchor.UpperLeft);
        PinTop(_ingredients.rectTransform, 20f, 204f, 20f, 100f);

        _status = CreateText(detail.transform, string.Empty, 14, TextAnchor.UpperLeft);
        PinTop(_status.rectTransform, 20f, 308f, 20f, 28f);

        BuildQty(detail.transform);

        _craftButton = CreateButton(detail.transform, "Craft    [Enter]", new Vector2(0f, 48f), TryCraft);
        var craftRt = (RectTransform)_craftButton.transform;
        craftRt.anchorMin = new Vector2(0.08f, 0f);
        craftRt.anchorMax = new Vector2(0.92f, 0f);
        craftRt.pivot = new Vector2(0.5f, 0f);
        craftRt.anchoredPosition = new Vector2(0f, 96f);
        craftRt.sizeDelta = new Vector2(0f, 48f);
        _craftLabel = _craftButton.GetComponentInChildren<Text>();
        if (_craftLabel != null)
        {
            _craftLabel.fontSize = 18;
            _craftLabel.fontStyle = FontStyle.Bold;
        }
    }

    void BuildQty(Transform parent)
    {
        var row = new GameObject("Qty", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0.08f, 0f);
        rt.anchorMax = new Vector2(0.92f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 156f);
        rt.sizeDelta = new Vector2(0f, 40f);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        var minus = CreateButton(row.transform, "−", new Vector2(40f, 36f), () => SetQty(_qty - 1));
        minus.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;

        _qtyLabel = CreateText(row.transform, "1", 20, TextAnchor.MiddleCenter);
        var qtyLe = _qtyLabel.gameObject.AddComponent<LayoutElement>();
        qtyLe.preferredWidth = 56f;
        qtyLe.flexibleWidth = 1f;
        _qtyLabel.fontStyle = FontStyle.Bold;

        var plus = CreateButton(row.transform, "+", new Vector2(40f, 36f), () => SetQty(_qty + 1));
        plus.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;

        var max = CreateButton(row.transform, "Max", new Vector2(72f, 36f), () =>
        {
            int n = CraftingService.MaxCraftable(_inventory, _selected);
            SetQty(Mathf.Max(1, n));
        });
        max.gameObject.AddComponent<LayoutElement>().preferredWidth = 72f;
    }

    Row CreateRow(CraftRecipe recipe)
    {
        var go = new GameObject(recipe.DisplayName, typeof(RectTransform));
        go.transform.SetParent(_listRoot, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 56f;
        le.preferredHeight = 56f;
        le.flexibleWidth = 1f;

        var bg = go.AddComponent<Image>();
        Sprite slot = InventoryUiTheme.SlotSprite;
        if (slot != null)
        {
            bg.sprite = slot;
            bg.type = Image.Type.Sliced;
        }

        bg.color = InventoryUiTheme.SlotNormal;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        ApplyButtonColors(btn);
        var captured = recipe;
        btn.onClick.AddListener(() => Select(captured));

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = new Vector2(0f, 0.15f);
        iconRt.anchorMax = new Vector2(0f, 0.85f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(8f, 0f);
        iconRt.sizeDelta = new Vector2(44f, 0f);
        var icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;

        var name = CreateText(go.transform, recipe.DisplayName, 16, TextAnchor.MiddleLeft);
        Stretch(name.rectTransform, 60f, 4f, -88f, -4f);
        name.raycastTarget = false;

        var state = CreateText(go.transform, "Missing", 13, TextAnchor.MiddleRight);
        Stretch(state.rectTransform, -84f, 4f, -10f, -4f);
        state.raycastTarget = false;

        return new Row
        {
            Recipe = recipe,
            Button = btn,
            Background = bg,
            Name = name,
            State = state,
            Icon = icon
        };
    }

    Button CreateButton(Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick,
        Sprite sprite = null)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (size.sqrMagnitude > 0.01f)
            rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        ApplySprite(img, sprite != null ? sprite : InventoryUiTheme.ButtonSprite, sliced: true);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ApplyButtonColors(btn);
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null)
            btn.onClick.AddListener(onClick);

        var text = CreateText(go.transform, label, 15, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 8f, 4f, -8f, -4f);
        text.raycastTarget = false;
        text.fontStyle = FontStyle.Bold;
        return btn;
    }

    static void ApplyButtonColors(Button btn)
    {
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
        colors.pressedColor = new Color(0.82f, 0.74f, 0.58f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.52f, 0.48f, 0.7f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
    }

    Text CreateText(Transform parent, string content, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = InventoryUiTheme.TextDark;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 size, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        if (size.sqrMagnitude > 0.01f)
            ((RectTransform)go.transform).sizeDelta = size;
        var img = go.AddComponent<Image>();
        Sprite panel = sprite != null ? sprite : InventoryUiTheme.PanelSprite;
        if (panel != null)
            ApplySprite(img, panel, sliced: true);
        else
            img.color = InventoryUiTheme.PanelTint;
        return go;
    }

    static void ApplySprite(Image img, Sprite sprite, bool sliced)
    {
        if (img == null || sprite == null)
            return;
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = Color.white;
        img.preserveAspect = false;
        // PPU 100 would shrink 9-slice borders to ~1px; keep 1 texture pixel = 1 canvas unit.
        if (sliced && sprite.pixelsPerUnit > 1.01f)
            img.pixelsPerUnitMultiplier = sprite.pixelsPerUnit;
    }

    static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
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
