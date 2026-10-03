using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime square castle assembled from module prefabs (3 per side).</summary>
public sealed class SquareCastle : MonoBehaviour
{
    [SerializeField] bool _playerOwned;

    readonly List<CastleWallChunk> _chunks = new List<CastleWallChunk>(128);

    public bool IsPlayerOwned => _playerOwned;

    public static SquareCastle FindPlayerOwned()
    {
        var all = FindObjectsByType<SquareCastle>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsPlayerOwned)
                return all[i];
        }

        return null;
    }

    public void SetPlayerOwned(bool playerOwned) => _playerOwned = playerOwned;

    void Start()
    {
        RefreshIgnorePairs();
    }

    public void RefreshIgnorePairs()
    {
        _chunks.Clear();
        GetComponentsInChildren(true, _chunks);
        for (int i = 0; i < _chunks.Count; i++)
        {
            var a = _chunks[i];
            if (a == null || a.ChunkCollider == null || a.IsDetached)
                continue;
            for (int j = i + 1; j < _chunks.Count; j++)
            {
                var b = _chunks[j];
                if (b == null || b.ChunkCollider == null || b.IsDetached)
                    continue;
                Physics.IgnoreCollision(a.ChunkCollider, b.ChunkCollider, true);
            }
        }
    }

    public void ApplyBlast(Vector3 worldPoint, float radius, float explosionForce, Vector3 preferredOutward)
    {
        if (_playerOwned)
            return;
        RefreshIgnorePairs();
        float effectiveRadius = Mathf.Max(0.05f, radius);
        float r2 = effectiveRadius * effectiveRadius;
        const int maxDetach = 3;

        Vector3 blastOut = preferredOutward.sqrMagnitude > 0.01f
            ? preferredOutward.normalized
            : Vector3.forward;

        var candidates = new List<(CastleWallChunk chunk, float d2)>(32);
        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk == null || chunk.IsDetached)
                continue;

            float d2 = DistanceSq(chunk, worldPoint);
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
    }

    static float DistanceSq(CastleWallChunk chunk, Vector3 worldPoint)
    {
        var rend = chunk.GetComponent<Renderer>();
        if (rend != null)
            return (rend.bounds.center - worldPoint).sqrMagnitude;
        return (chunk.transform.position - worldPoint).sqrMagnitude;
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
            Physics.IgnoreCollision(detached.ChunkCollider, other.ChunkCollider, !other.IsDetached);
        }
    }
}
