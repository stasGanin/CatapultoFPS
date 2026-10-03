using UnityEngine;

/// <summary>
/// Art kit sizes in meters. FBX is authored at 0.01 (Maya cm); runtime scale is 100.
/// Adjacent cells share a 1 m column: pitch = floor - column.
/// </summary>
public static class CarcassMetrics
{
    public const float ColumnSize = 1f;
    public const float ColumnHeight = 3.36f;
    public const float FloorSize = 14f;
    public const float GridPitch = 13f;
    public const float FloorThickness = 0.37f;
    public const float RoofThickness = 0.37f;
    public const float WallAlong = 6f;
    public const float WallThickness = 1f;
    public const float WallHeight = 3.36f;
    public const int BaysPerEdge = 2;
    public const int MaxPlanRadius = 1;
    public const int MaxFloors = 2;

    public enum WallDir
    {
        N = 0,
        E = 1,
        S = 2,
        W = 3
    }

    public static Vector3 VertexLocal(int vx, int vz)
    {
        return new Vector3(vx * GridPitch, 0f, vz * GridPitch);
    }

    public static Vector3 ColumnLocal(int vx, int floorY, int vz)
    {
        Vector3 v = VertexLocal(vx, vz);
        v.x += ColumnSize * 0.5f;
        v.z += ColumnSize * 0.5f;
        v.y = floorY * ColumnHeight;
        return v;
    }

    public static Vector3 CellCenterLocal(int cx, int floorY, int cz)
    {
        return new Vector3(
            cx * GridPitch + FloorSize * 0.5f,
            floorY * ColumnHeight,
            cz * GridPitch + FloorSize * 0.5f);
    }

    public static Vector3 FloorLocal(int cx, int floorY, int cz)
    {
        Vector3 c = CellCenterLocal(cx, floorY, cz);
        return c;
    }

    public static Vector3 RoofLocal(int cx, int floorY, int cz)
    {
        Vector3 c = CellCenterLocal(cx, floorY, cz);
        c.y = floorY * ColumnHeight + ColumnHeight;
        return c;
    }

    public static bool InPlanCap(int cx, int cz)
    {
        return Mathf.Abs(cx) <= MaxPlanRadius && Mathf.Abs(cz) <= MaxPlanRadius;
    }

    public static Vector2Int DirToDelta(WallDir dir)
    {
        switch (dir)
        {
            case WallDir.N: return new Vector2Int(0, 1);
            case WallDir.E: return new Vector2Int(1, 0);
            case WallDir.S: return new Vector2Int(0, -1);
            default: return new Vector2Int(-1, 0);
        }
    }

    public static Quaternion WallRotation(WallDir dir)
    {
        // Kit meshes are long on local Z. N/S edges run along X, so start at 90°.
        return Quaternion.Euler(0f, (int)dir * 90f + 90f, 0f);
    }

    public static Vector3 MerlonLocal(int cx, int floorY, int cz, WallDir dir, int slot)
    {
        Vector3 p = BayLocal(cx, floorY, cz, dir, slot);
        p.y = floorY * ColumnHeight + ColumnHeight + RoofThickness;
        return p;
    }

    /// <summary>Отступ центра башни от внешних краёв угловой клетки: башня стоит на крыше, а не свисает с неё.</summary>
    public const float TowerCornerInset = 2.4f;

    /// <summary>Основание башни на крыше угловой клетки (cx, floorY, cz); dx/dz — сторона угла (0 = западная/южная).</summary>
    public static Vector3 TowerBaseLocal(int cx, int cz, int dx, int dz, int floorY)
    {
        return new Vector3(
            cx * GridPitch + (dx == 0 ? TowerCornerInset : FloorSize - TowerCornerInset),
            floorY * ColumnHeight + ColumnHeight + RoofThickness,
            cz * GridPitch + (dz == 0 ? TowerCornerInset : FloorSize - TowerCornerInset));
    }

    public static Vector3 BayLocal(int cx, int floorY, int cz, WallDir dir, int slot)
    {
        int s = slot == 0 ? 0 : 1;
        float along = ColumnSize + WallAlong * 0.5f + s * WallAlong;
        float y = floorY * ColumnHeight + FloorThickness;
        float inner = ColumnSize * 0.5f;
        float outer = FloorSize - ColumnSize * 0.5f;

        switch (dir)
        {
            case WallDir.N:
                return new Vector3(cx * GridPitch + along, y, cz * GridPitch + outer);
            case WallDir.E:
                return new Vector3(cx * GridPitch + outer, y, cz * GridPitch + along);
            case WallDir.S:
                return new Vector3(cx * GridPitch + along, y, cz * GridPitch + inner);
            default:
                return new Vector3(cx * GridPitch + inner, y, cz * GridPitch + along);
        }
    }
}
