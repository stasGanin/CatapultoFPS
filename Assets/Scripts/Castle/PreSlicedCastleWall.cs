using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a breakable wall from a clean destroyed FBX (pre-sliced meshes + materials).
/// Leaf mesh pieces start kinematic; hits detach them so they fall and can be mined.
/// </summary>
public sealed class PreSlicedCastleWall : MonoBehaviour
{
    const string DestroyedMeshResource = "Castle/DestroyedParts";
    const string MaterialResource = "Castle/CommonPartMat";

    [Tooltip("Clean FBX root (Resources/Castle/DestroyedParts). Leave empty to load from Resources.")]
    [SerializeField] GameObject _destroyedMesh;

    [SerializeField] float _targetWorldHeight = 3.5f;
    [SerializeField] Vector3 _spawnOffset;
    [SerializeField] int _detachHp = 5;
    [SerializeField] int _mineHp = 10;
    [SerializeField] float _detachImpulse = 0.55f;
    [SerializeField] float _pickaxeBreakRadius = 0.4f;
    [SerializeField] int _maxChunksPerPickHit = 1;
    [SerializeField] int _maxChunksPerBlast = 5;
    [SerializeField] bool _collapseUnsupported = true;
    [Tooltip("How close chunk bounds must be to count as supporting neighbors.")]
    [SerializeField] float _supportGap = 0.45f;
    [Tooltip("Chunk bottom within this of wall base = grounded.")]
    [SerializeField] float _groundSlack = 0.35f;
    [SerializeField] bool _disableLegacyCastleWall = true;

    readonly List<CastleWallChunk> _chunks = new List<CastleWallChunk>(128);
    List<int>[] _supportLinks;
    float _groundY;
    bool _collapsing;
    Transform _root;

    public IReadOnlyList<CastleWallChunk> Chunks => _chunks;

    void Awake()
    {
        DisableLegacy();

        if (_destroyedMesh == null)
            _destroyedMesh = Resources.Load<GameObject>(DestroyedMeshResource);

        if (_destroyedMesh == null)
        {
            Debug.LogError($"PreSlicedCastleWall: missing mesh '{DestroyedMeshResource}'.", this);
            return;
        }

        Material mat = Resources.Load<Material>(MaterialResource);

        _root = new GameObject("PreSlicedCastleWall").transform;
        _root.SetPositionAndRotation(transform.position + _spawnOffset, transform.rotation);

        GameObject instance = Instantiate(_destroyedMesh, _root);
        instance.name = "DestroyedParts";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        StripForeignComponents(instance);
        BuildChunksFromLeaves(instance, mat);
        FitScaleToTargetHeight();
        CacheChunks();
        BuildSupportGraph();

        Debug.Log($"PreSlicedCastleWall: {_chunks.Count} clean chunks, scale={_root.localScale.y:0.###}", this);
    }

    void DisableLegacy()
    {
        if (!_disableLegacyCastleWall)
            return;

        var legacy = GetComponent<CastleWall>();
        if (legacy == null)
            legacy = FindFirstObjectByType<CastleWall>();
        if (legacy == null)
            return;

        legacy.enabled = false;
        var mr = legacy.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.enabled = false;
        var mf = legacy.GetComponent<MeshFilter>();
        if (mf != null)
            mf.sharedMesh = null;
        foreach (var col in legacy.GetComponents<Collider>())
            col.enabled = false;

        var bootstrap = GetComponent<CastleSegmentWallBootstrap>();
        if (bootstrap != null)
            bootstrap.enabled = false;
    }

    static void StripForeignComponents(GameObject root)
    {
        // Remove anything left over if a battle prefab was assigned by mistake
        foreach (var c in root.GetComponentsInChildren<Catapulto.Battle.Castle.CastleSegmentView>(true))
            DestroyImmediate(c);
        foreach (var c in root.GetComponentsInChildren<Catapulto.Battle.Castle.CastleDebrisImpactDamage>(true))
            DestroyImmediate(c);
        foreach (var c in root.GetComponentsInChildren<Catapulto.Battle.Castle.CastleSegmentRigidbodyImpact>(true))
            DestroyImmediate(c);

        // FBX often imports Animator / empty Rigidbodies — strip so we own physics
        foreach (var a in root.GetComponentsInChildren<Animator>(true))
            DestroyImmediate(a);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(rb);
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(col);
    }

    void BuildChunksFromLeaves(GameObject instance, Material mat)
    {
        var filters = instance.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;

            // Only leaf meshes — avoids "piece inside piece" double colliders
            if (HasMeshFilterInChildren(mf.transform))
                continue;

            GameObject go = mf.gameObject;

            if (mat != null)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = mat;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.mass = 5f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.constraints = RigidbodyConstraints.FreezeAll;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;

            var chunk = go.AddComponent<CastleWallChunk>();
            chunk.Configure(_detachHp, _mineHp, _detachImpulse, this);
            chunk.HoldInPlace();
        }

        // Overlapping convex hulls jitter — ignore collisions between attached pieces
        IgnoreCollisionsBetweenAttachedChunks(true);
    }

    void IgnoreCollisionsBetweenAttachedChunks(bool ignore)
    {
        CacheChunks();
        for (int i = 0; i < _chunks.Count; i++)
        {
            var a = _chunks[i];
            if (a == null || a.ChunkCollider == null)
                continue;
            for (int j = i + 1; j < _chunks.Count; j++)
            {
                var b = _chunks[j];
                if (b == null || b.ChunkCollider == null)
                    continue;
                Physics.IgnoreCollision(a.ChunkCollider, b.ChunkCollider, ignore);
            }
        }
    }

    /// <summary>
    /// Detached chunk keeps ignoring still-attached siblings (shared faces / hull overlap),
    /// but collides with other free debris and the world.
    /// </summary>
    public void OnChunkDetached(CastleWallChunk detached)
    {
        if (detached == null || detached.ChunkCollider == null)
            return;

        for (int i = 0; i < _chunks.Count; i++)
        {
            var other = _chunks[i];
            if (other == null || other == detached || other.ChunkCollider == null)
                continue;

            if (!other.IsDetached)
                Physics.IgnoreCollision(detached.ChunkCollider, other.ChunkCollider, true);
            else
                Physics.IgnoreCollision(detached.ChunkCollider, other.ChunkCollider, false);
        }

        CollapseUnsupported();
    }

    void BuildSupportGraph()
    {
        int n = _chunks.Count;
        _supportLinks = new List<int>[n];
        for (int i = 0; i < n; i++)
            _supportLinks[i] = new List<int>(8);

        if (n == 0)
            return;

        var bounds = new Bounds[n];
        _groundY = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            var col = _chunks[i] != null ? _chunks[i].ChunkCollider : null;
            if (col == null)
            {
                bounds[i] = new Bounds(_chunks[i].transform.position, Vector3.one * 0.1f);
            }
            else
            {
                bounds[i] = col.bounds;
            }
            _groundY = Mathf.Min(_groundY, bounds[i].min.y);
        }

        float gap = Mathf.Max(0.2f, _supportGap);
        // Expand gap using median chunk size so scaled meshes still link
        float avgExtent = 0f;
        for (int i = 0; i < n; i++)
            avgExtent += (bounds[i].extents.x + bounds[i].extents.y + bounds[i].extents.z) / 3f;
        avgExtent /= Mathf.Max(1, n);
        gap = Mathf.Max(gap, avgExtent * 0.55f);

        int linkCount = 0;
        for (int i = 0; i < n; i++)
        {
            Bounds bi = bounds[i];
            bi.Expand(gap);
            for (int j = i + 1; j < n; j++)
            {
                if (!bi.Intersects(bounds[j]))
                    continue;
                _supportLinks[i].Add(j);
                _supportLinks[j].Add(i);
                linkCount++;
            }
        }

        int groundedSeeds = 0;
        for (int i = 0; i < n; i++)
        {
            if (IsGrounded(i))
                groundedSeeds++;
        }

        // If almost nothing is grounded, graph is unreliable — disable collapse
        if (groundedSeeds < Mathf.Max(2, n / 25))
        {
            _collapseUnsupported = false;
            Debug.LogWarning(
                $"PreSlicedCastleWall: collapse disabled (grounded={groundedSeeds}/{n}, links={linkCount}).",
                this);
        }
        else
        {
            Debug.Log($"PreSlicedCastleWall support: grounded={groundedSeeds}, links={linkCount}, gap={gap:0.##}", this);
        }
    }

    bool IsGrounded(int index)
    {
        var chunk = _chunks[index];
        if (chunk == null || chunk.IsDetached)
            return false;

        var col = chunk.ChunkCollider;
        float bottom = col != null ? col.bounds.min.y : chunk.transform.position.y;
        if (bottom <= _groundY + _groundSlack)
            return true;

        // Also treat contact with non-wall world below as support
        Vector3 origin = col != null ? col.bounds.center : chunk.transform.position;
        float dist = col != null ? col.bounds.extents.y + _groundSlack + 0.05f : 0.4f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            var hitChunk = hit.collider.GetComponentInParent<CastleWallChunk>();
            if (hitChunk == null)
                return true; // ground / prop
            if (!hitChunk.IsDetached && hitChunk != chunk)
                return false; // resting on another wall chunk — connectivity handles it
        }

        return false;
    }

    void CollapseUnsupported()
    {
        if (!_collapseUnsupported || _collapsing || _supportLinks == null)
            return;

        _collapsing = true;
        try
        {
            // Support links are indexed into the original stable _chunks list — never shrink it
            int n = Mathf.Min(_chunks.Count, _supportLinks.Length);
            var supported = new bool[n];
            var queue = new Queue<int>(n);

            for (int i = 0; i < n; i++)
            {
                if (_chunks[i] == null || _chunks[i].IsDetached)
                    continue;
                if (!IsGrounded(i))
                    continue;
                supported[i] = true;
                queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (i < 0 || i >= _supportLinks.Length)
                    continue;
                var links = _supportLinks[i];
                if (links == null)
                    continue;

                for (int k = 0; k < links.Count; k++)
                {
                    int j = links[k];
                    if (j < 0 || j >= n || supported[j])
                        continue;
                    var other = _chunks[j];
                    if (other == null || other.IsDetached)
                        continue;
                    supported[j] = true;
                    queue.Enqueue(j);
                }
            }

            for (int i = 0; i < n; i++)
            {
                var chunk = _chunks[i];
                if (chunk == null || chunk.IsDetached || supported[i])
                    continue;
                chunk.Detach(chunk.transform.position, Vector3.down, _detachImpulse * 0.5f);
            }
        }
        finally
        {
            _collapsing = false;
        }
    }

    static bool HasMeshFilterInChildren(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            if (t.GetChild(i).GetComponentInChildren<MeshFilter>(true) != null)
                return true;
        }
        return false;
    }

    void FitScaleToTargetHeight()
    {
        if (_root == null || _targetWorldHeight <= 0.01f)
            return;

        var renderers = _root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        float height = b.size.y;
        if (height < 0.01f)
            return;

        _root.localScale = Vector3.one * (_targetWorldHeight / height);

        renderers = _root.GetComponentsInChildren<Renderer>();
        b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Vector3 pos = _root.position;
        pos.y += transform.position.y - b.min.y;
        _root.position = pos;
    }

    void CacheChunks()
    {
        _chunks.Clear();
        if (_root != null)
            _root.GetComponentsInChildren(true, _chunks);
    }

    readonly List<CastleWallChunk> _pickScratch = new List<CastleWallChunk>(64);

    public bool ApplyPickaxeHit(Vector3 worldPoint, Vector3 hitNormal, int damage)
    {
        if (_chunks.Count == 0 || damage <= 0)
            return false;

        // Do NOT mutate _chunks order/size — support graph indices depend on it
        _pickScratch.Clear();
        for (int i = 0; i < _chunks.Count; i++)
        {
            var c = _chunks[i];
            if (c == null || c.IsDetached)
                continue;
            _pickScratch.Add(c);
        }

        if (_pickScratch.Count == 0)
            return false;

        _pickScratch.Sort((a, b) => ChunkDistanceSq(a, worldPoint).CompareTo(ChunkDistanceSq(b, worldPoint)));

        float r2 = _pickaxeBreakRadius * _pickaxeBreakRadius;
        int broken = 0;
        for (int i = 0; i < _pickScratch.Count && broken < _maxChunksPerPickHit; i++)
        {
            var chunk = _pickScratch[i];
            float d2 = ChunkDistanceSq(chunk, worldPoint);
            if (d2 > r2 && broken > 0)
                break;

            if (chunk.ApplyHit(damage, worldPoint, hitNormal))
                broken++;
        }

        if (broken > 0)
            CollapseUnsupported();

        return broken > 0;
    }

    public void ApplyBlast(Vector3 worldPoint, float radius, float explosionForce, Vector3 preferredOutward)
    {
        if (_chunks.Count == 0)
            return;

        float effectiveRadius = Mathf.Max(0.5f, radius);
        float r2 = effectiveRadius * effectiveRadius;
        int maxDetach = Mathf.Max(1, _maxChunksPerBlast);

        Vector3 blastOut = preferredOutward.sqrMagnitude > 0.01f
            ? preferredOutward.normalized
            : Vector3.forward;

        var candidates = new List<(CastleWallChunk chunk, float d2)>(32);
        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk == null || chunk.IsDetached)
                continue;

            float d2 = ChunkDistanceSq(chunk, worldPoint);
            if (d2 > r2)
                continue;
            candidates.Add((chunk, d2));
        }

        candidates.Sort((a, b) => a.d2.CompareTo(b.d2));

        float blastImpulse = Mathf.Clamp(explosionForce * 0.035f, 1.0f, 4.0f);

        int detached = 0;
        for (int i = 0; i < candidates.Count && detached < maxDetach; i++)
        {
            var chunk = candidates[i].chunk;
            // Prefer shooter-facing outward; blend with radial from blast point
            Vector3 radial = chunk.transform.position - worldPoint;
            radial.y = Mathf.Max(0f, radial.y);
            if (radial.sqrMagnitude < 0.0001f)
                radial = blastOut;
            else
                radial.Normalize();

            Vector3 outward = (blastOut * 0.75f + radial * 0.35f + Vector3.up * 0.1f).normalized;
            chunk.Detach(worldPoint, outward, blastImpulse);
            detached++;
        }

        if (detached > 0)
            CollapseUnsupported();
    }

    // Back-compat overload
    public void ApplyBlast(Vector3 worldPoint, float radius, float explosionForce)
    {
        ApplyBlast(worldPoint, radius, explosionForce, Vector3.forward);
    }

    static float ChunkDistanceSq(CastleWallChunk chunk, Vector3 worldPoint)
    {
        var col = chunk.GetComponent<Collider>();
        if (col != null)
        {
            Vector3 closest = col.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude;
        }
        return (chunk.transform.position - worldPoint).sqrMagnitude;
    }
}
