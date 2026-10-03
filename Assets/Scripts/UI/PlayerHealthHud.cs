using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Parchment health bar (bottom-left). Matches inventory beige theme.
/// </summary>
public sealed class PlayerHealthHud : MonoBehaviour
{
    [SerializeField] PlayerHealth _health;
    [SerializeField] PlayerMana _mana;
    [SerializeField] Image _fill;
    [SerializeField] Image _heart;
    [SerializeField] Text _label;
    [SerializeField] Image _manaFill;
    [SerializeField] Text _manaLabel;
    [SerializeField] string _format = "{0}  /  {1}";

    float _displayFill = 1f;
    float _displayManaFill = 1f;
    bool _built;

    public void Bind(PlayerHealth health)
    {
        if (_health != null)
            _health.HealthChanged -= OnHealthChanged;
        _health = health;
        if (isActiveAndEnabled && _health != null)
            _health.HealthChanged += OnHealthChanged;
        if (_health != null)
            SnapTo(_health.Health, _health.MaxHealth);
    }

    public void BindMana(PlayerMana mana)
    {
        if (_mana != null)
            _mana.ManaChanged -= OnManaChanged;
        _mana = mana;
        if (isActiveAndEnabled && _mana != null)
            _mana.ManaChanged += OnManaChanged;
        if (_mana != null)
            SnapMana(_mana.Mana, _mana.MaxMana);
    }

    void Awake()
    {
        if (_health == null)
            _health = GetComponentInParent<PlayerHealth>() ?? FindFirstObjectByType<PlayerHealth>();
        if (_mana == null)
            _mana = GetComponentInParent<PlayerMana>() ?? FindFirstObjectByType<PlayerMana>();

        if (_fill == null || _label == null)
            TryWireExisting();

        if (_fill == null || _label == null)
            BuildRuntimeHud();

        EnsureManaBar();
    }

    void TryWireExisting()
    {
        if (_fill == null)
        {
            var t = transform.Find("HealthCanvas/HpPanel/FillTrack/Fill");
            if (t != null)
                _fill = t.GetComponent<Image>();
        }

        if (_label == null)
        {
            var t = transform.Find("HealthCanvas/HpPanel/HpText");
            if (t == null)
                t = transform.Find("HealthCanvas/HpText");
            if (t != null)
                _label = t.GetComponent<Text>();
            if (_label == null)
                _label = GetComponentInChildren<Text>(true);
        }

        if (_heart == null)
        {
            var t = transform.Find("HealthCanvas/HpPanel/Heart");
            if (t != null)
                _heart = t.GetComponent<Image>();
        }
    }

    void BuildRuntimeHud()
    {
        if (_built)
            return;
        _built = true;

        // Hide old plain text if present under us
        var old = transform.Find("HealthCanvas");
        if (old != null)
            Destroy(old.gameObject);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var canvasGo = new GameObject("HealthCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("HpPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.anchoredPosition = new Vector2(28f, 110f);
        panelRt.sizeDelta = new Vector2(320f, 52f);

        var panelImg = panel.AddComponent<Image>();
        Sprite parchment = InventoryUiTheme.PanelSprite;
        if (parchment != null)
        {
            panelImg.sprite = parchment;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
        }
        else
            panelImg.color = InventoryUiTheme.PanelTint;

        var heartGo = new GameObject("Heart", typeof(RectTransform));
        heartGo.transform.SetParent(panel.transform, false);
        var heartRt = heartGo.GetComponent<RectTransform>();
        heartRt.anchorMin = heartRt.anchorMax = new Vector2(0f, 0.5f);
        heartRt.pivot = new Vector2(0f, 0.5f);
        heartRt.anchoredPosition = new Vector2(8f, 0f);
        heartRt.sizeDelta = new Vector2(40f, 40f);
        _heart = heartGo.AddComponent<Image>();
        Sprite heartSpr = Resources.Load<Sprite>("UI/hud_heart");
        if (heartSpr != null)
        {
            _heart.sprite = heartSpr;
            _heart.preserveAspect = true;
            _heart.color = Color.white;
        }
        else
            _heart.color = new Color(0.72f, 0.32f, 0.28f, 1f);

        var trackGo = new GameObject("FillTrack", typeof(RectTransform));
        trackGo.transform.SetParent(panel.transform, false);
        var trackRt = trackGo.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot = new Vector2(0f, 0.5f);
        trackRt.anchoredPosition = new Vector2(54f, -2f);
        trackRt.sizeDelta = new Vector2(-66f, 18f);

        var trackImg = trackGo.AddComponent<Image>();
        trackImg.color = new Color(0.78f, 0.72f, 0.62f, 0.95f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(trackGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        _fill = fillGo.AddComponent<Image>();
        _fill.color = new Color(0.72f, 0.34f, 0.30f, 1f);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 1f;

        var textGo = new GameObject("HpText", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0.55f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(54f, 0f);
        textRt.offsetMax = new Vector2(-10f, -2f);
        _label = textGo.AddComponent<Text>();
        _label.font = font;
        _label.fontSize = 15;
        _label.fontStyle = FontStyle.Bold;
        _label.color = InventoryUiTheme.TextDark;
        _label.alignment = TextAnchor.MiddleLeft;
        _label.raycastTarget = false;
    }

    void EnsureManaBar()
    {
        if (_manaFill != null)
            return;

        Transform canvasRoot = transform.Find("HealthCanvas");
        if (canvasRoot == null && GetComponent<Canvas>() != null)
            canvasRoot = transform;
        if (canvasRoot == null)
            return;

        Transform existing = canvasRoot.Find("ManaPanel");
        if (existing != null)
        {
            var fill = existing.Find("FillTrack/Fill");
            if (fill != null)
                _manaFill = fill.GetComponent<Image>();
            var label = existing.Find("ManaText");
            if (label != null)
                _manaLabel = label.GetComponent<Text>();
            if (_manaFill != null)
                return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var panel = new GameObject("ManaPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.anchoredPosition = new Vector2(28f, 168f);
        panelRt.sizeDelta = new Vector2(320f, 46f);

        var panelImg = panel.AddComponent<Image>();
        Sprite parchment = InventoryUiTheme.PanelSprite;
        if (parchment != null)
        {
            panelImg.sprite = parchment;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
        }
        else
            panelImg.color = InventoryUiTheme.PanelTint;

        var gemGo = new GameObject("Gem", typeof(RectTransform));
        gemGo.transform.SetParent(panel.transform, false);
        var gemRt = gemGo.GetComponent<RectTransform>();
        gemRt.anchorMin = gemRt.anchorMax = new Vector2(0f, 0.5f);
        gemRt.pivot = new Vector2(0f, 0.5f);
        gemRt.anchoredPosition = new Vector2(10f, 0f);
        gemRt.sizeDelta = new Vector2(28f, 28f);
        var gem = gemGo.AddComponent<Image>();
        Sprite gemSpr = Resources.Load<Sprite>("UI/hud_heart");
        if (gemSpr != null)
        {
            gem.sprite = gemSpr;
            gem.preserveAspect = true;
        }

        gem.color = new Color(0.35f, 0.62f, 0.95f, 1f);

        var trackGo = new GameObject("FillTrack", typeof(RectTransform));
        trackGo.transform.SetParent(panel.transform, false);
        var trackRt = trackGo.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot = new Vector2(0f, 0.5f);
        trackRt.anchoredPosition = new Vector2(48f, -2f);
        trackRt.sizeDelta = new Vector2(-60f, 16f);
        var trackImg = trackGo.AddComponent<Image>();
        trackImg.color = new Color(0.70f, 0.76f, 0.86f, 0.95f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(trackGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        _manaFill = fillGo.AddComponent<Image>();
        _manaFill.color = new Color(0.28f, 0.52f, 0.92f, 1f);
        _manaFill.type = Image.Type.Filled;
        _manaFill.fillMethod = Image.FillMethod.Horizontal;
        _manaFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _manaFill.fillAmount = 1f;

        var textGo = new GameObject("ManaText", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0.52f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(48f, 0f);
        textRt.offsetMax = new Vector2(-10f, -1f);
        _manaLabel = textGo.AddComponent<Text>();
        _manaLabel.font = font;
        _manaLabel.fontSize = 14;
        _manaLabel.fontStyle = FontStyle.Bold;
        _manaLabel.color = InventoryUiTheme.TextDark;
        _manaLabel.alignment = TextAnchor.MiddleLeft;
        _manaLabel.raycastTarget = false;
        _manaLabel.text = "100  /  100";
    }

    void OnEnable()
    {
        if (_health != null)
            _health.HealthChanged += OnHealthChanged;
        if (_mana != null)
            _mana.ManaChanged += OnManaChanged;
    }

    void OnDisable()
    {
        if (_health != null)
            _health.HealthChanged -= OnHealthChanged;
        if (_mana != null)
            _mana.ManaChanged -= OnManaChanged;
    }

    void Start()
    {
        if (_mana == null)
            _mana = GetComponentInParent<PlayerMana>() ?? FindFirstObjectByType<PlayerMana>();
        if (_health != null)
            SnapTo(_health.Health, _health.MaxHealth);
        if (_mana != null)
        {
            BindMana(_mana);
            SnapMana(_mana.Mana, _mana.MaxMana);
        }
    }

    void Update()
    {
        if (_fill != null)
        {
            _fill.fillAmount = Mathf.MoveTowards(_fill.fillAmount, _displayFill, Time.unscaledDeltaTime * 1.8f);
            if (_displayFill < 0.3f)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                _fill.color = new Color(0.78f * pulse, 0.28f, 0.26f, 1f);
            }
            else
                _fill.color = Color.Lerp(new Color(0.75f, 0.42f, 0.28f, 1f), new Color(0.62f, 0.32f, 0.28f, 1f), _displayFill);
        }

        if (_manaFill != null)
        {
            _manaFill.fillAmount = Mathf.MoveTowards(_manaFill.fillAmount, _displayManaFill, Time.unscaledDeltaTime * 2.2f);
            _manaFill.color = Color.Lerp(new Color(0.22f, 0.38f, 0.78f, 1f), new Color(0.38f, 0.68f, 1f, 1f), _displayManaFill);
        }
    }

    void OnHealthChanged(float current, float max) => Apply(current, max);
    void OnManaChanged(float current, float max) => ApplyMana(current, max);

    void SnapTo(float current, float max)
    {
        Apply(current, max);
        if (_fill != null)
            _fill.fillAmount = _displayFill;
    }

    void Apply(float current, float max)
    {
        max = Mathf.Max(1f, max);
        _displayFill = Mathf.Clamp01(current / max);
        if (_label != null)
            _label.text = string.Format(_format, Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }

    void SnapMana(float current, float max)
    {
        ApplyMana(current, max);
        if (_manaFill != null)
            _manaFill.fillAmount = _displayManaFill;
    }

    void ApplyMana(float current, float max)
    {
        max = Mathf.Max(1f, max);
        _displayManaFill = Mathf.Clamp01(current / max);
        if (_manaLabel != null)
            _manaLabel.text = string.Format(_format, Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }

    void Refresh(float current, float max) => Apply(current, max);
}
