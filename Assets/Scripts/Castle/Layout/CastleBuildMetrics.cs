using UnityEngine;

/// <summary>GDD v0.5 section metrics. Placeholder art must match these pivots.</summary>
public static class CastleBuildMetrics
{
    public const float SectionSize = 8f;
    public const float FloorHeight = 4f;
    public const float WallThickness = 0.45f;
    public const float RoofThickness = 0.2f;
    public const float FloorThickness = 0.12f;
    public const float OpeningCellWidth = 3.2f;
    public const float OpeningCellHeight = 1.85f;
    public const float OpeningBand = 0.15f;
    public const float CornerReserve = 0.8f;
    public const float TowerOutset = 1.1f;
    public const float TowerBase = 1.8f;
    public const float Tower1FHeight = 4.5f;
    public const float Tower2FHeight = 8.5f;
    public const float SmallPad = 0.9f;
    public const float HeavyPad = 1.6f;
    public const float SmallInset = 0.95f;
    public const int SmallSlotsPerSection = 8;
    public const int MaxPlan = 3;
    public const int MaxFloors = 2;

    public enum WallDir
    {
        N = 0,
        E = 1,
        S = 2,
        W = 3
    }

    public enum CornerId
    {
        NE = 0,
        SE = 1,
        SW = 2,
        NW = 3
    }

    public static Vector3 CellOrigin(int x, int y, int z)
    {
        return new Vector3(x * SectionSize, y * FloorHeight, z * SectionSize);
    }

    public static Vector2Int CornerCellOffset(CornerId corner)
    {
        switch (corner)
        {
            case CornerId.NE: return new Vector2Int(1, 1);
            case CornerId.SE: return new Vector2Int(1, -1);
            case CornerId.SW: return new Vector2Int(-1, -1);
            default: return new Vector2Int(-1, 1);
        }
    }

    public static Vector3 CornerLocalOnFloor(CornerId corner)
    {
        float h = SectionSize * 0.5f;
        Vector2Int d = CornerCellOffset(corner);
        return new Vector3(d.x * h, 0f, d.y * h);
    }

    public static Vector3 TowerLocal(CornerId corner)
    {
        Vector3 c = CornerLocalOnFloor(corner);
        Vector2Int d = CornerCellOffset(corner);
        return new Vector3(
            c.x + d.x * TowerOutset,
            0f,
            c.z + d.y * TowerOutset);
    }

    public static Vector3 HeavyLocal(CornerId corner)
    {
        Vector3 c = CornerLocalOnFloor(corner);
        Vector2Int d = CornerCellOffset(corner);
        return new Vector3(
            c.x - d.x * 1.15f,
            FloorThickness,
            c.z - d.y * 1.15f);
    }

    public static Vector3 OpeningLocal(WallDir wall, int col, int row)
    {
        float along = -OpeningCellWidth + OpeningCellWidth * 0.5f + col * OpeningCellWidth;
        float y = OpeningBand + OpeningCellHeight * 0.5f + row * OpeningCellHeight;
        float edge = SectionSize * 0.5f - WallThickness * 0.5f;

        // col 0 = left when looking at the wall from inside the room.
        switch (wall)
        {
            case WallDir.N:
                return new Vector3(along, y, edge);
            case WallDir.E:
                return new Vector3(edge, y, -along);
            case WallDir.S:
                return new Vector3(-along, y, -edge);
            default:
                return new Vector3(-edge, y, along);
        }
    }

    public static Vector3 WallCenterLocal(WallDir wall)
    {
        float edge = SectionSize * 0.5f - WallThickness * 0.5f;
        float y = FloorHeight * 0.5f;
        switch (wall)
        {
            case WallDir.N: return new Vector3(0f, y, edge);
            case WallDir.E: return new Vector3(edge, y, 0f);
            case WallDir.S: return new Vector3(0f, y, -edge);
            default: return new Vector3(-edge, y, 0f);
        }
    }

    public static Quaternion WallRotation(WallDir wall)
    {
        return Quaternion.Euler(0f, (int)wall * 90f, 0f);
    }

    /// <summary>Small pads: two per inner wall, index 0..7 (N,E,S,W).</summary>
    public static Vector3 SmallLocal(int index)
    {
        int wall = Mathf.Clamp(index, 0, 7) / 2;
        int which = index % 2;
        float along = which == 0 ? -2f : 2f;
        float inner = SectionSize * 0.5f - WallThickness - SmallInset;
        float y = FloorThickness;
        switch ((WallDir)wall)
        {
            case WallDir.N: return new Vector3(along, y, inner);
            case WallDir.E: return new Vector3(inner, y, -along);
            case WallDir.S: return new Vector3(-along, y, -inner);
            default: return new Vector3(-inner, y, along);
        }
    }

    public static void WorldToCell(Vector3 local, out int x, out int y, out int z)
    {
        x = Mathf.RoundToInt(local.x / SectionSize);
        y = Mathf.Clamp(Mathf.RoundToInt(local.y / FloorHeight), 0, MaxFloors - 1);
        z = Mathf.RoundToInt(local.z / SectionSize);
    }
}
