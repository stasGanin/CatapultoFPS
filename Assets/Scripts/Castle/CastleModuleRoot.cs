using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prefab root for one authored castle module (wall / door / spawner / tower).
/// Wires sliced pieces and keeps attached colliders from fighting each other.
/// </summary>
[DisallowMultipleComponent]
public sealed class CastleModuleRoot : MonoBehaviour
{
    [SerializeField] CastleModuleKind _kind = CastleModuleKind.Wall;
    [SerializeField] float _footprintWidth = 4f;
    [SerializeField] float _footprintDepth = 1.2f;
    [SerializeField] float _height = 4f;
    [SerializeField] Transform _golemSocket;
    [SerializeField] CastleWallChunk _keyChunk;
    [SerializeField] int _detachHp = 8;
    [SerializeField] int _mineHp = 10;
    [SerializeField] float _detachImpulse = 0.55f;

    readonly List<CastleWallChunk> _chunks = new List<CastleWallChunk>(32);

    public CastleModuleKind Kind => _kind;
    public float FootprintWidth => Mathf.Max(0.5f, _footprintWidth);
    public float FootprintDepth => Mathf.Max(0.2f, _footprintDepth);
    public float Height => Mathf.Max(0.5f, _height);
    public Transform GolemSocket => _golemSocket;
    public bool BelongsToPlayerCastle
    {
        get
        {
            var square = GetComponentInParent<SquareCastle>();
            return square != null && square.IsPlayerOwned;
        }
    }

    public void SetKind(CastleModuleKind kind) => _kind = kind;

    public bool NeedsRepair()
    {
        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk == null || chunk.NeedsRestore)
                return true;
        }

        return false;
    }

    public bool TryRepairFull()
    {
        if (!NeedsRepair())
            return false;

        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk == null)
                continue;
            if (chunk.IsDetached)
                chunk.RestoreToSocket();
            else
                chunk.RestoreFull();
        }

        IgnoreAttachedPairs(true);
        EnsureKeyChunk();
        HighlightKeyChunk();
        var breakable = GetComponent<CarcassWallBreakable>();
        if (breakable != null)
            breakable.OnRepaired();
        return true;
    }

    public void SetRepairHighlight(bool on, bool damaged = true)
    {
        Color tint = damaged
            ? new Color(0.45f, 1f, 0.42f, 1f)
            : new Color(0.85f, 0.82f, 0.45f, 1f);
        var block = new MaterialPropertyBlock();

        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk == null)
                continue;

            var rends = chunk.GetComponentsInChildren<Renderer>(true);
            if (chunk.IsDetached)
            {
                for (int r = 0; r < rends.Length; r++)
                {
                    if (rends[r] != null)
                        rends[r].enabled = false;
                }

                continue;
            }

            for (int r = 0; r < rends.Length; r++)
            {
                var rend = rends[r];
                if (rend == null)
                    continue;
                if (!on)
                {
                    rend.SetPropertyBlock(null);
                    continue;
                }

                rend.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                block.SetColor("_EmissionColor", tint * 0.25f);
                rend.SetPropertyBlock(block);
            }
        }

        if (!on)
            HighlightKeyChunk();
    }

    public void SetBuildHighlight(bool on)
    {
        SetPreviewHidden(on);
    }

    /// <summary>
    /// Hide mesh while a build ghost occupies this slot. Clears leftover tints so albedo
    /// does not stay white after the preview ends.
    /// </summary>
    public void SetPreviewHidden(bool hidden)
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null)
                continue;
            rends[i].enabled = !hidden;
            if (!hidden)
                rends[i].SetPropertyBlock(null);
        }

        if (!hidden)
            HighlightKeyChunk();
    }

    public static string PrefabResourceFor(CastleModuleKind kind)
    {
        switch (kind)
        {
            case CastleModuleKind.Door: return "Castle/Carcass/Source/WallDoor";
            case CastleModuleKind.Wall: return "Castle/Carcass/Source/Wall";
            case CastleModuleKind.Window: return "Castle/Carcass/Source/WallWindows";
            case CastleModuleKind.Spawner: return "Castle/Modules/SpawnerModule";
            case CastleModuleKind.Tower: return "Castle/Modules/TowerModule";
            default: return "";
        }
    }

    public void SetFootprint(float width, float depth, float height)
    {
        _footprintWidth = width;
        _footprintDepth = depth;
        _height = height;
    }

    public void SetGolemSocket(Transform socket) => _golemSocket = socket;

    void Awake()
    {
        StripImportedCamerasAndLights();
        CacheChunks();
        EnsureKeyChunk();
        HighlightKeyChunk();
        IgnoreAttachedPairs(true);
    }

    static void StripImportedCamerasAndLights(Transform root)
    {
        var cams = root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] == null)
                continue;
            cams[i].enabled = false;
            if (cams[i].GetComponent<MeshFilter>() == null)
                DestroyCompat(cams[i].gameObject);
            else
                DestroyCompat(cams[i]);
        }

        var lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null)
                continue;
            DestroyCompat(lights[i]);
        }
    }

    void StripImportedCamerasAndLights() => StripImportedCamerasAndLights(transform);

    public void CacheChunks()
    {
        _chunks.Clear();
        GetComponentsInChildren(true, _chunks);
    }

    public void OnChunkDetached(CastleWallChunk detached)
    {
        if (detached == null || detached.ChunkCollider == null)
            return;

        for (int i = 0; i < _chunks.Count; i++)
        {
            var other = _chunks[i];
            if (other == null || other == detached || other.ChunkCollider == null)
                continue;

            // Keep ignoring still-attached siblings so convex hulls don't explode.
            Physics.IgnoreCollision(detached.ChunkCollider, other.ChunkCollider, !other.IsDetached);
        }

        if (detached == _keyChunk)
            NotifyModuleDestroyed();

        var breakable = GetComponent<CarcassWallBreakable>();
        if (breakable != null)
            breakable.OnChunkDetached(detached);
    }

    public void IgnoreAttachedPairs(bool ignore)
    {
        var live = GetComponentsInChildren<CastleWallChunk>(true);
        for (int i = 0; i < live.Length; i++)
        {
            var a = live[i];
            if (a == null || a.ChunkCollider == null || a.IsDetached)
                continue;
            for (int j = i + 1; j < live.Length; j++)
            {
                var b = live[j];
                if (b == null || b.ChunkCollider == null || b.IsDetached)
                    continue;
                Physics.IgnoreCollision(a.ChunkCollider, b.ChunkCollider, ignore);
            }
        }
    }

    public void PrepareAuthoredPieces()
    {
        var filters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;
            if (HasMeshFilterInChildren(mf.transform))
                continue;

            GameObject go = mf.gameObject;
            bool destroyInPlace = IsCoreSegment(go.name);

            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 5f;
            }

            var chunk = go.GetComponent<CastleWallChunk>();
            if (chunk == null)
                chunk = go.AddComponent<CastleWallChunk>();

            chunk.ConfigureSegment(_detachHp, _mineHp, _detachImpulse, destroyInPlace);
        }

        CacheChunks();
        IgnoreAttachedPairs(true);
        EnsureKeyChunk();
        HighlightKeyChunk();
    }

    void EnsureKeyChunk()
    {
        if (_keyChunk != null && !_keyChunk.IsDetached)
            return;

        CacheChunks();
        for (int i = 0; i < _chunks.Count; i++)
        {
            if (_chunks[i] != null && !_chunks[i].IsDetached)
            {
                _keyChunk = _chunks[i];
                return;
            }
        }
    }

    void HighlightKeyChunk()
    {
        if (_keyChunk == null || _keyChunk.IsDetached || _kind != CastleModuleKind.Spawner)
            return;

        var rends = _keyChunk.GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();
        Color tint = new Color(0.55f, 1f, 0.95f, 1f);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null)
                continue;
            rends[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            block.SetColor("_EmissionColor", tint * 0.35f);
            rends[i].SetPropertyBlock(block);
        }
    }

    void NotifyModuleDestroyed()
    {
        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICastleModuleBehavior module)
                module.OnModuleDestroyed();
        }
    }

    static bool IsCoreSegment(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name == "Tower";
    }

    static bool HasMeshFilterInChildren(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            if (child.name == "Physics")
                continue;
            if (child.GetComponent<MeshFilter>() != null)
                return true;
            if (HasMeshFilterInChildren(child))
                return true;
        }

        return false;
    }

    static void DestroyCompat(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_golemSocket == null)
            return;
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireSphere(_golemSocket.position, 0.45f);
    }
#endif
}
