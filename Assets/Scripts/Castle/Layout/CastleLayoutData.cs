using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Serializable castle layout (GDD v0.5). Saved as JSON under Resources/Castle/Layouts.</summary>
[Serializable]
public sealed class CastleLayoutData
{
    public int version = 1;
    public List<SectionCell> sections = new List<SectionCell>();
    public List<OpeningFill> openings = new List<OpeningFill>();
    public List<PowerFill> powers = new List<PowerFill>();
    public List<SmallFill> smalls = new List<SmallFill>();

    [Serializable]
    public struct SectionCell
    {
        public int x;
        public int y;
        public int z;
    }

    [Serializable]
    public struct OpeningFill
    {
        public int x;
        public int y;
        public int z;
        public int wall;
        public int col;
        public int row;
        public int kind;
    }

    [Serializable]
    public struct PowerFill
    {
        public int x;
        public int z;
        public int corner;
        public int kind;
    }

    [Serializable]
    public struct SmallFill
    {
        public int x;
        public int y;
        public int z;
        public int index;
        public int kind;
    }

    public const int OpeningDoor = 1;
    public const int OpeningWindow = 2;
    public const int PowerTower = 1;
    public const int PowerHeavy = 2;
    public const int SmallChest = 1;
    public const int SmallWorkbench = 2;
    public const int SmallPlanter = 3;

    public static CastleLayoutData DefaultPlayer()
    {
        var data = new CastleLayoutData();
        data.sections.Add(new SectionCell());
        data.openings.Add(new OpeningFill
        {
            wall = (int)CastleBuildMetrics.WallDir.N,
            col = 0,
            row = 0,
            kind = OpeningDoor
        });
        return data;
    }

    public static CastleLayoutData DefaultEnemy()
    {
        var data = DefaultPlayer();
        data.powers.Add(new PowerFill
        {
            corner = (int)CastleBuildMetrics.CornerId.NE,
            kind = PowerTower
        });
        data.smalls.Add(new SmallFill { index = 0, kind = SmallChest });
        return data;
    }

    public bool HasSection(int x, int y, int z)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            if (s.x == x && s.y == y && s.z == z)
                return true;
        }

        return false;
    }

    public int FloorCount(int x, int z)
    {
        int n = 0;
        for (int y = 0; y < CastleBuildMetrics.MaxFloors; y++)
        {
            if (HasSection(x, y, z))
                n++;
            else
                break;
        }

        return n;
    }

    public bool TryAddSection(int x, int y, int z)
    {
        if (HasSection(x, y, z))
            return false;
        if (y < 0 || y >= CastleBuildMetrics.MaxFloors)
            return false;
        if (y > 0 && !HasSection(x, y - 1, z))
            return false;
        if (!FitsPlan(x, z))
            return false;

        sections.Add(new SectionCell { x = x, y = y, z = z });
        return true;
    }

    public bool RemoveSection(int x, int y, int z)
    {
        if (y < CastleBuildMetrics.MaxFloors - 1 && HasSection(x, y + 1, z))
            return false;

        for (int i = sections.Count - 1; i >= 0; i--)
        {
            var s = sections[i];
            if (s.x != x || s.y != y || s.z != z)
                continue;
            sections.RemoveAt(i);
            PurgeCell(x, y, z);
            return true;
        }

        return false;
    }

    void PurgeCell(int x, int y, int z)
    {
        for (int i = openings.Count - 1; i >= 0; i--)
        {
            var o = openings[i];
            if (o.x == x && o.y == y && o.z == z)
                openings.RemoveAt(i);
        }

        for (int i = smalls.Count - 1; i >= 0; i--)
        {
            var s = smalls[i];
            if (s.x == x && s.y == y && s.z == z)
                smalls.RemoveAt(i);
        }

        if (!HasSection(x, 0, z) && !HasSection(x, 1, z))
        {
            for (int i = powers.Count - 1; i >= 0; i--)
            {
                var p = powers[i];
                if (p.x == x && p.z == z)
                    powers.RemoveAt(i);
            }
        }
    }

    bool FitsPlan(int x, int z)
    {
        int minX = x;
        int maxX = x;
        int minZ = z;
        int maxZ = z;
        for (int i = 0; i < sections.Count; i++)
        {
            minX = Mathf.Min(minX, sections[i].x);
            maxX = Mathf.Max(maxX, sections[i].x);
            minZ = Mathf.Min(minZ, sections[i].z);
            maxZ = Mathf.Max(maxZ, sections[i].z);
        }

        return (maxX - minX + 1) <= CastleBuildMetrics.MaxPlan
            && (maxZ - minZ + 1) <= CastleBuildMetrics.MaxPlan;
    }

    public void GetPlanBounds(out int minX, out int maxX, out int minZ, out int maxZ)
    {
        minX = maxX = minZ = maxZ = 0;
        if (sections.Count == 0)
            return;

        minX = maxX = sections[0].x;
        minZ = maxZ = sections[0].z;
        for (int i = 1; i < sections.Count; i++)
        {
            minX = Mathf.Min(minX, sections[i].x);
            maxX = Mathf.Max(maxX, sections[i].x);
            minZ = Mathf.Min(minZ, sections[i].z);
            maxZ = Mathf.Max(maxZ, sections[i].z);
        }
    }

    public bool IsOuterPowerCorner(int x, int z, CastleBuildMetrics.CornerId corner)
    {
        if (!HasSection(x, 0, z) && !HasSection(x, 1, z))
            return false;

        GetPlanBounds(out int minX, out int maxX, out int minZ, out int maxZ);
        switch (corner)
        {
            case CastleBuildMetrics.CornerId.NE:
                return x == maxX && z == maxZ;
            case CastleBuildMetrics.CornerId.SE:
                return x == maxX && z == minZ;
            case CastleBuildMetrics.CornerId.SW:
                return x == minX && z == minZ;
            default:
                return x == minX && z == maxZ;
        }
    }

    public int GetOpening(int x, int y, int z, int wall, int col, int row)
    {
        for (int i = 0; i < openings.Count; i++)
        {
            var o = openings[i];
            if (o.x == x && o.y == y && o.z == z && o.wall == wall && o.col == col && o.row == row)
                return o.kind;
        }

        return 0;
    }

    public void SetOpening(int x, int y, int z, int wall, int col, int row, int kind)
    {
        for (int i = 0; i < openings.Count; i++)
        {
            var o = openings[i];
            if (o.x != x || o.y != y || o.z != z || o.wall != wall || o.col != col || o.row != row)
                continue;
            if (kind == 0)
                openings.RemoveAt(i);
            else
            {
                o.kind = kind;
                openings[i] = o;
            }

            return;
        }

        if (kind != 0)
        {
            openings.Add(new OpeningFill
            {
                x = x, y = y, z = z, wall = wall, col = col, row = row, kind = kind
            });
        }
    }

    public int GetPower(int x, int z, int corner)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            var p = powers[i];
            if (p.x == x && p.z == z && p.corner == corner)
                return p.kind;
        }

        return 0;
    }

    public void SetPower(int x, int z, int corner, int kind)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            var p = powers[i];
            if (p.x != x || p.z != z || p.corner != corner)
                continue;
            if (kind == 0)
                powers.RemoveAt(i);
            else
            {
                p.kind = kind;
                powers[i] = p;
            }

            return;
        }

        if (kind != 0)
            powers.Add(new PowerFill { x = x, z = z, corner = corner, kind = kind });
    }

    public int GetSmall(int x, int y, int z, int index)
    {
        for (int i = 0; i < smalls.Count; i++)
        {
            var s = smalls[i];
            if (s.x == x && s.y == y && s.z == z && s.index == index)
                return s.kind;
        }

        return 0;
    }

    public void SetSmall(int x, int y, int z, int index, int kind)
    {
        for (int i = 0; i < smalls.Count; i++)
        {
            var s = smalls[i];
            if (s.x != x || s.y != y || s.z != z || s.index != index)
                continue;
            if (kind == 0)
                smalls.RemoveAt(i);
            else
            {
                s.kind = kind;
                smalls[i] = s;
            }

            return;
        }

        if (kind != 0)
        {
            smalls.Add(new SmallFill { x = x, y = y, z = z, index = index, kind = kind });
        }
    }
}
