using UnityEngine;

/// <summary>Spawns a carcass castle: one mage section, shared columns/walls on expand.</summary>
public static class CarcassBuilder
{
    public static GameObject BuildPlayer(Vector3 center, float yawDegrees)
    {
        var root = CreateRoot("PlayerCastle_T1", center, yawDegrees, playerOwned: true);
        root.AddComponent<CastleFrame>().Bind(1, new System.Collections.Generic.List<CastleSocket>());
        var carcass = root.AddComponent<CarcassCastle>();
        carcass.Initialize(true);
        carcass.TryAddSection(0, 0, 0, fillOuterWalls: true, CarcassMetrics.WallDir.N, doorSlot: 0);

        Vector3 mageLocal = CarcassMetrics.CellCenterLocal(0, 0, 0) + Vector3.up * 0.05f;
        var mage = CastleMageVisual.Spawn(root.transform, mageLocal, heightScale: 2.8f, attachMageComponent: false);
        mage.AddComponent<OwnMageStation>();
        return root;
    }

    public static EnemyCastle BuildEnemy(Vector3 center, float yawDegrees)
    {
        var root = CreateRoot("EnemyCastle_T1", center, yawDegrees, playerOwned: false);
        var enemy = root.AddComponent<EnemyCastle>();
        var carcass = root.AddComponent<CarcassCastle>();
        carcass.Initialize(false);
        carcass.TryAddSection(0, 0, 0, fillOuterWalls: true, CarcassMetrics.WallDir.N, doorSlot: 0);

        Vector3 mageLocal = CarcassMetrics.CellCenterLocal(0, 0, 0) + Vector3.up * 0.05f;
        var mage = CastleMageVisual.Spawn(root.transform, mageLocal, heightScale: 2.8f, attachMageComponent: true);
        var mageComp = mage.GetComponent<EnemyCastleMage>();
        enemy.BindMage(mageComp);
        if (mageComp != null)
            mageComp.Bind(enemy, 120f);

        Vector3 chestLocal = mageLocal + new Vector3(2.2f, 0.02f, 0f);
        GameObject chest = FurnitureFactory.CreateChest(root.transform.TransformPoint(chestLocal), root.transform.rotation);
        chest.name = "EnemyLootChest";
        chest.transform.SetParent(root.transform, true);
        var storage = chest.GetComponent<StorageContainer>();
        storage?.Configure("Loot Chest", 36, 2.8f);
        LootDrop.FillEnemyChest(storage);
        InstallSpawners(root.transform, enemy);
        return enemy;
    }

    static void InstallSpawners(Transform root, EnemyCastle castle)
    {
        if (root == null || castle == null)
            return;

        Vector3 east = CarcassMetrics.BayLocal(0, 0, 0, CarcassMetrics.WallDir.E, 0);
        east += new Vector3(1.6f, 0.15f, 0f);
        Vector3 west = CarcassMetrics.BayLocal(0, 0, 0, CarcassMetrics.WallDir.W, 1);
        west += new Vector3(-1.6f, 0.15f, 0f);
        castle.RegisterSpawner(CreateSpawner(root, east, "Spawner_E"));
        castle.RegisterSpawner(CreateSpawner(root, west, "Spawner_W"));
    }

    static EnemyCastleSpawnModule CreateSpawner(Transform parent, Vector3 localPos, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "SpawnerTotem";
        mark.transform.SetParent(go.transform, false);
        mark.transform.localPosition = Vector3.up * 0.7f;
        mark.transform.localScale = new Vector3(0.55f, 1.4f, 0.55f);
        Object.Destroy(mark.GetComponent<Collider>());
        var rend = mark.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null && rend != null)
            rend.sharedMaterial = new Material(shader);
        if (rend != null)
        {
            var block = new MaterialPropertyBlock();
            Color c = new Color(0.28f, 0.12f, 0.12f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            rend.SetPropertyBlock(block);
        }

        var socket = new GameObject("GolemSocket");
        socket.transform.SetParent(go.transform, false);
        socket.transform.localPosition = Vector3.up * 0.25f;

        var module = go.AddComponent<CastleModuleRoot>();
        module.SetKind(CastleModuleKind.Spawner);
        module.SetFootprint(0.8f, 0.8f, 1.4f);
        module.SetGolemSocket(socket.transform);
        return go.AddComponent<EnemyCastleSpawnModule>();
    }

    static GameObject CreateRoot(string name, Vector3 center, float yawDegrees, bool playerOwned)
    {
        var go = new GameObject(name);
        // SW column of cell 0 sits at local origin; shift so the room center is on the pad.
        Vector3 origin = center - Quaternion.Euler(0f, yawDegrees, 0f) * CarcassMetrics.CellCenterLocal(0, 0, 0);
        go.transform.position = origin;
        go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        var square = go.AddComponent<SquareCastle>();
        square.SetPlayerOwned(playerOwned);
        return go;
    }
}
