using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghost placement with grid + neighbor snap for foundations, walls, roofs, stairs.
/// Wall: center on foundation edge (half thickness each side of the joint).
/// Roof: outer edge flush with wall outer edge.
/// </summary>
public sealed class BuildingPlacer
{
    readonly Camera _camera;
    readonly BuildingDefinition _definition;
    readonly GameObject _ghost;
    readonly List<Vector3> _snapScratch = new List<Vector3>(8);
    readonly Collider[] _overlapScratch = new Collider[32];

    int _yawSteps;
    bool _valid;
    bool _snappedNeighbor;
    Vector3 _position;
    Quaternion _rotation;

    public bool IsValid => _valid;
    public BuildingDefinition Definition => _definition;

    public BuildingPlacer(Camera camera, BuildingDefinition definition)
    {
        _camera = camera;
        _definition = definition;
        _ghost = BuildingPrefabFactory.CreateGhost(definition);
        _ghost.SetActive(false);
    }

    public void Dispose()
    {
        if (_ghost != null)
            Object.Destroy(_ghost);
    }

    public void SetVisible(bool visible)
    {
        if (_ghost != null)
            _ghost.SetActive(visible);
    }

    public void Rotate(int steps)
    {
        _yawSteps = (_yawSteps + steps) % 4;
        if (_yawSteps < 0)
            _yawSteps += 4;
    }

    public void Tick()
    {
        if (_camera == null || _definition == null || _ghost == null)
            return;

        _rotation = Quaternion.Euler(0f, _yawSteps * 90f, 0f);
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, 14f, ~0, QueryTriggerInteraction.Ignore))
        {
            _valid = false;
            _ghost.SetActive(false);
            return;
        }

        _snappedNeighbor = false;
        _position = hit.point;
        ApplyDefaultHeight(hit);

        if (_definition.SnapToNeighbors)
            TrySnapToNeighbors(hit.point, hit);

        if (_definition.SnapToGrid && !_snappedNeighbor)
            SnapToWorldGrid(ref _position);

        // Free foundation follows camera yaw so one face stays toward the player
        if (!_snappedNeighbor && _definition.Kind == BuildingPieceKind.Foundation)
            ApplyCameraFacingYaw();

        _valid = IsPlacementValid();
        _ghost.SetActive(true);
        _ghost.transform.SetPositionAndRotation(_position, _rotation);
        SetGhostColor(_valid);
    }

    void ApplyCameraFacingYaw()
    {
        Vector3 flat = _camera.transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            return;

        float cameraYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        // +Z faces camera forward → near face of the slab looks at the player
        _rotation = Quaternion.Euler(0f, cameraYaw + _yawSteps * 90f, 0f);
    }

    void ApplyDefaultHeight(RaycastHit hit)
    {
        float halfY = _definition.Size.y * 0.5f;
        var under = hit.collider != null ? hit.collider.GetComponentInParent<BuildingPiece>() : null;

        switch (_definition.Kind)
        {
            case BuildingPieceKind.Wall:
                if (under != null && under.Definition != null &&
                    under.Definition.Kind == BuildingPieceKind.Foundation)
                    _position.y = under.transform.position.y + under.Size.y * 0.5f + halfY;
                else
                    _position.y = hit.point.y + halfY;
                break;

            case BuildingPieceKind.Roof:
                if (under != null)
                    _position.y = under.transform.position.y + under.Size.y * 0.5f + halfY;
                else
                    _position.y = hit.point.y + halfY;
                break;

            case BuildingPieceKind.Stairs:
                _position.y = hit.point.y;
                break;

            default:
                _position.y = hit.point.y + halfY;
                break;
        }
    }

    void TrySnapToNeighbors(Vector3 rawHit, RaycastHit hit)
    {
        float best = 1.25f;
        Vector3 bestPos = _position;
        Quaternion bestRot = _rotation;

        switch (_definition.Kind)
        {
            case BuildingPieceKind.Wall:
                TrySnapWall(rawHit, hit, ref best, ref bestPos, ref bestRot);
                break;
            case BuildingPieceKind.Roof:
                TrySnapRoof(rawHit, hit, ref best, ref bestPos, ref bestRot);
                break;
            default:
                // Start with a generous search radius for foundation rows
                best = 2.25f;
                TrySnapFootprint(rawHit, ref best, ref bestPos, ref bestRot);
                break;
        }

        if (_snappedNeighbor)
        {
            _position = bestPos;
            _rotation = bestRot;
            float yaw = bestRot.eulerAngles.y;
            _yawSteps = Mathf.RoundToInt(yaw / 90f) % 4;
            if (_yawSteps < 0)
                _yawSteps += 4;
        }
    }

    /// <summary>
    /// Wall center sits on the foundation edge so half thickness lies on each side of the joint.
    /// </summary>
    void TrySnapWall(Vector3 rawHit, RaycastHit hit, ref float best, ref Vector3 bestPos, ref Quaternion bestRot)
    {
        var pieces = Object.FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            if (piece?.Definition == null || piece.Definition.Kind != BuildingPieceKind.Foundation)
                continue;

            piece.GetEdgeSnapPoints(_snapScratch);
            for (int s = 0; s < _snapScratch.Count; s++)
            {
                Vector3 edge = _snapScratch[s];
                Vector3 outward = edge - piece.transform.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.0001f)
                    continue;
                outward.Normalize();

                // Center ON the edge (joint) — not offset by half thickness
                Vector3 candidate = edge;
                candidate.y = piece.transform.position.y + piece.Size.y * 0.5f + _definition.Size.y * 0.5f;
                Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);

                float dist = PlanarDist(rawHit, candidate);
                if (dist >= best)
                    continue;

                best = dist;
                bestPos = candidate;
                bestRot = rot;
                _snappedNeighbor = true;
            }
        }

        // Looking at a foundation: pick nearest edge under the cursor
        var under = hit.collider != null ? hit.collider.GetComponentInParent<BuildingPiece>() : null;
        if (under?.Definition == null || under.Definition.Kind != BuildingPieceKind.Foundation)
            return;

        if (!TryGetNearestFoundationEdge(under, rawHit, out Vector3 edgePt, out Vector3 outwardDir))
            return;

        Vector3 cand = edgePt;
        cand.y = under.transform.position.y + under.Size.y * 0.5f + _definition.Size.y * 0.5f;
        float d = PlanarDist(rawHit, cand);
        if (d < best)
        {
            best = d;
            bestPos = cand;
            bestRot = Quaternion.LookRotation(outwardDir, Vector3.up);
            _snappedNeighbor = true;
        }
    }

    /// <summary>
    /// Roof outer edge flush with wall outer edge; roof extends inward over the room.
    /// </summary>
    void TrySnapRoof(Vector3 rawHit, RaycastHit hit, ref float best, ref Vector3 bestPos, ref Quaternion bestRot)
    {
        float roofHalfZ = _definition.Size.z * 0.5f;
        float roofHalfX = _definition.Size.x * 0.5f;

        var pieces = Object.FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            var wall = pieces[i];
            if (wall?.Definition == null || wall.Definition.Kind != BuildingPieceKind.Wall)
                continue;

            // Wall thin axis = local forward (+Z)
            Vector3 outward = wall.transform.forward;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f)
                continue;
            outward.Normalize();

            float wallHalfThick = wall.Size.z * 0.5f;
            // Outer face of the wall
            Vector3 wallOuterEdge = wall.transform.position + outward * wallHalfThick;

            // Roof outer edge = wall outer edge → roof center sits inward
            // roof.pos + outward * roofHalf = wallOuterEdge
            Vector3 candidate = wallOuterEdge - outward * roofHalfZ;
            candidate.y = wall.transform.position.y + wall.Size.y * 0.5f + _definition.Size.y * 0.5f;

            // Keep roof centered along the wall length
            Vector3 along = wall.transform.right;
            along.y = 0f;
            along.Normalize();
            Vector3 wallCenter = wall.transform.position;
            candidate = wallCenter - outward * (roofHalfZ - wallHalfThick);
            candidate.y = wall.transform.position.y + wall.Size.y * 0.5f + _definition.Size.y * 0.5f;

            Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);

            float dist = PlanarDist(rawHit, candidate);
            // Also try the opposite face of the wall (inner / outer swap)
            Vector3 candidateIn = wallCenter + outward * (roofHalfZ - wallHalfThick);
            candidateIn.y = candidate.y;
            float distIn = PlanarDist(rawHit, candidateIn);
            if (distIn < dist)
            {
                dist = distIn;
                candidate = candidateIn;
                rot = Quaternion.LookRotation(-outward, Vector3.up);
            }

            if (dist >= best)
                continue;

            best = dist;
            bestPos = candidate;
            bestRot = rot;
            _snappedNeighbor = true;
        }

        // Aiming at a wall top — prefer that wall's snap
        var under = hit.collider != null ? hit.collider.GetComponentInParent<BuildingPiece>() : null;
        if (under?.Definition != null && under.Definition.Kind == BuildingPieceKind.Wall)
        {
            Vector3 outward = under.transform.forward;
            outward.y = 0f;
            outward.Normalize();
            float wallHalfThick = under.Size.z * 0.5f;

            Vector3 candA = under.transform.position - outward * (roofHalfZ - wallHalfThick);
            Vector3 candB = under.transform.position + outward * (roofHalfZ - wallHalfThick);
            candA.y = under.transform.position.y + under.Size.y * 0.5f + _definition.Size.y * 0.5f;
            candB.y = candA.y;

            float dA = PlanarDist(rawHit, candA);
            float dB = PlanarDist(rawHit, candB);
            if (dA <= dB && dA < best)
            {
                best = dA;
                bestPos = candA;
                bestRot = Quaternion.LookRotation(outward, Vector3.up);
                _snappedNeighbor = true;
            }
            else if (dB < best)
            {
                best = dB;
                bestPos = candB;
                bestRot = Quaternion.LookRotation(-outward, Vector3.up);
                _snappedNeighbor = true;
            }
        }

        // Roof-to-roof side snap (edge to edge)
        for (int i = 0; i < pieces.Length; i++)
        {
            var roof = pieces[i];
            if (roof?.Definition == null || roof.Definition.Kind != BuildingPieceKind.Roof)
                continue;

            roof.GetEdgeSnapPoints(_snapScratch);
            for (int s = 0; s < _snapScratch.Count; s++)
            {
                Vector3 edge = _snapScratch[s];
                Vector3 outward = edge - roof.transform.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.0001f)
                    continue;
                outward.Normalize();

                // New roof edge meets existing roof edge
                float half = Mathf.Abs(Vector3.Dot(outward, roof.transform.forward)) > 0.5f
                    ? roofHalfZ
                    : roofHalfX;
                Vector3 candidate = edge + outward * half;
                candidate.y = roof.transform.position.y;

                float dist = PlanarDist(rawHit, candidate);
                if (dist >= best)
                    continue;

                best = dist;
                bestPos = candidate;
                bestRot = roof.transform.rotation;
                _snappedNeighbor = true;
            }
        }
    }

    void TrySnapFootprint(Vector3 rawHit, ref float best, ref Vector3 bestPos, ref Quaternion bestRot)
    {
        // Cell width so the next free slot past a row still snaps
        float cell = Mathf.Max(_definition.Size.x, _definition.Size.z);
        best = Mathf.Max(best, cell * 1.15f);
        float bestDist = best;

        var pieces = Object.FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            if (piece?.Definition == null)
                continue;
            if (!CanSnapTo(piece.Definition.Kind, _definition.Kind))
                continue;

            piece.GetEdgeSnapPoints(_snapScratch);
            for (int s = 0; s < _snapScratch.Count; s++)
            {
                if (!TryBuildFootprintCandidate(piece, _snapScratch[s], out Vector3 candidate, out Quaternion rot))
                    continue;
                if (IsFootprintOccupied(candidate, pieces))
                    continue;

                float dist = PlanarDist(rawHit, candidate);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                best = dist;
                bestPos = candidate;
                bestRot = rot;
                _snappedNeighbor = true;
            }
        }
    }

    bool TryBuildFootprintCandidate(BuildingPiece piece, Vector3 edge, out Vector3 candidate, out Quaternion rot)
    {
        candidate = default;
        rot = piece.transform.rotation;

        Vector3 outward = edge - piece.transform.position;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
            return false;
        outward.Normalize();

        float alongRight = Mathf.Abs(Vector3.Dot(outward, piece.transform.right));
        float halfFoot = alongRight > 0.5f
            ? _definition.Size.x * 0.5f
            : _definition.Size.z * 0.5f;

        candidate = edge + outward * halfFoot;
        candidate.y = piece.transform.position.y;
        if (_definition.Kind == BuildingPieceKind.Stairs)
            candidate.y = piece.transform.position.y - piece.Size.y * 0.5f;
        return true;
    }

    bool IsFootprintOccupied(Vector3 candidate, BuildingPiece[] pieces)
    {
        // Same cell as an existing foundation/stairs (adjacent cells are ~1 cell apart)
        float minSep = Mathf.Min(_definition.Size.x, _definition.Size.z) * 0.5f;
        float minSepSq = minSep * minSep;
        for (int i = 0; i < pieces.Length; i++)
        {
            var p = pieces[i];
            if (p == null || p.Definition == null)
                continue;
            if (p.Definition.Kind != BuildingPieceKind.Foundation && p.Definition.Kind != BuildingPieceKind.Stairs)
                continue;

            Vector3 delta = p.transform.position - candidate;
            delta.y = 0f;
            if (delta.sqrMagnitude < minSepSq)
                return true;
        }
        return false;
    }

    static bool TryGetNearestFoundationEdge(BuildingPiece foundation, Vector3 rawHit, out Vector3 edge, out Vector3 outward)
    {
        edge = default;
        outward = default;
        Vector3 local = foundation.transform.InverseTransformPoint(rawHit);
        Vector3 size = foundation.Size;

        float toPosZ = size.z * 0.5f - local.z;
        float toNegZ = local.z + size.z * 0.5f;
        float toPosX = size.x * 0.5f - local.x;
        float toNegX = local.x + size.x * 0.5f;
        float min = Mathf.Min(toPosZ, toNegZ, toPosX, toNegX);

        if (min == toPosZ)
        {
            outward = foundation.transform.forward;
            edge = foundation.transform.position + outward * (size.z * 0.5f);
        }
        else if (min == toNegZ)
        {
            outward = -foundation.transform.forward;
            edge = foundation.transform.position + outward * (size.z * 0.5f);
        }
        else if (min == toPosX)
        {
            outward = foundation.transform.right;
            edge = foundation.transform.position + outward * (size.x * 0.5f);
        }
        else
        {
            outward = -foundation.transform.right;
            edge = foundation.transform.position + outward * (size.x * 0.5f);
        }

        outward.y = 0f;
        outward.Normalize();
        return true;
    }

    static float PlanarDist(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static bool CanSnapTo(BuildingPieceKind existing, BuildingPieceKind placing)
    {
        switch (placing)
        {
            case BuildingPieceKind.Foundation:
            case BuildingPieceKind.Stairs:
                return existing == BuildingPieceKind.Foundation || existing == BuildingPieceKind.Stairs;
            case BuildingPieceKind.Wall:
                return existing == BuildingPieceKind.Foundation;
            case BuildingPieceKind.Roof:
                return existing == BuildingPieceKind.Wall || existing == BuildingPieceKind.Roof;
            default:
                return true;
        }
    }

    void SnapToWorldGrid(ref Vector3 pos)
    {
        float sx = Mathf.Max(0.1f, _definition.Size.x);
        float sz = Mathf.Max(0.1f, _definition.Kind == BuildingPieceKind.Wall ? _definition.Size.x : _definition.Size.z);
        pos.x = Mathf.Round(pos.x / sx) * sx;
        pos.z = Mathf.Round(pos.z / sz) * sz;
    }

    bool IsPlacementValid()
    {
        Vector3 half = _definition.Size * 0.5f;
        // Slightly inset so edge-touching neighbors don't fail validation
        Vector3 checkHalf = half * 0.82f;
        Vector3 center = _position;
        if (_definition.Kind == BuildingPieceKind.Stairs)
            center.y += half.y;

        // Wall centered on joint overlaps foundation slightly — allow that
        if (_definition.Kind == BuildingPieceKind.Wall)
            checkHalf.z *= 0.5f;

        int count = Physics.OverlapBoxNonAlloc(
            center, checkHalf, _overlapScratch, _rotation, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var col = _overlapScratch[i];
            if (col == null)
                continue;
            if (col.transform.IsChildOf(_ghost.transform) || col.transform == _ghost.transform)
                continue;

            var otherPiece = col.GetComponentInParent<BuildingPiece>();
            if (otherPiece != null)
            {
                var otherKind = otherPiece.Definition != null ? otherPiece.Definition.Kind : BuildingPieceKind.Foundation;

                // Wall may overlap foundations (sits on the joint)
                if (_definition.Kind == BuildingPieceKind.Wall && otherKind == BuildingPieceKind.Foundation)
                    continue;

                // Roof may rest on wall top
                if (_definition.Kind == BuildingPieceKind.Roof && otherKind == BuildingPieceKind.Wall)
                    continue;

                // Foundations/stairs: reject only if centers are too close (real overlap),
                // not when boxes barely touch at the shared edge
                if (_definition.Kind == BuildingPieceKind.Foundation || _definition.Kind == BuildingPieceKind.Stairs)
                {
                    Vector3 delta = otherPiece.transform.position - center;
                    delta.y = 0f;
                    float minCenter = Mathf.Min(_definition.Size.x, _definition.Size.z) * 0.9f;
                    if (delta.sqrMagnitude < minCenter * minCenter)
                        return false;
                    continue;
                }

                if (otherKind == _definition.Kind)
                    return false;
                continue;
            }

            if (col.GetComponentInParent<CharacterController>() != null)
                return false;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)
                return false;
        }

        return true;
    }

    void SetGhostColor(bool valid)
    {
        Color c = valid
            ? new Color(0.2f, 0.85f, 0.35f, 0.5f)
            : new Color(0.9f, 0.2f, 0.15f, 0.5f);
        foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterial != null)
                r.sharedMaterial.color = c;
        }
    }

    public bool TryPlace(PlayerInventory inventory, ItemDefinition stoneItem)
    {
        if (!_valid || _definition == null || inventory == null)
            return false;

        int cost = _definition.StoneCost;
        if (cost > 0)
        {
            if (stoneItem == null || inventory.CountItem(stoneItem) < cost)
                return false;
            if (!inventory.TryConsumeItem(stoneItem, cost))
                return false;
        }

        GameObject placed;
        if (_definition.Prefab != null)
        {
            placed = Object.Instantiate(_definition.Prefab, _position, _rotation);
            placed.transform.localScale = _definition.Size;
        }
        else
        {
            placed = BuildingPrefabFactory.CreateRuntimePiece(_definition);
            placed.transform.SetPositionAndRotation(_position, _rotation);
        }

        placed.name = _definition.DisplayName;
        var piece = placed.GetComponent<BuildingPiece>();
        if (piece == null)
            piece = placed.AddComponent<BuildingPiece>();
        piece.Init(_definition);

        return true;
    }
}
