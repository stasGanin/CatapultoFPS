using UnityEngine;

/// <summary>
/// Snaps a station footprint to the 1 m grid on a carcass section floor and checks it is free.
/// Grid lives in castle-local space: section cells start at multiples of GridPitch (13 m),
/// so integer local coordinates are shared by all sections.
/// </summary>
public static class StationPlacement
{
    const float MaxRange = 10f;
    // Внутренняя зона клетки: от внутренней грани стены до противоположной (стены — первый и последний метр).
    const float InnerMin = 1f;
    const float InnerMax = 13f;
    const float OverlapInset = 0.06f;

    static readonly RaycastHit[] Hits = new RaycastHit[16];
    static readonly Collider[] Overlaps = new Collider[32];

    public struct Result
    {
        public Vector3 LocalCenter;
        public Quaternion LocalRotation;
        public bool IsValid;
    }

    /// <summary>False when the crosshair isn't on a floor of this castle at all.</summary>
    public static bool TrySnap(CarcassCastle castle, Ray ray, StationDefinition def, int quarterTurns, out Result result)
    {
        result = default;
        if (!TryHitFloor(castle, ray, out Vector3 hitLocal))
            return false;

        Vector2Int fp = def.Footprint;
        bool rotated = (quarterTurns & 1) == 1;
        int w = rotated ? fp.y : fp.x;
        int d = rotated ? fp.x : fp.y;

        int minX = Mathf.RoundToInt(hitLocal.x - w * 0.5f);
        int minZ = Mathf.RoundToInt(hitLocal.z - d * 0.5f);
        int floorY = Mathf.Max(0, Mathf.RoundToInt((hitLocal.y - CarcassMetrics.FloorThickness) / CarcassMetrics.ColumnHeight));
        float floorTop = floorY * CarcassMetrics.ColumnHeight + CarcassMetrics.FloorThickness;

        result.LocalCenter = new Vector3(minX + w * 0.5f, floorTop, minZ + d * 0.5f);
        result.LocalRotation = Quaternion.Euler(0f, 90f * quarterTurns, 0f);
        result.IsValid = InsideOneSection(castle, minX, minZ, w, d, floorY)
                         && IsFree(castle, result.LocalCenter, w, d, def.Height);
        return true;
    }

    static bool TryHitFloor(CarcassCastle castle, Ray ray, out Vector3 hitLocal)
    {
        hitLocal = default;
        int count = Physics.RaycastNonAlloc(ray, Hits, MaxRange, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var hit = Hits[i];
            if (hit.distance >= best || hit.normal.y < 0.7f)
                continue;
            if (!hit.collider.transform.IsChildOf(castle.FloorsRoot))
                continue;
            best = hit.distance;
            hitLocal = castle.transform.InverseTransformPoint(hit.point);
            found = true;
        }

        return found;
    }

    /// <summary>Весь footprint внутри одной секции этого этажа, не на линии стен.</summary>
    static bool InsideOneSection(CarcassCastle castle, int minX, int minZ, int w, int d, int floorY)
    {
        int cx = Mathf.FloorToInt((minX - InnerMin) / CarcassMetrics.GridPitch);
        int cz = Mathf.FloorToInt((minZ - InnerMin) / CarcassMetrics.GridPitch);
        if (!castle.HasCell(cx, floorY, cz))
            return false;
        float baseX = cx * CarcassMetrics.GridPitch, baseZ = cz * CarcassMetrics.GridPitch;
        return minX >= baseX + InnerMin && minX + w <= baseX + InnerMax
               && minZ >= baseZ + InnerMin && minZ + d <= baseZ + InnerMax;
    }

    /// <summary>
    /// Триггеры тоже считаем: проём двери и пятачок у лестницы — это триггеры, туда ставить нельзя.
    /// Пол своего замка и игрок не мешают.
    /// </summary>
    static bool IsFree(CarcassCastle castle, Vector3 localCenter, int w, int d, float height)
    {
        Vector3 center = castle.transform.TransformPoint(localCenter + Vector3.up * (height * 0.5f + OverlapInset));
        var half = new Vector3(w * 0.5f - OverlapInset, height * 0.5f, d * 0.5f - OverlapInset);
        int count = Physics.OverlapBoxNonAlloc(center, half, Overlaps, castle.transform.rotation, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = Overlaps[i];
            if (col.transform.IsChildOf(castle.FloorsRoot) || EnemySenses.IsPlayer(col))
                continue;
            return false;
        }

        return true;
    }
}
