using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Прямой опрос Keyboard/Mouse (Input System API, без legacy Input.GetKey).
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool AttackHeld { get; private set; }
    public bool InventoryTogglePressed { get; private set; }
    public bool CraftMenuPressed { get; private set; }
    public bool PlayerMenuPressed { get; private set; }
    public bool BuildMenuPressed { get; private set; }
    public bool CancelPressed { get; private set; }
    public bool SecondaryPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool RotateBuildingPressed { get; private set; }
    public float RotateBuildingScroll { get; private set; }
    public bool MapPressed { get; private set; }
    public bool HelpTogglePressed { get; private set; }
    /// <summary>Mouse wheel: +1 next / -1 prev for hotbar when not in menu.</summary>
    public int HotbarScrollDelta { get; private set; }
    /// <summary>0–8 при нажатии 1–9, иначе -1.</summary>
    public int HotbarSlotPressed { get; private set; }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        Vector2 move = Vector2.zero;
        InventoryTogglePressed = false;
        CraftMenuPressed = false;
        PlayerMenuPressed = false;
        BuildMenuPressed = false;
        CancelPressed = false;
        SecondaryPressed = false;
        InteractPressed = false;
        RotateBuildingPressed = false;
        RotateBuildingScroll = 0f;
        MapPressed = false;
        HelpTogglePressed = false;
        HotbarScrollDelta = 0;
        HotbarSlotPressed = -1;

        if (kb != null)
        {
            if (kb.wKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed) move.y -= 1f;
            if (kb.aKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            JumpPressed = kb.spaceKey.wasPressedThisFrame;
            SprintHeld = kb.leftShiftKey.isPressed;
            InventoryTogglePressed = kb.iKey.wasPressedThisFrame;
            CraftMenuPressed = kb.cKey.wasPressedThisFrame;
            PlayerMenuPressed = kb.tabKey.wasPressedThisFrame;
            BuildMenuPressed = kb.bKey.wasPressedThisFrame;
            CancelPressed = kb.escapeKey.wasPressedThisFrame;
            InteractPressed = kb.eKey.wasPressedThisFrame;
            RotateBuildingPressed = kb.rKey.wasPressedThisFrame;
            MapPressed = kb.mKey.wasPressedThisFrame;
            HelpTogglePressed = kb.f1Key.wasPressedThisFrame;

            if (kb.digit1Key.wasPressedThisFrame) HotbarSlotPressed = 0;
            else if (kb.digit2Key.wasPressedThisFrame) HotbarSlotPressed = 1;
            else if (kb.digit3Key.wasPressedThisFrame) HotbarSlotPressed = 2;
            else if (kb.digit4Key.wasPressedThisFrame) HotbarSlotPressed = 3;
            else if (kb.digit5Key.wasPressedThisFrame) HotbarSlotPressed = 4;
            else if (kb.digit6Key.wasPressedThisFrame) HotbarSlotPressed = 5;
            else if (kb.digit7Key.wasPressedThisFrame) HotbarSlotPressed = 6;
            else if (kb.digit8Key.wasPressedThisFrame) HotbarSlotPressed = 7;
            else if (kb.digit9Key.wasPressedThisFrame) HotbarSlotPressed = 8;
        }
        else
        {
            JumpPressed = false;
            SprintHeld = false;
        }

        if (move.sqrMagnitude > 1f)
            move.Normalize();
        Move = move;

        Look = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        AttackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        AttackHeld = mouse != null && mouse.leftButton.isPressed;
        SecondaryPressed = mouse != null && mouse.rightButton.wasPressedThisFrame;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            RotateBuildingScroll = scroll;
            if (scroll > 0.1f)
                HotbarScrollDelta = -1;
            else if (scroll < -0.1f)
                HotbarScrollDelta = 1;
        }
    }
}
