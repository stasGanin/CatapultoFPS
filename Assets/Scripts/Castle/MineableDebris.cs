using UnityEngine;

/// <summary>
/// Detached debris piece mined with the pickaxe; weapons can smash it too (base stone, no pickaxe bonus).
/// Prefer setting Loot Item / Count on the prefab; pickaxe stone is only a fallback.
/// </summary>
public class MineableDebris : MonoBehaviour, IDamageable
{
    [Header("Mining")]
    [SerializeField] int _maxHp = 10;

    [Header("Loot (prefab)")]
    [Tooltip("If set, pickaxe drops this instead of the tool default stone.")]
    [SerializeField] ItemDefinition _lootItem;
    [SerializeField] int _lootCount = 1;

    const float WeaponLootPickupRadius = 2f;

    static ItemDefinition _defaultStone;

    int _hp;
    // Destroy откладывается до конца кадра: без флага два удара в один кадр дадут двойной дроп.
    bool _isSpent;

    static ItemDefinition DefaultStone
    {
        get
        {
            if (_defaultStone == null)
                _defaultStone = Resources.Load<ItemDefinition>("Items/StoneItem");
            return _defaultStone;
        }
    }

    public ItemDefinition LootItem => _lootItem;
    public int LootCount => Mathf.Max(1, _lootCount);

    void Awake()
    {
        _hp = Mathf.Max(1, _maxHp);
    }

    public void ResetHp(int maxHp)
    {
        _maxHp = Mathf.Max(1, maxHp);
        _hp = _maxHp;
    }

    public void ConfigureLoot(ItemDefinition item, int count)
    {
        _lootItem = item;
        _lootCount = Mathf.Max(1, count);
    }

    /// <summary>
    /// Pickaxe hit. Uses prefab loot when assigned; otherwise fallbackItem/fallbackCount.
    /// lootMultiplier — бонус кирки к количеству дропа (1 = без бонуса).
    /// </summary>
    public bool TryMine(
        int damage,
        ItemDefinition fallbackItem,
        int fallbackCount,
        float lootPickupRadius,
        Vector3 hitPoint,
        Vector3 hitNormal,
        float lootMultiplier = 1f)
    {
        if (damage <= 0 || !isActiveAndEnabled || _isSpent)
            return false;

        HitSparkVfx.Play(hitPoint, hitNormal);
        _hp -= damage;
        if (_hp > 0)
            return true;

        _isSpent = true;

        ItemDefinition drop = _lootItem != null ? _lootItem : fallbackItem;
        int baseCount = _lootItem != null ? LootCount : Mathf.Max(1, fallbackCount);
        int count = Mathf.Max(1, Mathf.RoundToInt(baseCount * lootMultiplier));
        if (drop != null)
            WorldLootPickup.Spawn(drop, count, GetDebrisCenter(), lootPickupRadius);

        Destroy(gameObject);
        return true;
    }

    /// <summary>Урон оружием: кусок просто разбивается, камень «по умолчанию» даёт только кирка.</summary>
    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (amount <= 0f || !isActiveAndEnabled || _isSpent)
            return;

        _hp -= Mathf.CeilToInt(amount);
        if (_hp > 0)
            return;

        _isSpent = true;

        // Без своего лута оружие даёт обычный камень (1 шт., без бонуса кирки).
        ItemDefinition drop = _lootItem != null ? _lootItem : DefaultStone;
        int count = _lootItem != null ? LootCount : 1;
        if (drop != null)
            WorldLootPickup.Spawn(drop, count, GetDebrisCenter(), WeaponLootPickupRadius);
        Destroy(gameObject);
    }

    Vector3 GetDebrisCenter()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            return col.bounds.center;

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
            return renderer.bounds.center;

        return transform.position;
    }
}
