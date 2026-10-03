using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Occupied carcass cells. Columns and wall bays are unique — neighbors share them.
/// </summary>
public sealed class CarcassCastle : MonoBehaviour
{
    [SerializeField] bool _playerOwned;

    readonly HashSet<Vector3Int> _cells = new HashSet<Vector3Int>();
    readonly HashSet<Vector3Int> _columns = new HashSet<Vector3Int>();
    readonly Dictionary<Vector3Int, GameObject> _floors = new Dictionary<Vector3Int, GameObject>();
    readonly Dictionary<Vector3Int, GameObject> _roofs = new Dictionary<Vector3Int, GameObject>();
    readonly Dictionary<string, CarcassBay> _bays = new Dictionary<string, CarcassBay>();
    readonly Dictionary<Vector2Int, CastleTower> _towers = new Dictionary<Vector2Int, CastleTower>();

    static CarcassCastle _player;

    Transform _columnsRoot;
    Transform _floorsRoot;
    Transform _roofsRoot;
    Transform _baysRoot;
    Transform _merlonsRoot;
    Transform _laddersRoot;
    Transform _stationsRoot;
    Transform _towersRoot;

    public bool IsPlayerOwned => _playerOwned;
    public IReadOnlyCollection<Vector3Int> Cells => _cells;

    public static CarcassCastle FindPlayerOwned()
    {
        if (_player != null && _player._playerOwned)
            return _player;

        var all = FindObjectsByType<CarcassCastle>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i]._playerOwned)
            {
                _player = all[i];
                return _player;
            }
        }

        return null;
    }

    public void Initialize(bool playerOwned)
    {
        _playerOwned = playerOwned;
        _columnsRoot = EnsureChild("Columns");
        _floorsRoot = EnsureChild("Floors");
        _roofsRoot = EnsureChild("Roofs");
        _baysRoot = EnsureChild("Bays");
        _merlonsRoot = EnsureChild("Merlons");
        _laddersRoot = EnsureChild("Ladders");
        _stationsRoot = EnsureChild("Stations");
        if (playerOwned)
            _player = this;
    }

    public Transform FloorsRoot => _floorsRoot;
    public Transform StationsRoot => _stationsRoot;

    public bool HasCell(int x, int y, int z) => _cells.Contains(new Vector3Int(x, y, z));

    public bool CanPlaceSection(int x, int y, int z)
    {
        if (!CarcassMetrics.InPlanCap(x, z))
            return false;
        if (y < 0 || y >= CarcassMetrics.MaxFloors)
            return false;
        if (HasCell(x, y, z))
            return false;
        if (_cells.Count == 0)
            return x == 0 && y == 0 && z == 0;
        if (y > 0 && !HasCell(x, y - 1, z))
            return false;
        if (y == 0)
            return HasOrthoNeighbor(x, 0, z);
        return true;
    }

    public bool TryAddSection(int x, int y, int z, bool fillOuterWalls, CarcassMetrics.WallDir doorDir, int doorSlot)
    {
        if (!CanPlaceSection(x, y, z) && _cells.Count > 0)
            return false;
        if (_cells.Count == 0 && (x != 0 || y != 0 || z != 0))
            return false;

        _cells.Add(new Vector3Int(x, y, z));
        EnsureColumn(x, y, z);
        EnsureColumn(x + 1, y, z);
        EnsureColumn(x, y, z + 1);
        EnsureColumn(x + 1, y, z + 1);
        EnsureFloor(x, y, z);
        RefreshRoof(x, y, z);
        if (y > 0)
        {
            RefreshRoof(x, y - 1, z);
            CarcassLadder.CreatePair(transform, _laddersRoot, x, y - 1, z);
        }

        EnsureBay(x, y, z, CarcassMetrics.WallDir.N, 0);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.N, 1);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.E, 0);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.E, 1);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.S, 0);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.S, 1);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.W, 0);
        EnsureBay(x, y, z, CarcassMetrics.WallDir.W, 1);

        if (fillOuterWalls)
            FillNewWalls(x, y, z, doorDir, doorSlot);

        RefreshMerlons();
        RefreshTowers();
        if (!fillOuterWalls)
            HitSparkVfx.PlayDust(transform.TransformPoint(CarcassMetrics.CellCenterLocal(x, y, z)), Vector3.up, 14);
        return true;
    }

    public bool TrySetBay(CarcassBay bay, CastleModuleKind kind, bool playFx = true)
    {
        if (bay == null)
            return false;
        if (kind != CastleModuleKind.Wall && kind != CastleModuleKind.Window && kind != CastleModuleKind.Door)
            return false;

        bay.ClearModule();
        GameObject module = CarcassKit.CreateWallModule(
            kind,
            bay.transform,
            Vector3.zero,
            Quaternion.identity);
        bay.BindModule(module, kind);
        if (playFx)
            HitSparkVfx.PlayDust(bay.transform.position + Vector3.up * 0.4f, bay.transform.forward, 10);
        return true;
    }

    public CarcassBay FindBestBay(Ray ray, float maxRange)
    {
        CarcassBay best = null;
        float bestScore = float.MaxValue;
        foreach (var pair in _bays)
        {
            var bay = pair.Value;
            if (bay == null)
                continue;
            Vector3 p = bay.transform.position;
            Vector3 to = p - ray.origin;
            if (to.sqrMagnitude > maxRange * maxRange)
                continue;
            float dist = to.magnitude;
            float lateral = Vector3.Cross(ray.direction, p - ray.origin).magnitude;
            float score = dist + lateral * 1.6f;
            if (score < bestScore)
            {
                bestScore = score;
                best = bay;
            }
        }

        return best;
    }

    public bool FindBestSectionCell(Ray ray, float maxRange, out int x, out int y, out int z)
    {
        x = y = z = 0;
        float best = float.MaxValue;
        bool found = false;
        for (int cx = -CarcassMetrics.MaxPlanRadius; cx <= CarcassMetrics.MaxPlanRadius; cx++)
        {
            for (int cz = -CarcassMetrics.MaxPlanRadius; cz <= CarcassMetrics.MaxPlanRadius; cz++)
            {
                for (int cy = 0; cy < CarcassMetrics.MaxFloors; cy++)
                {
                    if (!CanPlaceSection(cx, cy, cz))
                        continue;
                    Vector3 p = transform.TransformPoint(CarcassMetrics.CellCenterLocal(cx, cy, cz));
                    float dist = Vector3.Distance(ray.origin, p);
                    if (dist > maxRange)
                        continue;
                    float lateral = Vector3.Cross(ray.direction, p - ray.origin).magnitude;
                    float score = dist + lateral * 1.4f;
                    if (score < best)
                    {
                        best = score;
                        x = cx;
                        y = cy;
                        z = cz;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    public Vector3 CellWorldCenter(int x, int y, int z)
    {
        return transform.TransformPoint(CarcassMetrics.CellCenterLocal(x, y, z));
    }

    /// <summary>Верхний этаж колонки (x, z) или -1, если там нет секций.</summary>
    public int TopFloorAt(int x, int z)
    {
        for (int y = CarcassMetrics.MaxFloors - 1; y >= 0; y--)
        {
            if (HasCell(x, y, z))
                return y;
        }

        return -1;
    }

    public bool CanPlaceTower(TowerCorner corner)
    {
        return TopFloorAt(corner.CellX, corner.CellZ) >= 0
               && !_towers.ContainsKey(corner.Vertex)
               && IsOuterCorner(corner);
    }

    /// <summary>
    /// Угол внешний, пока вокруг его вершины стоит только своя клетка. Соседи проверяются на нулевом этаже:
    /// верхние секции опираются на нижние, так что угол не «зарастёт» позже на одном лишь верхнем этаже.
    /// </summary>
    bool IsOuterCorner(TowerCorner corner)
    {
        Vector2Int v = corner.Vertex;
        for (int cx = v.x - 1; cx <= v.x; cx++)
        {
            for (int cz = v.y - 1; cz <= v.y; cz++)
            {
                if ((cx != corner.CellX || cz != corner.CellZ) && HasCell(cx, 0, cz))
                    return false;
            }
        }

        return true;
    }

    public bool TryAddTower(TowerCorner corner)
    {
        if (!CanPlaceTower(corner))
            return false;

        if (_towersRoot == null)
            _towersRoot = EnsureChild("Towers");
        int floorY = TopFloorAt(corner.CellX, corner.CellZ);
        _towers[corner.Vertex] = CastleTower.Create(this, _towersRoot, corner, floorY);
        return true;
    }

    public void RemoveTower(CastleTower tower, bool destroyedByDamage = false)
    {
        if (tower == null)
            return;
        _towers.Remove(tower.Corner.Vertex);
        tower.Dismantle(destroyedByDamage);
    }

    /// <summary>
    /// Снимает башни, чей угол после пристройки секции оказался внутри замка. Возвращает их число,
    /// чтобы вызывающий вернул ресурсы (как и со стенами в <see cref="ClearSharedWalls"/>).
    /// </summary>
    public int RemoveSwallowedTowers()
    {
        var swallowed = new List<CastleTower>();
        foreach (var tower in _towers.Values)
        {
            if (!IsOuterCorner(tower.Corner))
                swallowed.Add(tower);
        }

        for (int i = 0; i < swallowed.Count; i++)
            RemoveTower(swallowed[i]);
        return swallowed.Count;
    }

    /// <summary>Башни переезжают на крышу нового верхнего этажа своей колонки.</summary>
    void RefreshTowers()
    {
        foreach (var tower in _towers.Values)
            tower.MoveToFloor(TopFloorAt(tower.Corner.CellX, tower.Corner.CellZ));
    }

    public bool FindBestTowerCorner(Ray ray, float maxRange, out TowerCorner best)
    {
        best = default;
        float bestScore = float.MaxValue;
        bool found = false;
        foreach (var cell in _cells)
        {
            // Угол принадлежит колонке целиком: перебираем каждую один раз, по верхнему этажу.
            if (TopFloorAt(cell.x, cell.z) != cell.y)
                continue;
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dz = 0; dz <= 1; dz++)
                {
                    var corner = new TowerCorner(cell.x, cell.z, dx, dz);
                    if (!CanPlaceTower(corner))
                        continue;
                    Vector3 p = TowerWorldBase(corner);
                    Vector3 to = p - ray.origin;
                    if (to.sqrMagnitude > maxRange * maxRange)
                        continue;
                    float score = to.magnitude + Vector3.Cross(ray.direction, to).magnitude * 1.4f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = corner;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    public Vector3 TowerWorldBase(TowerCorner corner)
    {
        int floorY = TopFloorAt(corner.CellX, corner.CellZ);
        return transform.TransformPoint(CarcassMetrics.TowerBaseLocal(corner.CellX, corner.CellZ, corner.DX, corner.DZ, floorY));
    }

    bool HasOrthoNeighbor(int x, int y, int z)
    {
        return HasCell(x + 1, y, z)
               || HasCell(x - 1, y, z)
               || HasCell(x, y, z + 1)
               || HasCell(x, y, z - 1);
    }

    void EnsureColumn(int vx, int floorY, int vz)
    {
        var key = new Vector3Int(vx, floorY, vz);
        if (!_columns.Add(key))
            return;
        CarcassKit.CreateCorner(_columnsRoot, CarcassMetrics.ColumnLocal(vx, floorY, vz));
    }

    void EnsureFloor(int x, int y, int z)
    {
        var key = new Vector3Int(x, y, z);
        if (_floors.ContainsKey(key))
            return;
        GameObject floor = CarcassKit.CreateFloor(_floorsRoot, CarcassMetrics.FloorLocal(x, y, z));
        _floors[key] = floor;
    }

    void RefreshRoof(int x, int y, int z)
    {
        var key = new Vector3Int(x, y, z);
        bool want = HasCell(x, y, z) && !HasCell(x, y + 1, z);
        if (want && !_roofs.ContainsKey(key))
        {
            _roofs[key] = CarcassKit.CreateRoof(_roofsRoot, CarcassMetrics.RoofLocal(x, y, z));
            return;
        }

        if (!want && _roofs.TryGetValue(key, out GameObject roof))
        {
            Destroy(roof);
            _roofs.Remove(key);
        }
    }

    void RefreshMerlons()
    {
        if (_merlonsRoot == null)
            _merlonsRoot = EnsureChild("Merlons");

        for (int i = _merlonsRoot.childCount - 1; i >= 0; i--)
            Destroy(_merlonsRoot.GetChild(i).gameObject);

        foreach (var cell in _cells)
        {
            if (HasCell(cell.x, cell.y + 1, cell.z))
                continue;

            foreach (CarcassMetrics.WallDir dir in System.Enum.GetValues(typeof(CarcassMetrics.WallDir)))
            {
                Vector2Int d = CarcassMetrics.DirToDelta(dir);
                if (HasCell(cell.x + d.x, cell.y, cell.z + d.y))
                    continue;

                for (int slot = 0; slot < CarcassMetrics.BaysPerEdge; slot++)
                {
                    Vector3 local = CarcassMetrics.MerlonLocal(cell.x, cell.y, cell.z, dir, slot);
                    CarcassKit.CreateMerlon(_merlonsRoot, local, CarcassMetrics.WallRotation(dir));
                }
            }
        }
    }

    void OnDestroy()
    {
        if (_player == this)
            _player = null;
    }

    void EnsureBay(int x, int y, int z, CarcassMetrics.WallDir dir, int slot)
    {
        string key = BayKey(x, y, z, dir, slot);
        if (_bays.ContainsKey(key))
            return;

        var go = new GameObject();
        go.transform.SetParent(_baysRoot, false);
        go.transform.localPosition = CarcassMetrics.BayLocal(x, y, z, dir, slot);
        go.transform.localRotation = CarcassMetrics.WallRotation(dir);
        var bay = go.AddComponent<CarcassBay>();
        bay.Setup(x, y, z, dir, slot);
        _bays[key] = bay;
    }

    /// <summary>
    /// Снимает модули со стен, которые после пристройки (x,y,z) оказались внутри замка — между
    /// новой и соседней секцией. Возвращает виды снятых модулей, чтобы вызывающий вернул ресурсы.
    /// </summary>
    public void ClearSharedWalls(int x, int y, int z, List<CastleModuleKind> removed)
    {
        foreach (CarcassMetrics.WallDir dir in System.Enum.GetValues(typeof(CarcassMetrics.WallDir)))
        {
            Vector2Int d = CarcassMetrics.DirToDelta(dir);
            if (!HasCell(x + d.x, y, z + d.y))
                continue;
            for (int slot = 0; slot < CarcassMetrics.BaysPerEdge; slot++)
            {
                if (!_bays.TryGetValue(BayKey(x, y, z, dir, slot), out CarcassBay bay) || bay.IsEmpty)
                    continue;
                removed.Add(bay.Occupant);
                bay.ClearModule();
            }
        }
    }

    void FillNewWalls(int x, int y, int z, CarcassMetrics.WallDir doorDir, int doorSlot)
    {
        foreach (CarcassMetrics.WallDir dir in System.Enum.GetValues(typeof(CarcassMetrics.WallDir)))
        {
            for (int slot = 0; slot < CarcassMetrics.BaysPerEdge; slot++)
            {
                if (!_bays.TryGetValue(BayKey(x, y, z, dir, slot), out CarcassBay bay))
                    continue;
                if (!bay.IsEmpty)
                    continue;
                CastleModuleKind kind = dir == doorDir && slot == doorSlot
                    ? CastleModuleKind.Door
                    : CastleModuleKind.Wall;
                TrySetBay(bay, kind, playFx: false);
            }
        }
    }

    static string BayKey(int x, int y, int z, CarcassMetrics.WallDir dir, int slot)
    {
        // Shared edge: the same physical wall for both cells.
        int ax = x;
        int az = z;
        int bx = x;
        int bz = z;
        bool alongX = dir == CarcassMetrics.WallDir.N || dir == CarcassMetrics.WallDir.S;
        if (dir == CarcassMetrics.WallDir.N)
        {
            az = z + 1;
            bz = z + 1;
            bx = x + 1;
        }
        else if (dir == CarcassMetrics.WallDir.S)
        {
            bz = z;
            bx = x + 1;
        }
        else if (dir == CarcassMetrics.WallDir.E)
        {
            ax = x + 1;
            bx = x + 1;
            bz = z + 1;
        }
        else
        {
            bx = x;
            bz = z + 1;
        }

        int minx = Mathf.Min(ax, bx);
        int minz = Mathf.Min(az, bz);
        int maxx = Mathf.Max(ax, bx);
        int maxz = Mathf.Max(az, bz);
        return $"{minx}_{minz}_{maxx}_{maxz}_{y}_{slot}_{alongX}";
    }

    Transform EnsureChild(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
            return existing;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }
}
