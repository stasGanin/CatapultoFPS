#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the authored Project_2 terrain into SampleScene and leaves two test pads.
/// Menu: Catapulto / Map / Install Project_2 Terrain
/// </summary>
public static class CatapultoLocationMapGenerator
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string TerrainAssetPath = "Assets/Environment/Terrain/New Terrain.asset";
    const string BridgePath = "Assets/Environment/Bridge/bridge_low.fbx";
    const string WaterMeshPath = "Assets/Environment/Terrain/water_low.fbx";
    const string WaterMatPath = "Assets/Environment/Terrain/waterMat.mat";
    const string EnvRootName = "Project2Environment";

    [MenuItem("Catapulto/Map/Install Project_2 Terrain")]
    public static void InstallFromMenu() => InstallInternal(promptSave: true);

    [MenuItem("Catapulto/Map/Generate T1 Location")]
    public static void Generate() => InstallFromMenu();

    public static void GenerateBatch() => InstallInternal(promptSave: false);

    static void InstallInternal(bool promptSave)
    {
        if (promptSave && !Application.isBatchMode)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.Refresh();

        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
        if (data == null)
        {
            Debug.LogError($"Missing {TerrainAssetPath}. Copy New Terrain.asset from Project_2.");
            return;
        }

        bool hadPads = GameObject.Find("MapNodes") != null;

        ClearGameplayClutter();

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "T1_Terrain";
        go.transform.position = Vector3.zero;

        var terrain = go.GetComponent<Terrain>();
        terrain.allowAutoConnect = true;
        terrain.drawInstanced = false;
        terrain.drawTreesAndFoliage = true;
        terrain.treeDistance = 5000f;
        terrain.treeBillboardDistance = 50f;
        terrain.treeCrossFadeLength = 5f;

        SpawnAuthoredProps();

        if (!hadPads)
        {
            Vector3 home = FindFlatSpot(terrain, 0.18f, 0.36f, 0.18f, 0.36f, 11);
            Vector3 enemy = FindFlatSpot(terrain, 0.58f, 0.82f, 0.58f, 0.82f, 29);
            var root = new GameObject("MapNodes");
            CreatePadVisual(root.transform, home, MapNodeKind.PlayerCastle, "home", new[] { 1 }, true);
            CreatePadVisual(root.transform, enemy, MapNodeKind.EnemyCastle, "enemy", new[] { 0 }, false);
            var homePlatform = root.transform.GetChild(0).GetComponent<CastlePlatform>();
            if (homePlatform != null)
                PlacePlayerOnPad(homePlatform.SpawnPoint);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode)
            Selection.activeGameObject = go;

        Debug.Log(
            "Project_2 terrain + paint + bridge + water installed. " +
            "Two pads: green = player, red = enemy. Move them in the scene.");
    }

    static void ClearGameplayClutter()
    {
        string[] removeExact =
        {
            "Ground", "CastleWall", "EnemySpawner",
            "PreSlicedCastleWall",
        };

        var roots = SceneRoots();
        for (int i = roots.Length - 1; i >= 0; i--)
        {
            var go = roots[i];
            if (go == null)
                continue;

            string n = go.name;
            bool drop = false;
            for (int r = 0; r < removeExact.Length; r++)
            {
                if (n == removeExact[r] || n.StartsWith(removeExact[r]))
                {
                    drop = true;
                    break;
                }
            }

            if (n.StartsWith("Node_") || n == "RockScatter" ||
                n == "RidgeBoulders" || n == "T1_Terrain" || n == EnvRootName ||
                n == "CastleFrame_T1" || n == "EnemyCastle_T1" || n == "CastleBootstrap" ||
                n == "bridge_low" || n == "water_low")
                drop = true;

            if (drop)
                Object.DestroyImmediate(go);
        }

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t == null || t.parent != null)
                continue;
            if (t.name.StartsWith("Enemy") || t.name.StartsWith("Loot_") || t.name == "EnemyBolt")
                Object.DestroyImmediate(t.gameObject);
        }
    }

    static GameObject[] SceneRoots()
    {
        var scene = SceneManager.GetActiveScene();
        var list = new List<GameObject>();
        scene.GetRootGameObjects(list);
        return list.ToArray();
    }

    static void SpawnAuthoredProps()
    {
        var root = new GameObject(EnvRootName);

        GameObject bridge = SpawnFbx(
            BridgePath,
            "bridge_low",
            new Vector3(195.28f, 7.15f, 125.83f),
            new Quaternion(0f, 0.9437621f, 0f, 0.33062533f),
            Vector3.one * 203.01775f,
            root.transform,
            null);
        if (bridge != null)
        {
            var filters = bridge.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                    continue;
                if (filters[i].GetComponent<MeshCollider>() != null)
                    continue;
                var col = filters[i].gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = filters[i].sharedMesh;
            }
        }

        var waterMat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
        SpawnFbx(
            WaterMeshPath,
            "water_low",
            new Vector3(232.71f, 3.9872f, 55.241f),
            new Quaternion(-0.0020120596f, 0.6882547f, -0.013401477f, 0.7253426f),
            Vector3.one * 0.16735175f,
            root.transform,
            waterMat);
    }

    static GameObject SpawnFbx(
        string path,
        string name,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Transform parent,
        Material material)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"Missing {path}");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;

        if (material != null)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
        }

        return go;
    }

    static Vector3 FindFlatSpot(
        Terrain terrain, float u0, float u1, float v0, float v1, int seed)
    {
        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        Vector3 origin = terrain.transform.position;
        var rng = new System.Random(seed);
        Vector3 best = origin + new Vector3(size.x * (u0 + u1) * 0.5f, 0f, size.z * (v0 + v1) * 0.5f);
        float bestScore = float.MaxValue;

        for (int i = 0; i < 90; i++)
        {
            float u = u0 + (float)rng.NextDouble() * (u1 - u0);
            float v = v0 + (float)rng.NextDouble() * (v1 - v0);
            float steep = data.GetSteepness(u, v);
            float h = data.GetInterpolatedHeight(u, v);
            float score = steep * 4f + Mathf.Abs(h - size.y * 0.12f) * 0.03f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = origin + new Vector3(u * size.x, h, v * size.z);
        }

        return best;
    }

    static void CreatePadVisual(
        Transform parent, Vector3 pos, MapNodeKind kind, string id, int[] links, bool home)
    {
        var go = new GameObject();
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one;

        var marker = go.AddComponent<MapNodeMarker>();
        marker.Setup(kind, id, links);
        CastlePlatform.Ensure(marker);

        if (!home)
            return;

        var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "HomeFlag";
        flag.transform.SetParent(go.transform, false);
        flag.transform.localPosition = new Vector3(0f, 4.5f, 0f);
        flag.transform.localScale = new Vector3(0.35f, 6f, 0.12f);
        Object.DestroyImmediate(flag.GetComponent<Collider>());
        ApplyColor(flag, Color.white);
    }

    static void PlacePlayerOnPad(Vector3 spawnPoint)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        player.transform.position = spawnPoint + new Vector3(0f, 0.12f, 16f);
    }

    static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
#endif
