using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Own-mage station (E). Satiety, feed, T1 repair-aura upgrade.</summary>
[DefaultExecutionOrder(-10)]
public sealed class MageUI : MonoBehaviour
{
    [SerializeField] OwnMageStation _station;
    [SerializeField] PlayerInventory _inventory;

    Font _font;
    GameObject _root;
    Text _satietyLabel;
    Image _satietyFill;
    Text _feedLabel;
    Text _upgradeLabel;
    Text _status;
    Button _feedButton;
    Button _upgradeButton;
    bool _built;

    public void Bind(OwnMageStation station)
    {
        Unhook();
        _station = station;
        Hook();
        RefreshOpen();
        Refresh();
    }

    void Awake()
    {
        _inventory = GetComponent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureEventSystem();
        Build();
    }

    void OnEnable() => Hook();
    void OnDisable() => Unhook();

    void Start()
    {
        if (_station == null)
            _station = FindFirstObjectByType<OwnMageStation>();
        if (_station != null)
            Bind(_station);
        RefreshOpen();
    }

    void Update()
    {
        if (_station == null)
            _station = OwnMageStation.OpenStation ?? FindFirstObjectByType<OwnMageStation>();
        if (_station == null || !_station.IsOpen)
            return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null)
            return;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            TryFeed();
        if (kb.uKey.wasPressedThisFrame)
            TryUpgrade();
    }

    void Hook()
    {
        if (_station == null)
            return;
        _station.OpenChanged -= OnOpen;
        _station.Changed -= Refresh;
        _station.OpenChanged += OnOpen;
        _station.Changed += Refresh;
        if (_inventory != null)
        {
            _inventory.Changed -= Refresh;
            _inventory.Changed += Refresh;
        }
    }

    void Unhook()
    {
        if (_station != null)
        {
            _station.OpenChanged -= OnOpen;
            _station.Changed -= Refresh;
        }
        if (_inventory != null)
            _inventory.Changed -= Refresh;
    }

    void OnOpen(bool _) => RefreshOpen();

    void RefreshOpen()
    {
        if (_root != null)
            _root.SetActive(_station != null && _station.IsOpen);
        if (_station != null && _station.IsOpen)
            Refresh();
    }

    void TryFeed()
    {
        if (_station != null && _station.TryFeed(_inventory))
            Refresh();
    }

    void TryUpgrade()
    {
        if (_station != null && _station.TryUpgrade(_inventory))
            Refresh();
    }

    void Refresh()
    {
        if (!_built || _station == null)
            return;

        float t = _station.MaxSatiety > 0.01f ? _station.Satiety / _station.MaxSatiety : 0f;
        _satietyFill.fillAmount = Mathf.Clamp01(t);
        _satietyLabel.text = $"Satiety   {Mathf.CeilToInt(_station.Satiety)} / {Mathf.CeilToInt(_station.MaxSatiety)}";

        int haveC = _inventory != null && _station != null
            ? _inventory.CountItem(Resources.Load<ItemDefinition>("Items/ChronumItem"))
            : 0;
        int haveM = _inventory != null
            ? _inventory.CountItem(Resources.Load<ItemDefinition>("Items/MetalItem"))
            : 0;

        bool canFeed = haveC >= _station.FeedChronumCost && _station.Satiety < _station.MaxSatiety - 0.01f;
        _feedButton.interactable = canFeed;
        _feedLabel.text = canFeed ? "Feed    [Enter]" : "Need chronum";

        if (_station.UpgradeLevel >= 1)
        {
            _upgradeButton.interactable = false;
            _upgradeLabel.text = "Repair aura  active";
            _status.text = "T1 upgrade owned. Nearby own-castle pieces slowly mend.";
            _status.color = InventoryUiTheme.ReadyGreen;
        }
        else
        {
            bool canUp = _station.CanUpgrade(_inventory);
            _upgradeButton.interactable = canUp;
            _upgradeLabel.text = canUp ? "Upgrade T1    [U]" : "Can't upgrade";
            _status.text = $"Need  {_station.UpgradeChronumCost} chronum  ({haveC})  +  {_station.UpgradeMetalCost} metal  ({haveM})";
            _status.color = canUp ? InventoryUiTheme.ReadyGreen : InventoryUiTheme.MissingRed;
        }
    }

    void Build()
    {
        if (_built)
            return;
        _built = true;

        var canvasGo = new GameObject("MageCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("MageRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)_root.transform, 0f, 0f, 0f, 0f);

        var dim = new GameObject("Dimmer", typeof(RectTransform));
        dim.transform.SetParent(_root.transform, false);
        Stretch((RectTransform)dim.transform, 0f, 0f, 0f, 0f);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.07f, 0.05f, 0.03f, 0.52f);
        var dimBtn = dim.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => _station?.Close());

        var panel = CreatePanel(_root.transform, "MagePanel", new Vector2(720f, 520f),
            InventoryUiTheme.CraftPanelSprite);
        var pr = (RectTransform)panel.transform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = Vector2.zero;

        var title = CreateText(panel.transform, "Mage", 26, TextAnchor.MiddleLeft);
        PinTop(title.rectTransform, 100f, 92f, 120f, 34f);
        title.fontStyle = FontStyle.Bold;

        var close = CreateGoldButton(panel.transform, "X", new Vector2(40f, 40f), () => _station?.Close());
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-96f, -90f);

        var card = CreatePanel(panel.transform, "Body", Vector2.zero);
        Stretch((RectTransform)card.transform, 100f, 80f, -100f, -140f);

        _satietyLabel = CreateText(card.transform, "Satiety", 18, TextAnchor.MiddleLeft);
        PinTop(_satietyLabel.rectTransform, 20f, 16f, 20f, 28f);
        _satietyLabel.fontStyle = FontStyle.Bold;

        var barBg = new GameObject("BarBg", typeof(RectTransform));
        barBg.transform.SetParent(card.transform, false);
        PinTop((RectTransform)barBg.transform, 20f, 52f, 20f, 22f);
        var bgImg = barBg.AddComponent<Image>();
        bgImg.color = new Color(0.35f, 0.28f, 0.2f, 0.85f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(barBg.transform, false);
        Stretch((RectTransform)fillGo.transform, 2f, 2f, -2f, -2f);
        _satietyFill = fillGo.AddComponent<Image>();
        _satietyFill.color = new Color(0.35f, 0.72f, 0.85f, 1f);
        _satietyFill.type = Image.Type.Filled;
        _satietyFill.fillMethod = Image.FillMethod.Horizontal;
        _satietyFill.fillOrigin = 0;

        var feedHint = CreateText(card.transform,
            "Chronum is the mage's fuel. Feed from your bag — never mined, never in settlement chests.",
            14, TextAnchor.UpperLeft);
        PinTop(feedHint.rectTransform, 20f, 88f, 20f, 56f);
        feedHint.color = InventoryUiTheme.TextMuted;

        _feedButton = CreateGoldButton(card.transform, "Feed    [Enter]", new Vector2(0f, 44f), TryFeed);
        var feedRt = (RectTransform)_feedButton.transform;
        feedRt.anchorMin = new Vector2(0.08f, 1f);
        feedRt.anchorMax = new Vector2(0.92f, 1f);
        feedRt.pivot = new Vector2(0.5f, 1f);
        feedRt.anchoredPosition = new Vector2(0f, -160f);
        feedRt.sizeDelta = new Vector2(0f, 44f);
        _feedLabel = _feedButton.GetComponentInChildren<Text>();

        var upTitle = CreateText(card.transform, "Upgrade T1 — repair aura", 16, TextAnchor.MiddleLeft);
        PinTop(upTitle.rectTransform, 20f, 214f, 20f, 24f);
        upTitle.fontStyle = FontStyle.Bold;

        _upgradeButton = CreateGoldButton(card.transform, "Upgrade T1    [U]", new Vector2(0f, 44f), TryUpgrade);
        var upRt = (RectTransform)_upgradeButton.transform;
        upRt.anchorMin = new Vector2(0.08f, 1f);
        upRt.anchorMax = new Vector2(0.92f, 1f);
        upRt.pivot = new Vector2(0.5f, 1f);
        upRt.anchoredPosition = new Vector2(0f, -250f);
        upRt.sizeDelta = new Vector2(0f, 44f);
        _upgradeLabel = _upgradeButton.GetComponentInChildren<Text>();

        _status = CreateText(card.transform, string.Empty, 14, TextAnchor.UpperLeft);
        PinTop(_status.rectTransform, 20f, 310f, 20f, 48f);

        var hint = CreateText(panel.transform, "E / Esc  close    ·    Enter  feed    ·    U  upgrade",
            13, TextAnchor.MiddleCenter);
        PinBottom(hint.rectTransform, 100f, 56f, 100f, 24f);
        hint.color = InventoryUiTheme.TextMuted;

        _root.SetActive(false);
    }

    Button CreateGoldButton(Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        if (size.sqrMagnitude > 0.01f)
            ((RectTransform)go.transform).sizeDelta = size;
        var img = go.AddComponent<Image>();
        Sprite spr = InventoryUiTheme.ButtonSprite;
        if (spr != null)
        {
            img.sprite = spr;
            img.color = Color.white;
        }
        else
            img.color = InventoryUiTheme.ButtonNormal;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f);
        colors.pressedColor = new Color(0.82f, 0.74f, 0.58f);
        colors.disabledColor = new Color(0.55f, 0.52f, 0.48f, 0.7f);
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(onClick);
        var text = CreateText(go.transform, label, 16, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 8f, 4f, -8f, -4f);
        text.raycastTarget = false;
        text.fontStyle = FontStyle.Bold;
        return btn;
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
        {
            img.sprite = panel;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            if (panel.pixelsPerUnit > 1.01f)
                img.pixelsPerUnitMultiplier = panel.pixelsPerUnit;
        }
        else
            img.color = InventoryUiTheme.PanelTint;
        return go;
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
