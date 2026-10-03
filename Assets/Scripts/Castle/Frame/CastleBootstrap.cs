using UnityEngine;

/// <summary>
/// Spawns carcass player/enemy castles (shared columns, 2 wall bays per side) on map pads.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class CastleBootstrap : MonoBehaviour
{
    [SerializeField] float _yawOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Object.FindFirstObjectByType<CastleBootstrap>() != null)
            return;

        var host = new GameObject("CastleBootstrap");
        host.AddComponent<CastleBootstrap>();
    }

    void Start()
    {
        SpawnPlayerCastle();
        SpawnEnemyCastle();
        Destroy(gameObject);
    }

    void SpawnPlayerCastle()
    {
        if (FindFirstObjectByType<CastleFrame>() != null)
            return;

        MapNodeMarker home = FindNode(MapNodeKind.PlayerCastle);
        Vector3 center;
        float yaw = _yawOffset;

        if (home != null)
        {
            var platform = home.GetComponent<CastlePlatform>();
            center = platform != null ? platform.SpawnPoint : home.transform.position;
            Vector3 toOrigin = -home.transform.position;
            toOrigin.y = 0f;
            if (toOrigin.sqrMagnitude > 0.01f)
                yaw = Quaternion.LookRotation(toOrigin.normalized, Vector3.up).eulerAngles.y + _yawOffset;
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            center = player != null ? player.transform.position : Vector3.zero;
            SnapToTerrain(ref center);
        }

        CarcassBuilder.BuildPlayer(center, yaw);
    }

    void SpawnEnemyCastle()
    {
        if (FindFirstObjectByType<EnemyCastle>() != null)
            return;

        MapNodeMarker home = FindNode(MapNodeKind.PlayerCastle);
        MapNodeMarker enemy = FindNearestEnemyPad(home);

        Vector3 center;
        float yaw = 0f;

        if (enemy != null)
        {
            var platform = enemy.GetComponent<CastlePlatform>();
            center = platform != null ? platform.SpawnPoint : enemy.transform.position;
            if (home != null)
            {
                Vector3 toHome = home.transform.position - enemy.transform.position;
                toHome.y = 0f;
                if (toHome.sqrMagnitude > 0.01f)
                    yaw = Quaternion.LookRotation(toHome.normalized, Vector3.up).eulerAngles.y;
            }
        }
        else if (home != null)
        {
            center = home.transform.position + home.transform.forward * 55f;
            SnapToTerrain(ref center);
        }
        else
        {
            center = new Vector3(40f, 0f, 40f);
            SnapToTerrain(ref center);
        }

        CarcassBuilder.BuildEnemy(center, yaw);
    }

    static MapNodeMarker FindNearestEnemyPad(MapNodeMarker home)
    {
        var nodes = FindObjectsByType<MapNodeMarker>(FindObjectsSortMode.None);
        MapNodeMarker best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = home != null ? home.transform.position : Vector3.zero;

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null || n.Kind != MapNodeKind.EnemyCastle)
                continue;

            float d = home != null
                ? (n.transform.position - origin).sqrMagnitude
                : n.transform.position.sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = n;
            }
        }

        return best;
    }

    static void SnapToTerrain(ref Vector3 center)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain != null)
            center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
    }

    static MapNodeMarker FindNode(MapNodeKind kind)
    {
        var nodes = FindObjectsByType<MapNodeMarker>(FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null && nodes[i].Kind == kind)
                return nodes[i];
        }
        return null;
    }
}
