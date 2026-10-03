using UnityEngine;

/// <summary>Builds a square castle: 3 authored modules per side, no corner pieces yet.</summary>
public static class SquareCastleBuilder
{
    const string WallPath = "Castle/Modules/WallModule";
    const string DoorPath = "Castle/Modules/DoorModule";
    const string SpawnerPath = "Castle/Modules/SpawnerModule";
    const string TowerPath = "Castle/Modules/TowerModule";

    public static GameObject BuildPlayer(Vector3 center, float yawDegrees)
    {
        var root = CreateRoot("PlayerCastle_T1", center, yawDegrees, playerOwned: true);
        root.AddComponent<CastleFrame>().Bind(1, new System.Collections.Generic.List<CastleSocket>());
        PlaceSides(root.transform, playerLayout: true);
        var mage = CastleMageVisual.Spawn(root.transform, new Vector3(0f, 0.05f, 0f), heightScale: 2.8f, attachMageComponent: false);
        mage.AddComponent<OwnMageStation>();
        FurnitureGrid.Build(root.transform);
        return root;
    }

    public static EnemyCastle BuildEnemy(Vector3 center, float yawDegrees)
    {
        var root = CreateRoot("EnemyCastle_T1", center, yawDegrees, playerOwned: false);
        var castle = root.AddComponent<EnemyCastle>();
        PlaceSides(root.transform, playerLayout: false);

        var mage = CastleMageVisual.Spawn(root.transform, new Vector3(0f, 0.05f, 0f), heightScale: 2.8f, attachMageComponent: true);
        var mageComp = mage.GetComponent<EnemyCastleMage>();
        castle.BindMage(mageComp);
        if (mageComp != null)
            mageComp.Bind(castle, 120f);

        var spawners = root.GetComponentsInChildren<EnemyCastleSpawnModule>(true);
        for (int i = 0; i < spawners.Length; i++)
            castle.RegisterSpawner(spawners[i]);

        PlaceLootChest(root.transform);
        return castle;
    }

    static GameObject CreateRoot(string name, Vector3 center, float yawDegrees, bool playerOwned)
    {
        var go = new GameObject(name);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        var square = go.AddComponent<SquareCastle>();
        square.SetPlayerOwned(playerOwned);
        return go;
    }

    static void PlaceSides(Transform root, bool playerLayout)
    {
        GameObject wall = Load(WallPath);
        GameObject door = Load(DoorPath);
        GameObject spawner = Load(SpawnerPath);
        GameObject tower = Load(TowerPath);
        if (wall == null)
        {
            Debug.LogError("SquareCastleBuilder: missing WallModule prefab. Run Catapulto/Castle/Generate Module Prefabs.");
            return;
        }

        float width = AlongWallSize(wall);
        GameObject[][] sides = playerLayout
            ? PlayerSides(wall, door)
            : EnemySides(wall, door, spawner, tower);

        for (int side = 0; side < 4; side++)
        {
            Quaternion face = Quaternion.Euler(0f, side * 90f, 0f);
            for (int slot = 0; slot < 3; slot++)
            {
                GameObject prefab = sides[side][slot];
                if (prefab == null)
                    prefab = wall;

                Vector3 local = face * new Vector3((slot - 1) * width, 0f, 1.5f * width);
                var instance = Object.Instantiate(prefab, root);
                instance.name = prefab.name;
                instance.transform.localPosition = local;
                // FBX is long on local Z; +90 Y makes that axis run along the wall.
                instance.transform.localRotation = face * ArtCorrection(prefab);
                instance.transform.localScale = Vector3.one * WidthScale(prefab, width);
            }
        }
    }

    static void PlaceLootChest(Transform castleRoot)
    {
        Vector3 world = castleRoot.TransformPoint(new Vector3(0f, 0.02f, 3.2f));
        GameObject chest = FurnitureFactory.CreateChest(world, castleRoot.rotation);
        chest.name = "EnemyLootChest";
        chest.transform.SetParent(castleRoot, true);
        var storage = chest.GetComponent<StorageContainer>();
        storage?.Configure("Loot Chest", 36, 2.8f);
        LootDrop.FillEnemyChest(storage);
    }

    static GameObject[][] PlayerSides(GameObject wall, GameObject door)
    {
        GameObject d = door != null ? door : wall;
        return new[]
        {
            new[] { wall, d, wall },
            new[] { wall, wall, wall },
            new[] { wall, wall, wall },
            new[] { wall, wall, wall },
        };
    }

    static GameObject[][] EnemySides(GameObject wall, GameObject door, GameObject spawner, GameObject tower)
    {
        GameObject d = door != null ? door : wall;
        GameObject s = spawner != null ? spawner : wall;
        GameObject t = tower != null ? tower : wall;
        return new[]
        {
            new[] { wall, d, wall },
            new[] { wall, s, wall },
            new[] { wall, t, wall },
            new[] { wall, s, wall },
        };
    }

    static float WidthScale(GameObject prefab, float wallAlong)
    {
        float size = AlongWallSize(prefab);
        if (size < 0.1f)
            return 1f;
        return wallAlong / size;
    }

    static float AlongWallSize(GameObject prefab)
    {
        var root = prefab.GetComponent<CastleModuleRoot>();
        if (root != null)
            return Mathf.Max(root.FootprintWidth, root.FootprintDepth);

        var rend = prefab.GetComponentInChildren<Renderer>();
        if (rend != null)
            return Mathf.Max(2f, Mathf.Max(rend.bounds.size.x, rend.bounds.size.z));

        return 4f;
    }

    static Quaternion ArtCorrection(GameObject prefab)
    {
        var root = prefab.GetComponent<CastleModuleRoot>();
        if (root != null && root.FootprintDepth > root.FootprintWidth * 1.15f)
            return Quaternion.Euler(0f, 90f, 0f);
        return Quaternion.identity;
    }

    static GameObject Load(string resourcesPath)
    {
        return Resources.Load<GameObject>(resourcesPath);
    }
}
