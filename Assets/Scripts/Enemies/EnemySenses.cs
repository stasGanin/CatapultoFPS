using UnityEngine;

/// <summary>
/// Shared enemy perception: player lookup, clear-shot checks, ground probe, castle geometry.
/// </summary>
public static class EnemySenses
{
    const float PlayerLookupInterval = 1f;
    const float GroundProbeDepth = 30f;
    const float CastleFootprintMargin = 0.6f;

    static readonly RaycastHit[] Hits = new RaycastHit[24];

    static Transform _player;
    static CharacterController _playerController;
    static float _nextPlayerLookup;
    static SquareCastle _playerCastle;
    static float _nextCastleLookup;

    public static Transform Player
    {
        get
        {
            if (_player == null && Time.time >= _nextPlayerLookup)
            {
                _nextPlayerLookup = Time.time + PlayerLookupInterval;
                var go = GameObject.FindGameObjectWithTag("Player");
                _player = go != null ? go.transform : null;
                _playerController = go != null ? go.GetComponent<CharacterController>() : null;
            }

            return _player;
        }
    }

    public static SquareCastle PlayerCastle
    {
        get
        {
            if (_playerCastle == null && Time.time >= _nextCastleLookup)
            {
                _nextCastleLookup = Time.time + PlayerLookupInterval;
                _playerCastle = SquareCastle.FindPlayerOwned();
            }

            return _playerCastle;
        }
    }

    /// <summary>Центр капсулы игрока: pivot стоит у ног, стрелять в него — бить в пол.</summary>
    public static Vector3 PlayerAimPoint()
    {
        Transform player = Player;
        if (player == null)
            return Vector3.zero;
        if (_playerController != null)
            return _playerController.bounds.center;
        return player.position + Vector3.up;
    }

    public static Vector3 PlayerVelocity()
    {
        return _playerController != null ? _playerController.velocity : Vector3.zero;
    }

    public static bool IsPlayer(Collider col)
    {
        return col != null && col.GetComponentInParent<PlayerHealth>() != null;
    }

    public static bool IsEnemy(Collider col)
    {
        if (col == null)
            return false;
        return col.GetComponentInParent<EnemyUnit>() != null
               || col.GetComponentInParent<EnemyRoller>() != null
               || col.GetComponentInParent<EnemyBrute>() != null
               || col.GetComponentInParent<EnemyBomber>() != null;
    }

    /// <summary>
    /// Пролетит ли снаряд радиуса radius от origin до target. Первое препятствие (кроме себя,
    /// союзников и снарядов) должно принадлежать targetRoot. Землю НЕ пропускаем —
    /// иначе стрелки «видят» сквозь холмы и бьют в рельеф.
    /// </summary>
    public static bool HasClearPath(Vector3 origin, Vector3 target, float radius, Transform self, Transform targetRoot)
    {
        Vector3 delta = target - origin;
        float dist = delta.magnitude;
        if (dist < 0.05f)
            return true;
        Vector3 dir = delta / dist;

        int count = radius > 0.001f
            ? Physics.SphereCastNonAlloc(origin, radius, dir, Hits, dist, ~0, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, dir, Hits, dist, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        Collider blocker = null;
        for (int i = 0; i < count; i++)
        {
            Collider col = Hits[i].collider;
            if (col == null || ShouldPassThrough(col, self))
                continue;
            if (Hits[i].distance < nearest)
            {
                nearest = Hits[i].distance;
                blocker = col;
            }
        }

        if (blocker == null)
            return true;
        return targetRoot != null && blocker.transform.IsChildOf(targetRoot);
    }

    static bool ShouldPassThrough(Collider col, Transform self)
    {
        if (self != null && col.transform.IsChildOf(self))
            return true;
        if (IsEnemy(col))
            return true;
        return DamageUtility.ProjectileShouldIgnore(col);
    }

    /// <summary>
    /// Ближайшая опора ПОД точкой. Луч стартует чуть выше pos, а не с +8 м:
    /// иначе враг у стены «находит землю» на крыше или зубце и телепортируется туда.
    /// </summary>
    public static float GroundBelow(Vector3 pos, float probeUp, Transform self, float fallback)
    {
        Vector3 origin = pos + Vector3.up * probeUp;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, GroundProbeDepth, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        float groundY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider col = Hits[i].collider;
            if (col == null || ShouldPassThrough(col, self) || IsPlayer(col))
                continue;
            if (Hits[i].distance < nearest)
            {
                nearest = Hits[i].distance;
                groundY = Hits[i].point.y;
            }
        }

        if (groundY > float.NegativeInfinity)
            return groundY;

        var terrain = Terrain.activeTerrain;
        if (terrain != null)
            return terrain.SampleHeight(pos) + terrain.transform.position.y;
        return fallback;
    }

    /// <summary>
    /// Pivot каркасного замка стоит в SW-колонне, а не в центре зала,
    /// поэтому центр считаем по клетке мага (0,0,0).
    /// </summary>
    public static Vector3 CastleCenter(Transform castle)
    {
        if (castle == null)
            return Vector3.zero;
        if (castle.GetComponent<CarcassCastle>() != null)
            return castle.TransformPoint(CarcassMetrics.CellCenterLocal(0, 0, 0));
        return castle.position;
    }

    public static bool IsInsideCastle(Transform castle, Vector3 worldPos)
    {
        if (castle == null)
            return false;
        Vector3 local = castle.InverseTransformPoint(worldPos) - CarcassMetrics.CellCenterLocal(0, 0, 0);
        float half = CarcassMetrics.FloorSize * 0.5f + CastleFootprintMargin;
        return Mathf.Abs(local.x) <= half && Mathf.Abs(local.z) <= half;
    }

    public static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
