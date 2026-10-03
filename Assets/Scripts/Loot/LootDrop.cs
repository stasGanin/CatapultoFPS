using UnityEngine;

/// <summary>T1 world-drop tables from Docs/T1_Craft_Loot_UI.md. Blueprints skipped until that slice.</summary>
public static class LootDrop
{
    public static void Enemy(Vector3 position)
    {
        float roll = Random.value;
        if (roll < 0.10f)
            return;
        if (roll < 0.30f)
        {
            Spawn(Ore(), 1, position);
            return;
        }

        ItemDefinition chunk = Random.value < 0.5f ? Stone() : Wood();
        Spawn(chunk, Random.Range(1, 3), position);
    }

    public static void Mage(Vector3 position)
    {
        Spawn(Chronum(), 3, position);
        Spawn(Metal(), 5, position);
        // По T1-спеке маг гарантированно роняет рецепт; пока в пуле один.
        var blueprint = Resources.Load<ItemDefinition>("Items/EmberLauncherBlueprintItem");
        if (blueprint != null)
            Spawn(blueprint, 1, position);
    }

    public static void Brute(Vector3 position)
    {
        Spawn(BoneChip(), Random.Range(1, 3), position);
        if (Random.value < 0.55f)
            Spawn(Random.value < 0.5f ? Stone() : Wood(), Random.Range(1, 3), position);
        if (Random.value < 0.18f)
            Spawn(Metal(), 1, position);
    }

    public static void Bomber(Vector3 position)
    {
        Spawn(EmberGland(), 1, position);
        if (Random.value < 0.4f)
            Spawn(Ore(), 1, position);
        if (Random.value < 0.3f)
            Spawn(Wood(), Random.Range(1, 3), position);
    }

    public static void FillEnemyChest(StorageContainer chest)
    {
        if (chest == null)
            return;

        Put(chest, Wood(), Random.Range(4, 11));
        Put(chest, Stone(), Random.Range(4, 11));
        if (Random.value < 0.7f)
            Put(chest, Ore(), Random.Range(1, 5));
        if (Random.value < 0.45f)
            Put(chest, Metal(), Random.Range(1, 4));
        if (Random.value < 0.55f)
            Put(chest, Bolts(), Random.Range(6, 17));
        if (Random.value < 0.35f)
            Put(chest, RepairKit(), Random.Range(1, 3));
        if (Random.value < 0.4f)
            Put(chest, BoneChip(), Random.Range(1, 4));
        if (Random.value < 0.3f)
            Put(chest, EmberGland(), Random.Range(1, 3));
    }

    static void Put(StorageContainer chest, ItemDefinition item, int count)
    {
        if (chest == null || item == null || count <= 0)
            return;
        chest.TryAddItem(item, count);
    }

    public static void Module(CastleModuleKind kind, Vector3 position)
    {
        switch (kind)
        {
            case CastleModuleKind.Spawner:
                Spawn(Ore(), Random.Range(2, 4), position);
                break;
            case CastleModuleKind.Tower:
                Spawn(Metal(), Random.Range(1, 3), position);
                Spawn(Ore(), Random.Range(1, 3), position);
                break;
            case CastleModuleKind.Storage:
                Spawn(Random.value < 0.5f ? Wood() : Stone(), Random.Range(1, 3), position);
                break;
            default:
                Spawn(Stone(), Random.Range(2, 5), position);
                if (Random.value < 0.2f)
                    Spawn(Ore(), 1, position);
                break;
        }
    }

    static void Spawn(ItemDefinition item, int count, Vector3 position)
    {
        if (item == null || count <= 0)
            return;
        Vector3 jitter = Random.insideUnitSphere * 0.4f;
        jitter.y = Mathf.Abs(jitter.y);
        WorldLootPickup.Spawn(item, count, position + Vector3.up * 0.25f + jitter, 1.6f);
    }

    static ItemDefinition Wood() => Resources.Load<ItemDefinition>("Items/WoodItem");
    static ItemDefinition Stone() => Resources.Load<ItemDefinition>("Items/StoneItem");
    static ItemDefinition Ore() => Resources.Load<ItemDefinition>("Items/OreItem");
    static ItemDefinition Metal() => Resources.Load<ItemDefinition>("Items/MetalItem");
    static ItemDefinition Chronum() => Resources.Load<ItemDefinition>("Items/ChronumItem");
    static ItemDefinition Bolts() => Resources.Load<ItemDefinition>("Items/BoltsItem");
    static ItemDefinition RepairKit() => Resources.Load<ItemDefinition>("Items/RepairKitItem");
    static ItemDefinition BoneChip() => Resources.Load<ItemDefinition>("Items/BoneChipItem");
    static ItemDefinition EmberGland() => Resources.Load<ItemDefinition>("Items/EmberGlandItem");
}
