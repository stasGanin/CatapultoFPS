using UnityEngine;

/// <summary>
/// Item recipe (balance + display). Icons load from Resources/ItemIcons/{id} when unset.
/// </summary>
[CreateAssetMenu(menuName = "Catapulto/Inventory/Item Definition", fileName = "ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] string _id = "item";
    [SerializeField] string _displayName = "Item";
    [SerializeField] ItemKind _kind = ItemKind.None;
    [SerializeField] Color _iconColor = Color.white;
    [SerializeField] Sprite _icon;
    [SerializeField] int _maxStack = 1;
    [SerializeField, TextArea(2, 4)] string _description;
    [Tooltip("Только для Blueprint: какой рецепт изучается по ПКМ в инвентаре.")]
    [SerializeField] CraftRecipe _teachesRecipe;

    [Header("Consumable")]
    [Tooltip("Сколько HP возвращает при использовании.")]
    [SerializeField, Min(0f)] float _healAmount;
    [Tooltip("Откат после использования, сек. Общий для всех слотов с этим предметом.")]
    [SerializeField, Min(0f)] float _cooldownSeconds;
    [Tooltip("Сколько длится использование (анимация поедания), сек.")]
    [SerializeField, Min(0f)] float _useDuration;
    [Tooltip("Опыт героя за использование (плоды опыта).")]
    [SerializeField, Min(0)] int _xpAmount;

    public string Id => _id;
    public string DisplayName => _displayName;
    public ItemKind Kind => _kind;
    public Color IconColor => _iconColor;
    public int MaxStack => Mathf.Max(1, _maxStack);
    public string Description => _description ?? string.Empty;
    public CraftRecipe TeachesRecipe => _teachesRecipe;
    public bool IsConsumable => _kind == ItemKind.Consumable;
    public float HealAmount => _healAmount;
    public float CooldownSeconds => _cooldownSeconds;
    public float UseDuration => _useDuration;
    public int XpAmount => _xpAmount;
    public bool IsEquippableTool =>
        _kind == ItemKind.HandCannon || _kind == ItemKind.Pickaxe || _kind == ItemKind.Crossbow
        || _kind == ItemKind.Staff || _kind == ItemKind.Scattergun || _kind == ItemKind.EmberLauncher;

    public Sprite Icon
    {
        get
        {
            if (_icon != null)
                return _icon;
            if (string.IsNullOrEmpty(_id))
                return null;
            return Resources.Load<Sprite>($"ItemIcons/{_id}");
        }
    }
}
