using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Художественная стена (арт-меш от 3D) + воксельная сетка урона.
/// Выглядит как модель аниматора; ломается нашей логикой (HP клетки → обломок → добыча).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CastleWall : MonoBehaviour
{
    const string ArtResource = "Castle/ArtCastleWall";
    const string DebrisResource = "Loot/StoneLoot";

    [SerializeField] CastleWallConfig _config;
    [SerializeField] Mesh _artMeshOverride;

    bool[,,] _solid;
    bool[,,] _destroyed;
    int[,,] _cellHp;
    bool[] _supportedFlat;
    int _sizeX;
    int _sizeY;
    int _sizeZ;
    Vector3 _cellSize;
    Vector3 _gridOrigin; // local space, center of cell 0,0,0
    MeshCollider _collision;

    MeshFilter _meshFilter;
    MeshRenderer _meshRenderer;
    Material _material;
    Texture3D _damageMask;
    Color32[] _damagePixels;
    Mesh _debrisMesh;
    Vector3 _debrisMeshSize = Vector3.one;

    readonly List<Vector3> _removedCenters = new(64);
    readonly List<Vector3> _colVerts = new(4096);
    readonly List<int> _colTris = new(8192);
    readonly Queue<Vector3Int> _floodQueue = new();
    readonly Stack<DebrisPiece> _debrisPool = new();
    Mesh _collisionMesh;

    static readonly Vector3Int[] Neighbors =
    {
        new(1, 0, 0), new(-1, 0, 0),
        new(0, 1, 0), new(0, -1, 0),
        new(0, 0, 1), new(0, 0, -1),
    };

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        var legacyBox = GetComponent<BoxCollider>();
        if (legacyBox != null)
            Destroy(legacyBox);

        if (_config == null)
        {
            Debug.LogError("CastleWall: не назначен CastleWallConfig.", this);
            enabled = false;
            return;
        }

        EnsureArtMesh();
        if (_meshFilter.sharedMesh == null || !enabled)
            return;

        SetupMaterial();
        VoxelizeFromArtMesh();
        if (_solid == null)
            return;

        BuildDamageMask();
        PushShaderGrid();
        SetupCollisionCollider();
        RebuildCollisionMesh();
        CacheDebrisMesh();
        WarmDebrisPool(Mathf.Max(16, _config.MaxDebrisPerBlast * 2));
    }

    void EnsureArtMesh()
    {
        Mesh mesh = _artMeshOverride;
        if (mesh == null && _meshFilter.sharedMesh != null && _meshFilter.sharedMesh.vertexCount > 0)
            mesh = _meshFilter.sharedMesh;

        if (mesh == null)
        {
            GameObject prefab = Resources.Load<GameObject>(ArtResource);
            if (prefab != null)
            {
                MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                    mesh = mf.sharedMesh;
            }
        }

        if (mesh == null)
        {
            Debug.LogError("CastleWall: нет арт-меша. Положи FBX в Resources/Castle/ArtCastleWall.", this);
            enabled = false;
            return;
        }

        // Инстанс, чтобы починить нормали без порчи ассета
        Mesh instance = Instantiate(mesh);
        instance.name = mesh.name + "_Runtime";
        instance.RecalculateNormals();
        instance.RecalculateBounds();
        _meshFilter.sharedMesh = instance;
    }

    void SetupMaterial()
    {
        Shader shader = Shader.Find("Catapulto/DestructibleArtWall");
        if (shader == null)
        {
            Debug.LogError("CastleWall: не найден шейдер Catapulto/DestructibleArtWall.", this);
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        _material = new Material(shader);
        // Без текстур и normal map — плоский цвет
        _material.SetColor("_BaseColor", _config.BlockColor);
        _meshRenderer.sharedMaterial = _material;
    }

    void VoxelizeFromArtMesh()
    {
        Mesh art = _meshFilter.sharedMesh;
        Bounds b = art.bounds;
        float cs = Mathf.Max(0.2f, _config.VoxelCellSize);
        _cellSize = new Vector3(cs, cs, cs);

        _sizeX = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cs));
        _sizeY = Mathf.Max(1, Mathf.CeilToInt(b.size.y / cs));
        _sizeZ = Mathf.Max(1, Mathf.CeilToInt(b.size.z / cs));

        _gridOrigin = b.min + _cellSize * 0.5f;

        _solid = new bool[_sizeX, _sizeY, _sizeZ];
        _destroyed = new bool[_sizeX, _sizeY, _sizeZ];
        _cellHp = new int[_sizeX, _sizeY, _sizeZ];
        _supportedFlat = new bool[_sizeX * _sizeY * _sizeZ];

        var probe = gameObject.AddComponent<MeshCollider>();
        probe.sharedMesh = art;
        probe.convex = false;

        int maxHp = _config.CellMaxHp;
        int solidCount = 0;
        float nearDist = cs * 0.65f;
        float nearDistSq = nearDist * nearDist;
        Vector3 halfExtents = _cellSize * 0.5f;

        for (int x = 0; x < _sizeX; x++)
        for (int y = 0; y < _sizeY; y++)
        for (int z = 0; z < _sizeZ; z++)
        {
            Vector3 local = CellCenterLocal(x, y, z);
            Vector3 world = transform.TransformPoint(local);

            // Thin art walls: клетка solid, если центр близко к поверхности меша
            // (тест "внутри объёма" для тонких стен почти всегда false)
            Vector3 closest = probe.ClosestPoint(world);
            bool nearSurface = (closest - world).sqrMagnitude <= nearDistSq;

            bool overlap = false;
            Collider[] hits = Physics.OverlapBox(world, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == probe)
                {
                    overlap = true;
                    break;
                }
            }

            bool hit = nearSurface || overlap;
            _solid[x, y, z] = hit;
            _destroyed[x, y, z] = false; // клипаем только реально сломанные
            _cellHp[x, y, z] = hit ? maxHp : 0;
            if (hit) solidCount++;
        }

        DestroyImmediate(probe);
        Debug.Log($"CastleWall voxelize: {_sizeX}x{_sizeY}x{_sizeZ}, solid={solidCount}", this);

        if (solidCount == 0)
            Debug.LogError("CastleWall: вокселизация дала 0 solid — разрушение не будет работать.", this);
    }

    void BuildDamageMask()
    {
        _damageMask = new Texture3D(_sizeX, _sizeY, _sizeZ, TextureFormat.R8, false);
        _damageMask.wrapMode = TextureWrapMode.Clamp;
        _damageMask.filterMode = FilterMode.Point;
        _damagePixels = new Color32[_sizeX * _sizeY * _sizeZ];
        RefreshDamageMask();
        _material.SetTexture("_DamageMask", _damageMask);
    }

    void RefreshDamageMask()
    {
        int i = 0;
        for (int z = 0; z < _sizeZ; z++)
        for (int y = 0; y < _sizeY; y++)
        for (int x = 0; x < _sizeX; x++)
        {
            byte v = (byte)(_destroyed[x, y, z] ? 255 : 0);
            _damagePixels[i++] = new Color32(v, 0, 0, 255);
        }

        _damageMask.SetPixels32(_damagePixels);
        _damageMask.Apply(false);
    }

    void PushShaderGrid()
    {
        _material.SetVector("_GridOrigin", _gridOrigin);
        _material.SetVector("_CellSize", _cellSize);
        _material.SetVector("_GridDims", new Vector4(_sizeX, _sizeY, _sizeZ, 0f));
    }

    void SetupCollisionCollider()
    {
        _collision = GetComponent<MeshCollider>();
        if (_collision == null)
            _collision = gameObject.AddComponent<MeshCollider>();
        _collision.convex = false;
        _collisionMesh = new Mesh { name = "CastleWallCollision" };
        _collisionMesh.MarkDynamic();
        _collisionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    }

    public void ApplyBlast(Vector3 worldPoint, float radius, float explosionForce, Vector3 incomingVelocity)
    {
        if (_solid == null)
            return;

        _removedCenters.Clear();
        float radiusSq = radius * radius;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        for (int x = 0; x < _sizeX; x++)
        for (int y = 0; y < _sizeY; y++)
        for (int z = 0; z < _sizeZ; z++)
        {
            if (!_solid[x, y, z] || _destroyed[x, y, z])
                continue;

            Vector3 localCenter = CellCenterLocal(x, y, z);
            if ((localCenter - localPoint).sqrMagnitude > radiusSq)
                continue;

            BreakCell(x, y, z, transform.TransformPoint(localCenter));
        }

        CollapseUnsupported();
        AfterDestruction(worldPoint, explosionForce * _config.DebrisForceScale, incomingVelocity * 0.15f, soft: false);
    }

    public bool ApplyPickaxeHit(Vector3 worldPoint, Vector3 hitNormal, int damage, out Vector3 effectPosition)
    {
        effectPosition = worldPoint;
        if (_solid == null || damage <= 0)
            return false;

        Vector3 sample = worldPoint;
        if (hitNormal.sqrMagnitude > 0.001f)
            sample -= hitNormal.normalized * (_cellSize.x * 0.25f);

        if (!TryFindNearestSolid(sample, _config.MineRadius, out int bx, out int by, out int bz, out Vector3 center))
            return false;

        effectPosition = worldPoint;
        _cellHp[bx, by, bz] -= damage;
        if (_cellHp[bx, by, bz] > 0)
            return true;

        _removedCenters.Clear();
        BreakCell(bx, by, bz, center);
        CollapseUnsupported();
        AfterDestruction(center, 8f, -hitNormal * 0.8f, soft: true);
        return true;
    }

    void BreakCell(int x, int y, int z, Vector3 worldCenter)
    {
        if (_destroyed[x, y, z])
            return;
        _destroyed[x, y, z] = true;
        _solid[x, y, z] = false;
        _cellHp[x, y, z] = 0;
        _removedCenters.Add(worldCenter);
    }

    void AfterDestruction(Vector3 blastPoint, float force, Vector3 incoming, bool soft)
    {
        RefreshDamageMask();
        SpawnDebris(blastPoint, force, incoming, soft);
        RebuildCollisionMesh();
    }

    bool TryFindNearestSolid(Vector3 worldPoint, float radius, out int ox, out int oy, out int oz, out Vector3 worldCenter)
    {
        ox = oy = oz = 0;
        worldCenter = worldPoint;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        float best = radius * radius;
        bool found = false;

        for (int x = 0; x < _sizeX; x++)
        for (int y = 0; y < _sizeY; y++)
        for (int z = 0; z < _sizeZ; z++)
        {
            if (!_solid[x, y, z] || _destroyed[x, y, z])
                continue;

            Vector3 localCenter = CellCenterLocal(x, y, z);
            float d = (localCenter - localPoint).sqrMagnitude;
            if (d > best)
                continue;

            best = d;
            ox = x; oy = y; oz = z;
            worldCenter = transform.TransformPoint(localCenter);
            found = true;
        }

        return found;
    }

    Vector3 CellCenterLocal(int x, int y, int z)
    {
        return _gridOrigin + new Vector3(x * _cellSize.x, y * _cellSize.y, z * _cellSize.z);
    }

    void CollapseUnsupported()
    {
        System.Array.Clear(_supportedFlat, 0, _supportedFlat.Length);
        _floodQueue.Clear();

        for (int x = 0; x < _sizeX; x++)
        for (int z = 0; z < _sizeZ; z++)
        {
            if (!_solid[x, 0, z] || _destroyed[x, 0, z])
                continue;
            SetSupported(x, 0, z, true);
            _floodQueue.Enqueue(new Vector3Int(x, 0, z));
        }

        while (_floodQueue.Count > 0)
        {
            Vector3Int c = _floodQueue.Dequeue();
            for (int i = 0; i < Neighbors.Length; i++)
            {
                Vector3Int n = c + Neighbors[i];
                if (!InBounds(n.x, n.y, n.z) || !_solid[n.x, n.y, n.z] || _destroyed[n.x, n.y, n.z] || IsSupported(n.x, n.y, n.z))
                    continue;
                SetSupported(n.x, n.y, n.z, true);
                _floodQueue.Enqueue(n);
            }
        }

        for (int x = 0; x < _sizeX; x++)
        for (int y = 0; y < _sizeY; y++)
        for (int z = 0; z < _sizeZ; z++)
        {
            if (!_solid[x, y, z] || _destroyed[x, y, z] || IsSupported(x, y, z))
                continue;
            BreakCell(x, y, z, transform.TransformPoint(CellCenterLocal(x, y, z)));
        }
    }

    void SpawnDebris(Vector3 blastPoint, float explosionForce, Vector3 incomingVelocity, bool soft)
    {
        int count = Mathf.Min(_removedCenters.Count, soft ? _removedCenters.Count : _config.MaxDebrisPerBlast);
        if (count <= 0)
            return;

        float step = _removedCenters.Count > count ? (float)_removedCenters.Count / count : 1f;
        float visual = Mathf.Min(_cellSize.x, _cellSize.y) * 0.85f;
        Vector3 scale = Vector3.one * (visual / Mathf.Max(0.001f, _debrisMeshSize.x));

        for (int i = 0; i < count; i++)
        {
            int index = Mathf.Min(_removedCenters.Count - 1, Mathf.FloorToInt(i * step));
            DebrisPiece piece = RentDebris();
            piece.Transform.SetPositionAndRotation(_removedCenters[index], Random.rotation);
            piece.Transform.localScale = scale;
            piece.GameObject.SetActive(true);
            piece.Body.mass = _config.DebrisMass;
            piece.Body.linearVelocity = Vector3.zero;
            piece.Body.angularVelocity = Vector3.zero;
            if (piece.Mineable != null)
                piece.Mineable.ResetHp(_config.DebrisMaxHp);

            if (soft)
            {
                piece.Body.AddForce(incomingVelocity + Random.insideUnitSphere * 0.35f, ForceMode.VelocityChange);
                piece.Body.AddTorque(Random.insideUnitSphere * 0.4f, ForceMode.Impulse);
            }
            else
            {
                piece.Body.AddForce(incomingVelocity, ForceMode.VelocityChange);
                piece.Body.AddExplosionForce(explosionForce, blastPoint, Mathf.Max(_cellSize.x * 4f, 2f), 0.05f, ForceMode.Impulse);
            }
        }
    }

    void RebuildCollisionMesh()
    {
        _colVerts.Clear();
        _colTris.Clear();
        Vector3 half = _cellSize * 0.5f;

        for (int x = 0; x < _sizeX; x++)
        for (int y = 0; y < _sizeY; y++)
        for (int z = 0; z < _sizeZ; z++)
        {
            if (!_solid[x, y, z] || _destroyed[x, y, z])
                continue;
            AddCollisionBox(CellCenterLocal(x, y, z), half);
        }

        _collisionMesh.Clear();
        if (_colVerts.Count > 0)
        {
            _collisionMesh.SetVertices(_colVerts);
            _collisionMesh.SetTriangles(_colTris, 0, false);
            _collisionMesh.RecalculateBounds();
        }

        _collision.sharedMesh = null;
        if (_colVerts.Count == 0)
        {
            _collision.enabled = false;
            return;
        }

        _collision.sharedMesh = _collisionMesh;
        _collision.enabled = true;
    }

    void AddCollisionBox(Vector3 center, Vector3 half)
    {
        Vector3[] nrm =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        for (int f = 0; f < 6; f++)
        {
            Vector3 normal = nrm[f];
            Vector3 right = Vector3.Cross(normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, normal).normalized;
            float hr = Mathf.Abs(Vector3.Dot(half, right));
            float hu = Mathf.Abs(Vector3.Dot(half, up));
            Vector3 fc = center + Vector3.Scale(normal, half);
            int i = _colVerts.Count;
            _colVerts.Add(fc + (-right * hr - up * hu));
            _colVerts.Add(fc + (-right * hr + up * hu));
            _colVerts.Add(fc + (right * hr + up * hu));
            _colVerts.Add(fc + (right * hr - up * hu));
            _colTris.Add(i); _colTris.Add(i + 1); _colTris.Add(i + 2);
            _colTris.Add(i); _colTris.Add(i + 2); _colTris.Add(i + 3);
        }
    }

    void CacheDebrisMesh()
    {
        GameObject model = Resources.Load<GameObject>(DebrisResource);
        if (model != null)
        {
            MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                _debrisMesh = mf.sharedMesh;
                _debrisMeshSize = _debrisMesh.bounds.size;
                if (_debrisMeshSize.x < 0.001f) _debrisMeshSize = Vector3.one;
                return;
            }
        }

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _debrisMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        _debrisMeshSize = Vector3.one;
        Destroy(temp);
    }

    void WarmDebrisPool(int count)
    {
        for (int i = 0; i < count; i++)
            _debrisPool.Push(CreateDebrisPiece());
    }

    DebrisPiece RentDebris() => _debrisPool.Count > 0 ? _debrisPool.Pop() : CreateDebrisPiece();

    DebrisPiece CreateDebrisPiece()
    {
        var go = new GameObject("WallRubble");
        go.SetActive(false);
        go.AddComponent<MeshFilter>().sharedMesh = _debrisMesh;
        var rend = go.AddComponent<MeshRenderer>();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var debrisMat = lit != null ? new Material(lit) : new Material(Shader.Find("Standard"));
        debrisMat.SetColor("_BaseColor", _config.BlockColor);
        rend.sharedMaterial = debrisMat;
        go.AddComponent<BoxCollider>();
        var body = go.AddComponent<Rigidbody>();
        body.mass = _config.DebrisMass;
        body.linearDamping = 1.2f;
        body.angularDamping = 2.5f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var mineable = go.AddComponent<MineableDebris>();
        mineable.ResetHp(_config.DebrisMaxHp);
        return new DebrisPiece(go, go.transform, body, mineable);
    }

    bool InBounds(int x, int y, int z) => x >= 0 && x < _sizeX && y >= 0 && y < _sizeY && z >= 0 && z < _sizeZ;
    int FlatIndex(int x, int y, int z) => x + _sizeX * (y + _sizeY * z);
    bool IsSupported(int x, int y, int z) => _supportedFlat[FlatIndex(x, y, z)];
    void SetSupported(int x, int y, int z, bool value) => _supportedFlat[FlatIndex(x, y, z)] = value;

    sealed class DebrisPiece
    {
        public readonly GameObject GameObject;
        public readonly Transform Transform;
        public readonly Rigidbody Body;
        public readonly MineableDebris Mineable;

        public DebrisPiece(GameObject gameObject, Transform transform, Rigidbody body, MineableDebris mineable)
        {
            GameObject = gameObject;
            Transform = transform;
            Body = body;
            Mineable = mineable;
        }
    }
}
