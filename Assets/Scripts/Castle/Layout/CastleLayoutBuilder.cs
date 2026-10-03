using UnityEngine;

/// <summary>Builds placeholder section geometry from a layout (GDD v0.5 metrics).</summary>
public static class CastleLayoutBuilder
{
    static readonly Color FloorColor = new Color(0.55f, 0.5f, 0.42f, 1f);
    static readonly Color WallColor = new Color(0.78f, 0.72f, 0.62f, 1f);
    static readonly Color RoofColor = new Color(0.32f, 0.28f, 0.26f, 0.95f);
    static readonly Color DoorColor = new Color(0.42f, 0.28f, 0.16f, 1f);
    static readonly Color WindowColor = new Color(0.45f, 0.7f, 0.85f, 0.45f);
    static readonly Color TowerColor = new Color(0.72f, 0.22f, 0.18f, 1f);
    static readonly Color HeavyColor = new Color(0.55f, 0.32f, 0.7f, 1f);
    static readonly Color ChestColor = new Color(0.55f, 0.38f, 0.18f, 1f);
    static readonly Color BenchColor = new Color(0.4f, 0.38f, 0.32f, 1f);
    static readonly Color PlanterColor = new Color(0.28f, 0.48f, 0.22f, 1f);
    static readonly Color SocketColor = new Color(0.2f, 0.85f, 1f, 0.35f);
    static readonly Color PowerSocketColor = new Color(1f, 0.55f, 0.15f, 0.4f);
    static readonly Color SmallSocketColor = new Color(0.95f, 0.85f, 0.2f, 0.35f);

    public static CastleLayoutHost Spawn(Vector3 center, float yawDegrees, bool playerOwned, CastleLayoutData data, string saveId)
    {
        string name = playerOwned ? "PlayerCastle" : "EnemyCastle";
        var root = new GameObject(name);
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        var square = root.AddComponent<SquareCastle>();
        square.SetPlayerOwned(playerOwned);

        var host = root.AddComponent<CastleLayoutHost>();
        host.Bind(data ?? CastleLayoutData.DefaultPlayer(), saveId, playerOwned);
        host.Rebuild();
        return host;
    }

    public static void Build(CastleLayoutHost host)
    {
        if (host == null || host.Data == null)
            return;

        Transform root = host.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
            Object.Destroy(root.GetChild(i).gameObject);

        var geo = new GameObject("Geometry");
        geo.transform.SetParent(root, false);
        var socks = new GameObject("EditorSockets");
        socks.transform.SetParent(root, false);

        var data = host.Data;
        for (int i = 0; i < data.sections.Count; i++)
        {
            var cell = data.sections[i];
            BuildSection(geo.transform, socks.transform, data, cell.x, cell.y, cell.z, host.ShowSockets);
        }

        BuildPowers(geo.transform, socks.transform, data, host.ShowSockets);

        if (!host.PlayerOwned)
            EnsureEnemyCastle(root);

        SpawnMage(root, data, host.PlayerOwned);
    }

    static void BuildSection(
        Transform geo,
        Transform socks,
        CastleLayoutData data,
        int x,
        int y,
        int z,
        bool showSockets)
    {
        var section = new GameObject($"Section_{x}_{y}_{z}");
        section.transform.SetParent(geo, false);
        section.transform.localPosition = CastleBuildMetrics.CellOrigin(x, y, z);

        float size = CastleBuildMetrics.SectionSize;
        float h = CastleBuildMetrics.FloorHeight;
        float wallT = CastleBuildMetrics.WallThickness;
        float floorT = CastleBuildMetrics.FloorThickness;

        CastlePrim.Cube(
            "Floor",
            section.transform,
            new Vector3(0f, floorT * 0.5f, 0f),
            new Vector3(size, floorT, size),
            FloorColor);

        if (y == CastleBuildMetrics.MaxFloors - 1 || !data.HasSection(x, y + 1, z))
        {
            CastlePrim.Cube(
                "Roof",
                section.transform,
                new Vector3(0f, h - CastleBuildMetrics.RoofThickness * 0.5f, 0f),
                new Vector3(size, CastleBuildMetrics.RoofThickness, size),
                RoofColor);
        }

        var sectionSock = CastlePrim.Cube(
            "SectionHit",
            socks,
            CastleBuildMetrics.CellOrigin(x, y, z) + Vector3.up * 0.04f,
            new Vector3(size * 0.92f, 0.08f, size * 0.92f),
            new Color(0.2f, 1f, 0.4f, 0.12f),
            trigger: true);
        sectionSock.transform.SetParent(socks, true);
        var secMarker = sectionSock.AddComponent<CastleEditorSocket>();
        secMarker.SocketKind = CastleEditorSocket.Kind.Section;
        secMarker.X = x;
        secMarker.Y = y;
        secMarker.Z = z;
        sectionSock.SetActive(showSockets);

        for (int w = 0; w < 4; w++)
            BuildWall(section.transform, socks, data, x, y, z, (CastleBuildMetrics.WallDir)w, showSockets);

        for (int i = 0; i < CastleBuildMetrics.SmallSlotsPerSection; i++)
        {
            Vector3 local = CastleBuildMetrics.SmallLocal(i);
            int kind = data.GetSmall(x, y, z, i);
            if (kind == CastleLayoutData.SmallChest)
                BuildSmallProp(section.transform, local, "Prop_Chest", ChestColor, 0.85f, 0.7f);
            else if (kind == CastleLayoutData.SmallWorkbench)
                BuildSmallProp(section.transform, local, "Prop_Workbench", BenchColor, 1.1f, 0.85f);
            else if (kind == CastleLayoutData.SmallPlanter)
                BuildSmallProp(section.transform, local, "Prop_Planter", PlanterColor, 0.55f, 0.7f);

            var pad = CastlePrim.Cube(
                $"Small_{i}",
                socks,
                Vector3.zero,
                Vector3.one * (CastleBuildMetrics.SmallPad + 0.05f),
                SmallSocketColor,
                trigger: true);
            pad.transform.position = section.transform.TransformPoint(local + Vector3.up * 0.2f);
            var marker = pad.AddComponent<CastleEditorSocket>();
            marker.SocketKind = CastleEditorSocket.Kind.Small;
            marker.X = x;
            marker.Y = y;
            marker.Z = z;
            marker.Index = i;
            pad.SetActive(showSockets);
            if (showSockets)
                CastlePrim.Label(pad.transform, Vector3.zero, $"Small_{i}", Color.yellow);
        }
    }

    static void BuildSmallProp(Transform parent, Vector3 local, string name, Color color, float height, float width)
    {
        CastlePrim.Cube(
            name,
            parent,
            local + Vector3.up * (height * 0.5f),
            new Vector3(width, height, width),
            color);
    }

    static void BuildWall(
        Transform section,
        Transform socks,
        CastleLayoutData data,
        int x,
        int y,
        int z,
        CastleBuildMetrics.WallDir wall,
        bool showSockets)
    {
        float size = CastleBuildMetrics.SectionSize;
        float h = CastleBuildMetrics.FloorHeight;
        float t = CastleBuildMetrics.WallThickness;
        float cellW = CastleBuildMetrics.OpeningCellWidth;
        float cellH = CastleBuildMetrics.OpeningCellHeight;

        Vector3 center = CastleBuildMetrics.WallCenterLocal(wall);
        bool alongX = wall == CastleBuildMetrics.WallDir.N || wall == CastleBuildMetrics.WallDir.S;

        // Solid bands: left/right corner reserves + thin floor/roof strips, then 2x2 cells.
        float reserve = CastleBuildMetrics.CornerReserve;
        Vector3 wallScale = alongX
            ? new Vector3(size, h, t)
            : new Vector3(t, h, size);

        // Four opening cells replace the middle of the wall; leftover is one hull cube minus holes via 2x2 boxes only.
        for (int col = 0; col < 2; col++)
        {
            for (int row = 0; row < 2; row++)
            {
                int fill = data.GetOpening(x, y, z, (int)wall, col, row);
                Vector3 cellLocal = CastleBuildMetrics.OpeningLocal(wall, col, row);
                Vector3 cellScale = alongX
                    ? new Vector3(cellW, cellH, t)
                    : new Vector3(t, cellH, cellW);

                if (fill == CastleLayoutData.OpeningDoor)
                {
                    BuildDoor(section, wall, cellLocal, alongX, cellW, cellH, t);
                }
                else if (fill == CastleLayoutData.OpeningWindow)
                {
                    BuildWindow(section, wall, cellLocal, alongX, cellW, cellH, t);
                }
                else
                {
                    CastlePrim.Cube($"Wall_{wall}_{col}_{row}", section, cellLocal, cellScale, WallColor);
                }

                var sock = CastlePrim.Cube(
                    $"Opening_{wall}_{col}_{row}",
                    socks,
                    Vector3.zero,
                    cellScale * 0.92f,
                    SocketColor,
                    trigger: true);
                sock.transform.position = section.TransformPoint(cellLocal);
                var marker = sock.AddComponent<CastleEditorSocket>();
                marker.SocketKind = CastleEditorSocket.Kind.Opening;
                marker.X = x;
                marker.Y = y;
                marker.Z = z;
                marker.Wall = (int)wall;
                marker.Col = col;
                marker.Row = row;
                sock.SetActive(showSockets);
                if (showSockets)
                {
                    string tag = fill == CastleLayoutData.OpeningDoor ? "DOOR"
                        : fill == CastleLayoutData.OpeningWindow ? "WIN"
                        : $"O {wall} {col}{row}";
                    CastlePrim.Label(sock.transform, Vector3.zero, tag, Color.cyan);
                }
            }
        }

        // Corner pillars + top/bottom strips so the wall is not only 2x2 holes.
        BuildWallFrame(section, wall, alongX, size, h, t, reserve, cellW, cellH);
        _ = center;
        _ = wallScale;
    }

    static void BuildWallFrame(
        Transform section,
        CastleBuildMetrics.WallDir wall,
        bool alongX,
        float size,
        float h,
        float t,
        float reserve,
        float cellW,
        float cellH)
    {
        Vector3 center = CastleBuildMetrics.WallCenterLocal(wall);
        float mid = cellW; // two cells = 6.4, leftovers on each side = reserve
        _ = mid;
        _ = cellH;

        if (alongX)
        {
            float z = center.z;
            CastlePrim.Cube("WallEndL", section, new Vector3(-(size * 0.5f - reserve * 0.5f), h * 0.5f, z), new Vector3(reserve, h, t), WallColor);
            CastlePrim.Cube("WallEndR", section, new Vector3(size * 0.5f - reserve * 0.5f, h * 0.5f, z), new Vector3(reserve, h, t), WallColor);
        }
        else
        {
            float x = center.x;
            CastlePrim.Cube("WallEndL", section, new Vector3(x, h * 0.5f, -(size * 0.5f - reserve * 0.5f)), new Vector3(t, h, reserve), WallColor);
            CastlePrim.Cube("WallEndR", section, new Vector3(x, h * 0.5f, size * 0.5f - reserve * 0.5f), new Vector3(t, h, reserve), WallColor);
        }
    }

    static void BuildDoor(Transform section, CastleBuildMetrics.WallDir wall, Vector3 cellLocal, bool alongX, float cellW, float cellH, float t)
    {
        float post = 0.22f;
        float lintel = 0.28f;
        if (alongX)
        {
            CastlePrim.Cube("DoorPostL", section, cellLocal + new Vector3(-(cellW * 0.5f - post * 0.5f), 0f, 0f), new Vector3(post, cellH, t), DoorColor);
            CastlePrim.Cube("DoorPostR", section, cellLocal + new Vector3(cellW * 0.5f - post * 0.5f, 0f, 0f), new Vector3(post, cellH, t), DoorColor);
            CastlePrim.Cube("DoorLintel", section, cellLocal + new Vector3(0f, cellH * 0.5f - lintel * 0.5f, 0f), new Vector3(cellW, lintel, t), DoorColor);
        }
        else
        {
            CastlePrim.Cube("DoorPostL", section, cellLocal + new Vector3(0f, 0f, -(cellW * 0.5f - post * 0.5f)), new Vector3(t, cellH, post), DoorColor);
            CastlePrim.Cube("DoorPostR", section, cellLocal + new Vector3(0f, 0f, cellW * 0.5f - post * 0.5f), new Vector3(t, cellH, post), DoorColor);
            CastlePrim.Cube("DoorLintel", section, cellLocal + new Vector3(0f, cellH * 0.5f - lintel * 0.5f, 0f), new Vector3(t, lintel, cellW), DoorColor);
        }

        _ = wall;
    }

    static void BuildWindow(Transform section, CastleBuildMetrics.WallDir wall, Vector3 cellLocal, bool alongX, float cellW, float cellH, float t)
    {
        float frame = 0.12f;
        Color frameCol = new Color(0.55f, 0.5f, 0.42f, 1f);
        if (alongX)
        {
            CastlePrim.Cube("WinL", section, cellLocal + new Vector3(-(cellW * 0.5f - frame * 0.5f), 0f, 0f), new Vector3(frame, cellH, t), frameCol);
            CastlePrim.Cube("WinR", section, cellLocal + new Vector3(cellW * 0.5f - frame * 0.5f, 0f, 0f), new Vector3(frame, cellH, t), frameCol);
            CastlePrim.Cube("WinT", section, cellLocal + new Vector3(0f, cellH * 0.5f - frame * 0.5f, 0f), new Vector3(cellW, frame, t), frameCol);
            CastlePrim.Cube("WinB", section, cellLocal + new Vector3(0f, -(cellH * 0.5f - frame * 0.5f), 0f), new Vector3(cellW, frame, t), frameCol);
        }
        else
        {
            CastlePrim.Cube("WinL", section, cellLocal + new Vector3(0f, 0f, -(cellW * 0.5f - frame * 0.5f)), new Vector3(t, cellH, frame), frameCol);
            CastlePrim.Cube("WinR", section, cellLocal + new Vector3(0f, 0f, cellW * 0.5f - frame * 0.5f), new Vector3(t, cellH, frame), frameCol);
            CastlePrim.Cube("WinT", section, cellLocal + new Vector3(0f, cellH * 0.5f - frame * 0.5f, 0f), new Vector3(t, frame, cellW), frameCol);
            CastlePrim.Cube("WinB", section, cellLocal + new Vector3(0f, -(cellH * 0.5f - frame * 0.5f), 0f), new Vector3(t, frame, cellW), frameCol);
        }

        var glass = CastlePrim.Cube("WinGlass", section, cellLocal, alongX
            ? new Vector3(cellW - frame * 2f, cellH - frame * 2f, t * 0.2f)
            : new Vector3(t * 0.2f, cellH - frame * 2f, cellW - frame * 2f), WindowColor, trigger: true);
        _ = glass;
        _ = wall;
    }

    static void BuildPowers(Transform geo, Transform socks, CastleLayoutData data, bool showSockets)
    {
        if (data.sections.Count == 0)
            return;

        data.GetPlanBounds(out int minX, out int maxX, out int minZ, out int maxZ);
        TryPower(geo, socks, data, maxX, maxZ, CastleBuildMetrics.CornerId.NE, showSockets);
        TryPower(geo, socks, data, maxX, minZ, CastleBuildMetrics.CornerId.SE, showSockets);
        TryPower(geo, socks, data, minX, minZ, CastleBuildMetrics.CornerId.SW, showSockets);
        TryPower(geo, socks, data, minX, maxZ, CastleBuildMetrics.CornerId.NW, showSockets);
    }

    static void TryPower(
        Transform geo,
        Transform socks,
        CastleLayoutData data,
        int x,
        int z,
        CastleBuildMetrics.CornerId corner,
        bool showSockets)
    {
        if (!data.IsOuterPowerCorner(x, z, corner))
            return;

        int groundY = data.HasSection(x, 0, z) ? 0 : 1;
        var sectionOrigin = CastleBuildMetrics.CellOrigin(x, groundY, z);
        var section = new GameObject($"Power_{corner}");
        section.transform.SetParent(geo, false);
        section.transform.localPosition = sectionOrigin;

        int kind = data.GetPower(x, z, (int)corner);
        int floors = data.FloorCount(x, z);
        if (kind == CastleLayoutData.PowerTower)
        {
            float height = floors >= 2 ? CastleBuildMetrics.Tower2FHeight : CastleBuildMetrics.Tower1FHeight;
            Vector3 local = CastleBuildMetrics.TowerLocal(corner);
            CastlePrim.Cube(
                floors >= 2 ? "Tower_2F" : "Tower_1F",
                section.transform,
                local + Vector3.up * (height * 0.5f),
                new Vector3(CastleBuildMetrics.TowerBase, height, CastleBuildMetrics.TowerBase),
                TowerColor);
        }
        else if (kind == CastleLayoutData.PowerHeavy)
        {
            Vector3 local = CastleBuildMetrics.HeavyLocal(corner);
            CastlePrim.Cube(
                "Station_Heavy",
                section.transform,
                local + Vector3.up * 0.9f,
                new Vector3(CastleBuildMetrics.HeavyPad, 1.8f, CastleBuildMetrics.HeavyPad),
                HeavyColor);
        }

        Vector3 sockPos = kind == CastleLayoutData.PowerHeavy
            ? CastleBuildMetrics.HeavyLocal(corner)
            : CastleBuildMetrics.TowerLocal(corner);
        var pad = CastlePrim.Cube(
            $"TowerSocket_{corner}",
            socks,
            Vector3.zero,
            Vector3.one * 2.1f,
            PowerSocketColor,
            trigger: true);
        pad.transform.position = section.transform.TransformPoint(sockPos + Vector3.up * 0.3f);
        var marker = pad.AddComponent<CastleEditorSocket>();
        marker.SocketKind = CastleEditorSocket.Kind.Power;
        marker.X = x;
        marker.Z = z;
        marker.Y = groundY;
        marker.Corner = (int)corner;
        pad.SetActive(showSockets);
        if (showSockets)
        {
            string label = kind == CastleLayoutData.PowerTower
                ? (floors >= 2 ? "TOWER 2F" : "TOWER 1F")
                : kind == CastleLayoutData.PowerHeavy ? "HEAVY" : $"POWER {corner}";
            CastlePrim.Label(pad.transform, Vector3.zero, label, new Color(1f, 0.7f, 0.2f));
        }
    }

    static void SpawnMage(Transform root, CastleLayoutData data, bool playerOwned)
    {
        if (!data.HasSection(0, 0, 0) && data.sections.Count > 0)
        {
            // Mage stays at world origin of castle (starter should be 0,0,0). If not, use first cell.
        }

        Vector3 mageLocal = Vector3.up * 0.05f;
        if (data.HasSection(0, 0, 0))
            mageLocal = CastleBuildMetrics.CellOrigin(0, 0, 0) + Vector3.up * 0.05f;
        else if (data.sections.Count > 0)
        {
            var s = data.sections[0];
            mageLocal = CastleBuildMetrics.CellOrigin(s.x, s.y, s.z) + Vector3.up * 0.05f;
        }

        var mage = CastleMageVisual.Spawn(root, mageLocal, heightScale: 2.8f, attachMageComponent: !playerOwned);
        if (playerOwned)
        {
            if (mage.GetComponent<OwnMageStation>() == null)
                mage.AddComponent<OwnMageStation>();
        }
        else
        {
            var castle = root.GetComponent<EnemyCastle>();
            var mageComp = mage.GetComponent<EnemyCastleMage>();
            if (castle != null && mageComp != null)
            {
                castle.BindMage(mageComp);
                mageComp.Bind(castle, 120f);
            }
        }
    }

    static void EnsureEnemyCastle(Transform root)
    {
        var castle = root.GetComponent<EnemyCastle>();
        if (castle == null)
            castle = root.gameObject.AddComponent<EnemyCastle>();

        var mage = root.GetComponentInChildren<EnemyCastleMage>(true);
        if (mage != null)
        {
            castle.BindMage(mage);
            mage.Bind(castle, 120f);
        }
    }
}
