using UnityEngine;
using UnityEngine.UI;

/// <summary>Шкала перезарядки сильного удара кирки (ПКМ) над хотбаром; видна только с кирокой в руках.</summary>
public sealed class AltFireHud : MonoBehaviour
{
    static readonly Color ChargingColor = new Color(0.85f, 0.55f, 0.2f, 1f);
    static readonly Color ReadyColor = new Color(1f, 0.85f, 0.35f, 1f);
    const float HotbarClearance = 150f;

    PickaxeTool _pickaxe;
    GameObject _root;
    Image _fill;
    Text _label;

    void Awake()
    {
        _pickaxe = GetComponent<PickaxeTool>();
        if (_pickaxe == null)
        {
            Debug.LogError("AltFireHud: PickaxeTool not found on player", this);
            enabled = false;
            return;
        }

        Build();
    }

    void Build()
    {
        Canvas canvas = UiFactory.CreateCanvas(transform, "AltFireCanvas", 44);
        var anchor = new Vector2(0.5f, 0f);
        RectTransform root = UiFactory.CreateRect(
            canvas.transform, "AltFireRoot", anchor, anchor, new Vector2(0f, HotbarClearance), new Vector2(300f, 36f));
        _root = root.gameObject;

        _label = UiFactory.CreateText(root, "Label", 14, InventoryUiTheme.TextPrimary, TextAnchor.UpperCenter);
        _label.fontStyle = FontStyle.Bold;
        _fill = UiFactory.CreateBar(root, "Bar", new Vector2(0f, -20f), new Vector2(300f, 14f), ChargingColor);
    }

    void Update()
    {
        bool visible = _pickaxe.IsEquipped;
        if (_root.activeSelf != visible)
            _root.SetActive(visible);
        if (!visible)
            return;

        float charge = _pickaxe.HeavySwingCharge;
        bool isReady = charge >= 1f;
        UiFactory.SetBar(_fill, charge);
        _fill.color = isReady ? ReadyColor : ChargingColor;
        _label.text = isReady ? "RMB  Heavy swing  READY" : "RMB  Heavy swing";
    }
}
