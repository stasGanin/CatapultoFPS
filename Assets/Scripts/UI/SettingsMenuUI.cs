using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Pause / settings (Esc when no other menu is open).</summary>
[DefaultExecutionOrder(80)]
public sealed class SettingsMenuUI : MonoBehaviour
{
    static SettingsMenuUI _instance;

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;

    Font _font;
    GameObject _root;
    Text _sensValue;
    Text _fovValue;
    Text _volumeValue;
    Text _invertLabel;
    Text _fullLabel;
    Text _qualityLabel;
    bool _open;
    bool _blockedLastFrame;
    float _timeScaleBefore = 1f;

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
        if (player.GetComponent<SettingsMenuUI>() == null)
            player.AddComponent<SettingsMenuUI>();
    }

    void Awake()
    {
        _instance = this;
        GameSettings.EnsureLoaded();
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
        if (_open)
            Time.timeScale = _timeScaleBefore;
    }

    void Update()
    {
        if (_input == null)
            return;

        bool blockedNow = OverlayBlocking();
        if (_input.CancelPressed)
        {
            if (_open)
            {
                Close();
            }
            else if (!_blockedLastFrame && !blockedNow)
            {
                Open();
            }
        }

        _blockedLastFrame = OverlayBlocking();
    }

    bool OverlayBlocking()
    {
        if (_open)
            return true;
        if (_inventory == null)
            return WorldMapUI.IsOpen;

        if (_inventory.IsMenuOpen || _inventory.IsCraftOpen || _inventory.IsMageOpen || WorldMapUI.IsOpen)
            return true;

        if (_inventory.IsFurniturePlacing || ResearchUI.IsOpen)
            return true;

        // Учитываем и режим установки: иначе Esc одновременно отменяет призрак и ставит паузу.
        var building = _inventory.GetComponent<BuildingController>();
        if (building != null && building.enabled && building.BlocksWeapons)
            return true;

        var castle = _inventory.GetComponent<CastleBuildController>();
        return castle != null && castle.BlocksWeapons;
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
        WorldMapUI.CloseIfOpen();

        _open = true;
        _timeScaleBefore = Time.timeScale <= 0.01f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        RefreshValues();
        SetVisible(true);
        ApplyCursor();
        _inventory?.NotifyGameplayBlockChanged();
    }

    public void Close()
    {
        if (!_open)
            return;
        _open = false;
        Time.timeScale = _timeScaleBefore <= 0.01f ? 1f : _timeScaleBefore;
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
        if (_open || WorldMapUI.IsOpen || (_inventory != null && (_inventory.IsMenuOpen || _inventory.IsCraftOpen || _inventory.IsMageOpen)))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void RefreshValues()
    {
        if (_sensValue != null)
            _sensValue.text = GameSettings.LookSensitivity.ToString("0.00");
        if (_fovValue != null)
            _fovValue.text = Mathf.RoundToInt(GameSettings.FieldOfView).ToString();
        if (_volumeValue != null)
            _volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
        if (_invertLabel != null)
            _invertLabel.text = GameSettings.InvertY ? "On" : "Off";
        if (_fullLabel != null)
            _fullLabel.text = GameSettings.Fullscreen ? "On" : "Off";
        if (_qualityLabel != null)
            _qualityLabel.text = GameSettings.QualityName;
    }

    void Build()
    {
        var canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UiScale.Configure(canvasGo.AddComponent<CanvasScaler>());
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("SettingsRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)_root.transform);

        var dim = new GameObject("Dimmer", typeof(RectTransform));
        dim.transform.SetParent(_root.transform, false);
        Stretch((RectTransform)dim.transform);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);

        var panel = new GameObject("SettingsPanel", typeof(RectTransform));
        panel.transform.SetParent(_root.transform, false);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(580f, 640f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.10f, 0.13f, 0.18f, 0.98f);

        var title = CreateText(panel.transform, "Settings", 26, TextAnchor.MiddleCenter);
        PinTop(title.rectTransform, 24f, 18f, 24f, 40f);
        title.fontStyle = FontStyle.Bold;
        title.color = Color.white;

        float y = 78f;
        _sensValue = AddStepper(panel.transform, "Mouse sensitivity", y,
            () => GameSettings.SetLookSensitivity(GameSettings.LookSensitivity - 0.02f),
            () => GameSettings.SetLookSensitivity(GameSettings.LookSensitivity + 0.02f));
        y += 70f;
        _fovValue = AddStepper(panel.transform, "Field of view", y,
            () => GameSettings.SetFieldOfView(GameSettings.FieldOfView - 5f),
            () => GameSettings.SetFieldOfView(GameSettings.FieldOfView + 5f));
        y += 70f;
        _volumeValue = AddStepper(panel.transform, "Master volume", y,
            () => GameSettings.SetMasterVolume(GameSettings.MasterVolume - 0.1f),
            () => GameSettings.SetMasterVolume(GameSettings.MasterVolume + 0.1f));
        y += 70f;
        _invertLabel = AddToggle(panel.transform, "Invert Y", y, () => GameSettings.SetInvertY(!GameSettings.InvertY));
        y += 64f;
        _fullLabel = AddToggle(panel.transform, "Fullscreen", y, () => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
        y += 64f;
        _qualityLabel = AddStepper(panel.transform, "Quality", y,
            () => GameSettings.CycleQuality(-1),
            () => GameSettings.CycleQuality(1));

        var close = CreateButton(panel.transform, "Resume  [Esc]", new Vector2(280f, 46f), Close);
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 28f);

        var hint = CreateText(panel.transform, "Esc opens this menu in the field. Changes save immediately.", 13, TextAnchor.MiddleCenter);
        PinBottom(hint.rectTransform, 20f, 82f, 20f, 28f);
        hint.color = new Color(0.72f, 0.76f, 0.82f, 1f);

        RefreshValues();
    }

    Text AddStepper(Transform parent, string label, float top, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        PinTop((RectTransform)row.transform, 36f, top, 36f, 56f);

        var name = CreateText(row.transform, label, 16, TextAnchor.MiddleLeft);
        StretchLeft(name.rectTransform, 0f, 0.42f);

        var minusBtn = CreateButton(row.transform, "−", new Vector2(40f, 36f), () => { minus(); RefreshValues(); });
        PlaceRight((RectTransform)minusBtn.transform, 168f);

        var value = CreateText(row.transform, "0", 16, TextAnchor.MiddleCenter);
        var vrt = value.rectTransform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.pivot = new Vector2(1f, 0.5f);
        vrt.anchoredPosition = new Vector2(-88f, 0f);
        vrt.sizeDelta = new Vector2(72f, 36f);
        value.fontStyle = FontStyle.Bold;

        var plusBtn = CreateButton(row.transform, "+", new Vector2(40f, 36f), () => { plus(); RefreshValues(); });
        PlaceRight((RectTransform)plusBtn.transform, 0f);
        return value;
    }

    Text AddToggle(Transform parent, string label, float top, UnityEngine.Events.UnityAction onClick)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        PinTop((RectTransform)row.transform, 36f, top, 36f, 50f);

        var name = CreateText(row.transform, label, 16, TextAnchor.MiddleLeft);
        StretchLeft(name.rectTransform, 0f, 0.55f);

        var state = CreateText(row.transform, "Off", 16, TextAnchor.MiddleCenter);
        var srt = state.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 0.5f);
        srt.pivot = new Vector2(1f, 0.5f);
        srt.anchoredPosition = new Vector2(-108f, 0f);
        srt.sizeDelta = new Vector2(64f, 36f);
        state.fontStyle = FontStyle.Bold;

        var btn = CreateButton(row.transform, "Toggle", new Vector2(96f, 36f), () => { onClick(); RefreshValues(); });
        PlaceRight((RectTransform)btn.transform, 0f);
        return state;
    }

    static void StretchLeft(RectTransform rt, float min, float max)
    {
        rt.anchorMin = new Vector2(min, 0f);
        rt.anchorMax = new Vector2(max, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void PlaceRight(RectTransform rt, float fromRight)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-fromRight, 0f);
    }

    Button CreateButton(Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.48f, 0.82f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.60f, 0.95f, 1f);
        colors.pressedColor = new Color(0.16f, 0.34f, 0.62f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        var text = CreateText(go.transform, label, 15, TextAnchor.MiddleCenter);
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
