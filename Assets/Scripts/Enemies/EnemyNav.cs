using UnityEngine;

/// <summary>Shared ground snap and castle-exit helpers for walking enemies.</summary>
public static class EnemyNav
{
    // Луч на землю стартует чуть выше центра тела: так враг у стены не «находит землю» на её верхушке.
    const float GroundProbeUp = 0.5f;
    const float DoorExitDistance = 6f;

    public static float GroundY(Vector3 pos, float fallback, Transform self = null)
    {
        return EnemySenses.GroundBelow(pos, GroundProbeUp, self, fallback);
    }

    public static Vector3 Flatten(Vector3 v) => EnemySenses.Flatten(v);

    public static Transform FindPlayer() => EnemySenses.Player;

    public static Transform HomeCastleOf(Transform t)
    {
        if (t == null)
            return null;
        var castle = t.GetComponentInParent<SquareCastle>();
        return castle != null ? castle.transform : null;
    }

    public static Vector3 DoorExitPoint(Transform home, Vector3 fallback)
    {
        if (home == null)
            return fallback;

        Vector3 center = EnemySenses.CastleCenter(home);
        Vector3 exit = center + home.forward * (CarcassMetrics.FloorSize * 0.5f + DoorExitDistance);

        var doors = home.GetComponentsInChildren<InteractableDoor>(true);
        if (doors.Length > 0 && doors[0] != null)
        {
            Vector3 door = doors[0].transform.position;
            Vector3 away = Flatten(door - center);
            if (away.sqrMagnitude < 0.01f)
                away = Flatten(home.forward);
            exit = door + away.normalized * DoorExitDistance;
        }

        exit.y = GroundY(exit, fallback.y) + 0.05f;
        return exit;
    }

    public static Vector3 SteerFromWalls(Vector3 pos, Vector3 desired, float radius, Transform self)
    {
        Vector3 flat = Flatten(desired);
        if (flat.sqrMagnitude < 0.0001f)
            return desired;
        flat.Normalize();
        Vector3 origin = pos + Vector3.up * 0.3f;
        if (!Physics.SphereCast(origin, radius * 0.8f, flat, out RaycastHit hit, 1.8f, ~0, QueryTriggerInteraction.Ignore))
            return desired;
        if (hit.collider.transform.IsChildOf(self) || EnemySenses.IsPlayer(hit.collider) || EnemySenses.IsEnemy(hit.collider))
            return desired;
        // Пологие поверхности (пандусы, ступени) не обходим — по ним идём.
        if (hit.normal.y > 0.6f)
            return desired;

        Vector3 slide = Vector3.Cross(Vector3.up, hit.normal);
        if (Vector3.Dot(slide, flat) < 0f)
            slide = -slide;
        return (flat * 0.25f + Flatten(slide).normalized * 0.75f).normalized;
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
