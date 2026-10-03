using UnityEngine;

/// <summary>Shared ground snap and castle-exit helpers for walking enemies.</summary>
public static class EnemyNav
{
    public static float GroundY(Vector3 pos, float fallback)
    {
        Vector3 origin = pos + Vector3.up * 8f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 24f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
                return fallback;
            return hit.point.y;
        }

        var terrain = Terrain.activeTerrain;
        if (terrain != null)
            return terrain.SampleHeight(pos) + terrain.transform.position.y;
        return fallback;
    }

    public static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    public static Transform FindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.transform : null;
    }

    public static Vector3 DoorExitPoint(SquareCastle home, Vector3 fallback)
    {
        Transform root = home != null ? home.transform : null;
        return DoorExitPoint(root, fallback);
    }

    public static Vector3 DoorExitPoint(Transform home, Vector3 fallback)
    {
        if (home == null)
            return fallback;

        Vector3 center = home.position;
        Vector3 exit = home.TransformPoint(new Vector3(0f, 0f, 12f));
        bool found = false;

        var doors = home.GetComponentsInChildren<InteractableDoor>(true);
        if (doors != null && doors.Length > 0 && doors[0] != null)
        {
            exit = doors[0].transform.position;
            found = true;
        }

        if (!found)
        {
            var modules = home.GetComponentsInChildren<CastleModuleRoot>(true);
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null || modules[i].Kind != CastleModuleKind.Door)
                    continue;
                exit = modules[i].transform.position;
                found = true;
                break;
            }
        }

        if (found)
        {
            Vector3 away = Flatten(exit - center);
            if (away.sqrMagnitude < 0.01f)
                away = Flatten(home.forward);
            exit += away.normalized * 6f;
        }

        exit.y = GroundY(exit, fallback.y) + 0.05f;
        return exit;
    }

    public static Vector3 SteerFromWalls(Vector3 pos, Vector3 desired, float radius)
    {
        Vector3 flat = Flatten(desired);
        if (flat.sqrMagnitude < 0.0001f)
            return desired;
        flat.Normalize();
        Vector3 origin = pos + Vector3.up * 0.8f;
        if (Physics.SphereCast(origin, radius * 0.7f, flat, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
                return desired;
            Vector3 slide = Vector3.Cross(Vector3.up, hit.normal);
            if (Vector3.Dot(slide, flat) < 0f)
                slide = -slide;
            return (flat * 0.25f + Flatten(slide).normalized * 0.75f).normalized;
        }

        return desired;
    }

    public static void IgnoreCastle(Collider self, SquareCastle castle, bool ignore)
    {
        if (self == null || castle == null)
            return;
        IgnoreCastle(self, castle.transform, ignore);
    }

    public static void IgnoreCastle(Collider self, Transform castle, bool ignore)
    {
        if (self == null || castle == null)
            return;
        var cols = castle.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i] != self)
                Physics.IgnoreCollision(self, cols[i], ignore);
        }
    }
}
