using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inventory cell bound to any IInventorySlots host. Drag transfers between player and chest.
/// </summary>
public class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image _background;
    [SerializeField] Image _icon;
    [SerializeField] Text _label;
    [SerializeField] Outline _outline;
    [SerializeField] Text _keyHint;

    static readonly Color CooldownShade = new Color(0f, 0f, 0f, 0.6f);

    static Sprite _whiteSprite;

    InventoryUI _ui;
    IInventorySlots _host;
    ConsumableUser _consumables;
    Image _cooldownFill;
    Text _cooldownText;
    int _slotIndex;
    bool _isPlayerHotbar;

    static InventorySlotView _dragSource;
    static GameObject _dragGhost;

    public int SlotIndex => _slotIndex;
    public IInventorySlots Host => _host;

    public void Bind(InventoryUI ui, IInventorySlots host, int slotIndex, bool isPlayerHotbar)
    {
        AutoWire();
        _ui = ui;
        _host = host;
        _slotIndex = slotIndex;
        _isPlayerHotbar = isPlayerHotbar;
    }

    void Update()
    {
        // ConsumableUser ставит EnemyCombatBootstrap, он может появиться позже, чем слот привязан.
        if (_consumables == null && _host is PlayerInventory own)
            _consumables = own.GetComponent<ConsumableUser>();
        if (_consumables == null)
            return;

        // Заливка убывает сверху вниз по мере отката, поверх — секунды до готовности.
        ItemDefinition item = _host.GetSlot(_slotIndex).Item;
        float remaining = _consumables.GetCooldownRemaining(item);
        if (remaining <= 0f)
        {
            if (_cooldownFill != null)
                _cooldownFill.gameObject.SetActive(false);
            return;
        }

        EnsureCooldownOverlay();
        _cooldownFill.gameObject.SetActive(true);
        _cooldownFill.fillAmount = _consumables.GetCooldownFraction(item);
        _cooldownText.text = Mathf.CeilToInt(remaining).ToString();
    }

    void EnsureCooldownOverlay()
    {
        if (_cooldownFill != null)
            return;

        if (_whiteSprite == null)
            _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

        // Строим лениво: слоты бывают как префабные (в сцене), так и созданные кодом — оверлей не требует проводки.
        _cooldownFill = UiFactory.CreateFill(transform, "CooldownFill", CooldownShade);
        _cooldownFill.sprite = _whiteSprite;
        _cooldownFill.type = Image.Type.Filled;
        _cooldownFill.fillMethod = Image.FillMethod.Vertical;
        _cooldownFill.fillOrigin = (int)Image.OriginVertical.Top;
        _cooldownText = UiFactory.CreateText(_cooldownFill.transform, "CooldownText", 18, Color.white, TextAnchor.MiddleCenter);
    }

    /// <summary>Legacy bind to player inventory index.</summary>
    public void Bind(InventoryUI ui, int slotIndex, bool isHotbar)
    {
        Bind(ui, ui != null ? ui.Inventory : null, slotIndex, isHotbar);
    }

    public void Bind(InventoryUI ui, int slotIndex, bool isHotbar, Image background, Image icon, Text label, Outline outline)
    {
        _background = background;
        _icon = icon;
        _label = label;
        _outline = outline;
        Bind(ui, slotIndex, isHotbar);
    }

    public void SetKeyHint(string hint)
    {
        if (_keyHint == null)
            AutoWire();
        if (_keyHint != null)
            _keyHint.text = hint ?? string.Empty;
    }

    void AutoWire()
    {
        if (_background == null)
            _background = GetComponent<Image>();
        if (_outline == null)
            _outline = GetComponent<Outline>();
        if (_icon == null)
        {
            var t = transform.Find("Icon");
            if (t != null)
                _icon = t.GetComponent<Image>();
        }

        if (_label == null)
        {
            var t = transform.Find("Label");
            if (t != null)
                _label = t.GetComponent<Text>();
            if (_label == null)
            {
                // Prefer Count text if present
                var count = transform.Find("Count");
                if (count != null)
                    _label = count.GetComponent<Text>();
            }
        }

        if (_keyHint == null)
        {
            var t = transform.Find("KeyHint");
            if (t != null)
                _keyHint = t.GetComponent<Text>();
        }
    }

    public void Refresh(InventorySlot slot, bool selected)
    {
        if (_icon != null)
        {
            Sprite spr = !slot.IsEmpty ? slot.Item.Icon : null;
            if (spr != null)
            {
                _icon.enabled = true;
                _icon.sprite = spr;
                _icon.color = Color.white;
                _icon.preserveAspect = true;
            }
            else
            {
                _icon.enabled = !slot.IsEmpty;
                _icon.sprite = null;
                if (!slot.IsEmpty)
                    _icon.color = slot.Item.IconColor;
            }
        }

        if (_label != null)
        {
            if (slot.IsEmpty)
                _label.text = string.Empty;
            else if (slot.Count > 1)
                _label.text = slot.Count.ToString();
            else
                _label.text = string.Empty;
        }

        if (_outline != null)
            _outline.enabled = _isPlayerHotbar && selected;

        if (_background != null)
        {
            _background.color = selected && _isPlayerHotbar
                ? InventoryUiTheme.SlotSelected
                : InventoryUiTheme.SlotNormal;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // ПКМ = «использовать». Только из своего инвентаря, не из сундука.
            if (_host is PlayerInventory own && own.TryUseSlot(_slotIndex))
                InventoryTooltip.Hide();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        bool shift = UnityEngine.InputSystem.Keyboard.current != null &&
                     (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed ||
                      UnityEngine.InputSystem.Keyboard.current.rightShiftKey.isPressed);

        if (shift && _ui?.Inventory != null && _host != null)
        {
            _ui.Inventory.QuickTransferSlot(_host, _slotIndex);
            return;
        }

        if (_isPlayerHotbar && _host is PlayerInventory)
            _ui.SelectHotbarFromUi(_slotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Во время перетаскивания тултип только мешает.
        if (_host == null || _dragSource != null)
            return;
        InventorySlot slot = _host.GetSlot(_slotIndex);
        if (!slot.IsEmpty)
            InventoryTooltip.Show(slot.Item, slot.Count);
    }

    public void OnPointerExit(PointerEventData eventData) => InventoryTooltip.Hide();

    // Инвентарь закрыли, пока курсор был над слотом, — OnPointerExit уже не придёт.
    void OnDisable() => InventoryTooltip.Hide();

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_host == null)
            return;
        InventoryTooltip.Hide();

        InventorySlot slot = _host.GetSlot(_slotIndex);
        if (slot.IsEmpty)
            return;

        _dragSource = this;
        _dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var ghostImage = _dragGhost.GetComponent<Image>();
        ghostImage.raycastTarget = false;
        Sprite spr = slot.Item.Icon;
        if (spr != null)
        {
            ghostImage.sprite = spr;
            ghostImage.color = Color.white;
            ghostImage.preserveAspect = true;
        }
        else
            ghostImage.color = slot.Item.IconColor;

        Transform canvas = _ui != null && _ui.DragLayer != null ? _ui.DragLayer : transform.root;
        _dragGhost.transform.SetParent(canvas, false);
        var rt = _dragGhost.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(52f, 52f);
        rt.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
            _dragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
            Destroy(_dragGhost);
        _dragGhost = null;
        _dragSource = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_dragSource == null || _dragSource == this || _host == null || _dragSource._host == null)
            return;

        InventoryTransfer.Swap(_dragSource._host, _dragSource._slotIndex, _host, _slotIndex);
    }
}
