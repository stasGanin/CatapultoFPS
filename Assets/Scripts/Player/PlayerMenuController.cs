using UnityEngine;

public enum PlayerMenuTab
{
    Inventory = 0,
    Craft = 1,
    Talents = 2
}

/// <summary>
/// Tab открывает единое меню игрока на последней вкладке (инвентарь / ручной крафт / таланты).
/// Сами окна живут в своих контроллерах — здесь только переключение и «что сейчас открыто».
/// </summary>
[DefaultExecutionOrder(-15)]
public sealed class PlayerMenuController : MonoBehaviour
{
    PlayerInputReader _input;
    PlayerInventory _inventory;
    CraftMenuController _craft;
    TalentsUI _talents;
    PlayerMenuTab _lastTab = PlayerMenuTab.Inventory;

    /// <summary>Открытая вкладка меню или null. Крафт у станции (E) — не вкладка.</summary>
    public PlayerMenuTab? CurrentTab
    {
        get
        {
            if (_inventory != null && _inventory.IsMenuOpen)
                return PlayerMenuTab.Inventory;
            if (_craft != null && _craft.IsOpen && _craft.Station == CraftStation.Hand)
                return PlayerMenuTab.Craft;
            if (_talents != null && _talents.IsOpen)
                return PlayerMenuTab.Talents;
            return null;
        }
    }

    void Awake()
    {
        _input = GetComponent<PlayerInputReader>();
        _inventory = GetComponent<PlayerInventory>();
        _craft = GetComponent<CraftMenuController>();
        _talents = GetComponent<TalentsUI>();
    }

    void Update()
    {
        if (_input == null)
            return;

        // I / C открывают свои окна независимо; талантам нужно закрыться, если поверх открылось другое.
        if (_talents.IsOpen && (_inventory.IsMenuOpen || _craft.IsOpen))
            _talents.Close(false);

        PlayerMenuTab? current = CurrentTab;
        if (current.HasValue)
            _lastTab = current.Value;

        if (!_input.PlayerMenuPressed)
            return;

        if (current.HasValue)
            CloseAll();
        else if (!_inventory.BlocksGameplayInput)
            OpenTab(_lastTab);
    }

    public void OpenTab(PlayerMenuTab tab)
    {
        switch (tab)
        {
            case PlayerMenuTab.Inventory:
                _craft.Close();
                _talents.Close(false);
                _inventory.SetMenuOpen(true);
                break;
            case PlayerMenuTab.Craft:
                _talents.Close(false);
                _craft.OpenAt(CraftStation.Hand);
                break;
            case PlayerMenuTab.Talents:
                _inventory.CloseMenuAndStorage();
                _craft.Close();
                _talents.Open();
                break;
        }

        _lastTab = tab;
    }

    void CloseAll()
    {
        _inventory.CloseMenuAndStorage();
        _craft.Close();
        _talents.Close();
    }
}
