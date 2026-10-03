using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inventory cell bound to any IInventorySlots host. Drag transfers between player and chest.
/// </summary>
public class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [SerializeField] Image _background;
    [SerializeField] Image _icon;
    [SerializeField] Text _label;
    [SerializeField] Outline _outline;
    [SerializeField] Text _keyHint;

    InventoryUI _ui;
    IInventorySlots _host;
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_host == null)
            return;

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
