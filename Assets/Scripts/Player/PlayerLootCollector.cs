using UnityEngine;

/// <summary>
/// Автолут от игрока: ищем WorldLoot рядом (не наоборот — иначе буфер Overlap забивается сферами лута).
/// </summary>
public class PlayerLootCollector : MonoBehaviour
{
    static readonly Collider[] Buffer = new Collider[64];

    [SerializeField] PlayerInventory _inventory;
    [SerializeField] float _radius = 2.25f;

    void Awake()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
    }

    void FixedUpdate()
    {
        if (_inventory == null)
            return;

        Vector3 center = transform.position + Vector3.up * 1f;
        int hits = Physics.OverlapSphereNonAlloc(center, _radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider c = Buffer[i];
            if (c == null)
                continue;

            WorldLootPickup loot = c.GetComponent<WorldLootPickup>();
            if (loot == null)
                loot = c.GetComponentInParent<WorldLootPickup>();
            if (loot == null)
                continue;

            loot.TryCollect(_inventory);
        }
    }
}
