using UnityEngine;

/// <summary>
/// Flying sphere: grows in a socket, leaves via the door, then hunts and shoots only with LOS.
/// </summary>
public sealed class EnemyUnit : MonoBehaviour, IDamageable
{
    const float FullScale = 0.85f;
    const float MinGrowScale = 0.08f;
    const float HomeYardRadius = 16f;
    const float ExitTimeout = 4f;
    const float HoverMin = 1.15f;
    const float HoverMax = 2.45f;
    const float ExitHoverMax = 4.8f;

    [SerializeField] float _maxHealth = 20f;
    [SerializeField] float _fireInterval = 1.1f;
    [SerializeField] float _projectileSpeed = 10f;
    [SerializeField] float _projectileDamage = 1f;
    [SerializeField] float _aimHeight = 1.1f;
    [SerializeField] float _range = 28f;
    [SerializeField] float _flySpeed = 6f;
    [SerializeField] float _standoffDistance = 10f;
    [SerializeField] float _hoverHeight = 1.5f;
    [SerializeField] Color _color = new Color(0.72f, 0.22f, 0.2f);

    enum ExitPhase
    {
        Exit = 0,
        Hunt = 1
    }

    float _health;
    float _nextFire;
    float _growDuration;
    float _growElapsed;
    float _orbitSign = 1f;
    float _phaseElapsed;
    Transform _player;
    SquareCastle _playerCastle;
    SquareCastle _homeCastle;
    CastleWallChunk _siegeChunk;
    SphereCollider _sphere;
    Collider _selfCol;
    Rigidbody _body;
    bool _dead;
    bool _released;
    ExitPhase _exitPhase;

    public bool IsGrowing => !_released && !_dead;

    public static EnemyUnit SpawnInSocket(Transform socket, float growDuration)
    {
        Vector3 pos = socket != null ? socket.position : Vector3.zero;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Enemy";
        if (socket != null)
        {
            go.transform.SetParent(socket, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }
        else
        {
            go.transform.position = pos;
        }

        go.transform.localScale = Vector3.one * MinGrowScale;

        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var unit = go.AddComponent<EnemyUnit>();
        unit._growDuration = Mathf.Max(0.2f, growDuration);
        unit._body = body;
        unit._sphere = go.GetComponent<SphereCollider>();
        unit._selfCol = unit._sphere;
        unit._homeCastle = socket != null ? socket.GetComponentInParent<SquareCastle>() : null;
        unit.ApplyColor();
        unit.IgnoreHomeObstacles(true);
        return unit;
    }

    public static EnemyUnit Spawn(Vector3 position)
    {
        var unit = SpawnInSocket(null, 0.05f);
        unit.transform.position = position;
        unit._growElapsed = unit._growDuration;
        unit.Release();
        return unit;
    }

    void Awake()
    {
        _health = _maxHealth;
        if (_body == null)
            _body = GetComponent<Rigidbody>();
        if (_sphere == null)
            _sphere = GetComponent<SphereCollider>();
        if (_selfCol == null)
            _selfCol = GetComponent<Collider>();
        _orbitSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        CachePlayer();
        CachePlayerCastle();
        ApplyColor();
    }

    void Update()
    {
        if (_dead)
            return;

        if (!_released)
        {
            TickGrow();
            return;
        }

        if (_player == null)
        {
            CachePlayer();
            if (_player == null)
                return;
        }

        FaceTarget();
        TryShoot();
    }

    void FixedUpdate()
    {
        if (_dead || !_released || _body == null)
            return;
        if (_player == null)
            CachePlayer();
        CachePlayerCastle();

        if (_exitPhase != ExitPhase.Hunt)
        {
            TickExit();
            return;
        }

        bool fightPlayer = CanFightPlayer();
        Vector3 target = fightPlayer ? AimPoint() : SiegePoint();
        Vector3 toTarget = target - _body.position;
        float dist = toTarget.magnitude;
        Vector3 desired = toTarget / Mathf.Max(dist, 0.001f);
        float hold = fightPlayer ? _standoffDistance : 3.2f;

        bool canSee = fightPlayer || HasLineOfSight(target, _siegeChunk);
        if (!canSee)
        {
            Vector3 side = Vector3.Cross(Vector3.up, desired);
            if (side.sqrMagnitude < 0.001f)
                side = transform.right;
            desired = (desired + side.normalized * (0.7f * _orbitSign)).normalized;
        }
        else if (dist < hold)
        {
            float holdY = target.y + (fightPlayer ? _hoverHeight : 1.4f);
            Vector3 away = _body.position - target;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            desired = (away.normalized * 0.35f + Vector3.up * (holdY - _body.position.y)).normalized;
            if (Mathf.Abs(holdY - _body.position.y) < 0.25f && dist > hold * 0.7f)
                desired = Vector3.zero;
        }
        else
        {
            desired.y += ((fightPlayer ? _hoverHeight : 1.4f) - (_body.position.y - target.y)) * 0.15f;
            desired.Normalize();
        }

        desired = BlendSeparation(desired);
        desired = SteerAroundWalls(desired);
        desired = ClampAltitude(desired, HoverMax);
        _body.linearVelocity = desired * _flySpeed;
    }

    void TickExit()
    {
        if (PlayerInsideHome())
        {
            BeginHunt();
            return;
        }

        Vector3 target = DoorExitPoint();
        Vector3 to = target - _body.position;
        float dist = to.magnitude;
        Vector3 desired = dist > 0.001f ? to / dist : Vector3.zero;
        desired = BlendSeparation(desired);
        desired = SteerAroundWalls(desired);
        desired = ClampAltitude(desired, ExitHoverMax);
        _body.linearVelocity = desired * _flySpeed;

        _phaseElapsed += Time.fixedDeltaTime;
        bool outside = _homeCastle != null
            && PlanarDistance(_body.position, _homeCastle.transform.position) > 10f;
        if (dist <= 1.8f || outside || _phaseElapsed >= ExitTimeout)
        {
            if (_phaseElapsed >= ExitTimeout)
            {
                _body.position = target;
                HoldHover();
            }

            BeginHunt();
        }
    }

    void BeginHunt()
    {
        _exitPhase = ExitPhase.Hunt;
        _phaseElapsed = 0f;
    }

    Vector3 SteerAroundWalls(Vector3 desired)
    {
        if (desired.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        float radius = CastRadius();
        float look = 1.45f;
        Vector3 origin = _body.position;
        if (!Physics.SphereCast(origin, radius, desired, out RaycastHit hit, look, ~0, QueryTriggerInteraction.Ignore))
            return desired.normalized;

        if (ShouldIgnoreSteer(hit.collider))
            return desired.normalized;

        if (hit.normal.y >= 0.35f)
        {
            desired.y = Mathf.Max(desired.y, 0.55f);
            return desired.normalized;
        }

        Vector3 along = Vector3.Cross(Vector3.up, hit.normal);
        if (along.sqrMagnitude < 0.01f)
            along = transform.right * _orbitSign;
        along.Normalize();
        if (Vector3.Dot(along, desired) < 0f)
            along = -along;

        Vector3 climb = (Vector3.up * 0.7f + along * 0.35f + hit.normal * 0.35f + Flatten(desired) * 0.2f);
        if (climb.sqrMagnitude < 0.001f)
            climb = Vector3.up;
        return climb.normalized;
    }

    bool HasLineOfSight(Vector3 target, Component allowed = null)
    {
        Vector3 origin = _body != null ? _body.position : transform.position;
        Vector3 delta = target - origin;
        float dist = delta.magnitude;
        if (dist < 0.05f)
            return true;

        Vector3 dir = delta / dist;
        origin += dir * (CastRadius() + 0.08f);

        for (int i = 0; i < 6; i++)
        {
            dist = Vector3.Distance(origin, target);
            if (dist < 0.05f)
                return true;
            dir = (target - origin) / dist;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
                return true;
            if (IsPlayer(hit.collider))
                return true;
            if (allowed != null &&
                (hit.collider.transform == allowed.transform
                 || hit.collider.transform.IsChildOf(allowed.transform)
                 || hit.collider.GetComponentInParent<CastleWallChunk>() == allowed))
                return true;
            if (IsOwnOrAlly(hit.collider) || IsGroundCollider(hit.collider) || IsMage(hit.collider))
            {
                origin = hit.point + dir * 0.08f;
                continue;
            }

            return false;
        }

        return false;
    }

    void FaceTarget()
    {
        Vector3 look = _exitPhase != ExitPhase.Hunt
            ? DoorExitPoint()
            : (CanFightPlayer() ? AimPoint() : SiegePoint());
        Vector3 planar = look - transform.position;
        planar.y = 0f;
        if (planar.sqrMagnitude < 0.001f)
            return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(planar.normalized, Vector3.up),
            1f - Mathf.Exp(-8f * Time.deltaTime));
    }

    void TryShoot()
    {
        if (Time.time < _nextFire)
            return;
        if (_exitPhase != ExitPhase.Hunt)
            return;
        if (!CanFightPlayer())
            return;

        Vector3 target = AimPoint();
        float radius = FullScale * 0.5f;
        Vector3 muzzle = transform.position + transform.forward * (radius + 0.2f);
        _nextFire = Time.time + _fireInterval;
        Vector3 dir = (target - muzzle).normalized;
        EnemyProjectile.Spawn(muzzle, dir, _projectileSpeed, _projectileDamage, gameObject, _player);
    }

    Vector3 AimPoint()
    {
        return _player != null ? _player.position + Vector3.up * _aimHeight : transform.position;
    }

    bool CanFightPlayer()
    {
        if (_player == null)
            return false;
        if (_exitPhase != ExitPhase.Hunt)
            return false;
        Vector3 target = AimPoint();
        if (Vector3.Distance(transform.position, target) > _range)
            return false;
        return HasLineOfSight(target);
    }

    Vector3 SiegePoint()
    {
        RefreshSiegeChunk();
        if (_siegeChunk != null && !_siegeChunk.IsDetached)
            return _siegeChunk.transform.position;
        if (_playerCastle != null)
            return _playerCastle.transform.position + Vector3.up * 2f;
        return AimPoint();
    }

    void RefreshSiegeChunk()
    {
        if (_siegeChunk != null && !_siegeChunk.IsDetached)
            return;
        _siegeChunk = null;
        if (_playerCastle == null)
            return;

        var chunks = _playerCastle.GetComponentsInChildren<CastleWallChunk>(true);
        float best = float.MaxValue;
        Vector3 origin = transform.position;
        for (int i = 0; i < chunks.Length; i++)
        {
            var c = chunks[i];
            if (c == null || c.IsDetached)
                continue;
            float d = (c.transform.position - origin).sqrMagnitude;
            if (d < best)
            {
                best = d;
                _siegeChunk = c;
            }
        }
    }

    void CachePlayerCastle()
    {
        if (_playerCastle == null)
            _playerCastle = SquareCastle.FindPlayerOwned();
    }

    Vector3 DoorExitPoint()
    {
        if (_homeCastle == null)
        {
            Vector3 fallback = _body != null ? _body.position : transform.position;
            fallback.y = GroundY(fallback) + _hoverHeight;
            return fallback;
        }

        CastleModuleRoot door = FindHomeDoor();
        Vector3 center = _homeCastle.transform.position;
        Vector3 exitDir = Flatten(_homeCastle.transform.forward);
        Vector3 doorPos;

        if (door != null)
        {
            doorPos = door.transform.position;
            Vector3 away = Flatten(doorPos - center);
            if (away.sqrMagnitude < 0.01f)
                away = exitDir;
            else
                away.Normalize();
            exitDir = Vector3.Dot(Flatten(door.transform.forward), away) > 0f
                ? Flatten(door.transform.forward)
                : Flatten(-door.transform.forward);
            if (exitDir.sqrMagnitude < 0.01f)
                exitDir = away;
            doorPos += exitDir.normalized * 6f;
        }
        else
        {
            doorPos = _homeCastle.transform.TransformPoint(new Vector3(0f, 0f, 12f));
            exitDir = Flatten(doorPos - center);
        }

        doorPos += Flatten(Vector3.Cross(Vector3.up, exitDir)) * (_orbitSign * 1.4f);
        doorPos.y = GroundY(doorPos) + _hoverHeight;
        return doorPos;
    }

    CastleModuleRoot FindHomeDoor()
    {
        if (_homeCastle == null)
            return null;
        var modules = _homeCastle.GetComponentsInChildren<CastleModuleRoot>(true);
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != null && modules[i].Kind == CastleModuleKind.Door)
                return modules[i];
        }

        return null;
    }

    Vector3 ClampAltitude(Vector3 desired, float maxHover)
    {
        Vector3 pos = _body.position;
        float ground = GroundY(pos);
        float minY = ground + HoverMin;
        float maxY = ground + maxHover;

        if (pos.y < minY)
        {
            pos.y = minY;
            _body.position = pos;
            desired.y = Mathf.Max(desired.y, 0.55f);
        }
        else if (pos.y > maxY)
            desired.y = -0.65f;
        else
            desired.y = Mathf.Clamp(desired.y, -0.25f, 0.45f);

        if (desired.sqrMagnitude < 0.0001f)
            return Vector3.zero;
        return desired.normalized;
    }

    void HoldHover()
    {
        Vector3 p = _body.position;
        p.y = GroundY(p) + _hoverHeight;
        _body.position = p;
        _body.linearVelocity = Vector3.zero;
    }

    float GroundY(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * 2.8f;
        var hits = Physics.SphereCastAll(origin, 0.14f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == _selfCol || IsOwnOrAlly(hit.collider) || IsPlayer(hit.collider))
                continue;
            if (hit.normal.y < 0.35f)
                continue;
            if (hit.point.y > best)
                best = hit.point.y;
        }

        if (best > float.NegativeInfinity)
            return best;

        var terrain = Terrain.activeTerrain;
        if (terrain != null)
            return terrain.SampleHeight(pos) + terrain.transform.position.y;
        return pos.y;
    }

    float CastRadius()
    {
        float r = _sphere != null ? _sphere.radius : 0.5f;
        return Mathf.Max(0.12f, r * FullScale * 0.85f);
    }

    Vector3 BlendSeparation(Vector3 desired)
    {
        Vector3 push = Vector3.zero;
        int count = 0;
        var hits = Physics.OverlapSphere(_body.position, CastRadius() * 2.4f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null || col == _selfCol || !IsAllyUnit(col))
                continue;
            Vector3 delta = Flatten(_body.position - col.ClosestPointOnBounds(_body.position));
            if (delta.sqrMagnitude < 0.0001f)
                delta = Flatten(transform.right) * _orbitSign;
            push += delta.normalized;
            count++;
        }

        if (count == 0)
            return desired;
        Vector3 mixed = desired.sqrMagnitude > 0.0001f
            ? desired.normalized + push.normalized * 0.7f
            : push.normalized;
        return mixed.sqrMagnitude > 0.0001f ? mixed.normalized : desired;
    }

    bool IsPlayer(Collider col)
    {
        if (col == null || _player == null)
            return false;
        return col.transform == _player || col.transform.IsChildOf(_player)
               || col.GetComponentInParent<PlayerHealth>() != null;
    }

    bool IsOwnOrAlly(Collider col)
    {
        if (col == null)
            return true;
        if (col == _selfCol || col.transform == transform || col.transform.IsChildOf(transform))
            return true;
        return IsAllyUnit(col);
    }

    static bool IsAllyUnit(Collider col)
    {
        return col.GetComponentInParent<EnemyUnit>() != null
               || col.GetComponentInParent<EnemyRoller>() != null;
    }

    static bool IsGroundCollider(Collider col)
    {
        return col.GetComponent<TerrainCollider>() != null
               || col.GetComponent<CastlePlatform>() != null;
    }

    bool ShouldIgnoreSteer(Collider col)
    {
        if (col == null || col == _selfCol || IsPlayer(col) || IsMage(col))
            return true;
        if (IsGroundCollider(col))
            return true;
        if (_homeCastle != null && col.transform.IsChildOf(_homeCastle.transform))
            return true;
        return false;
    }

    static bool IsMage(Collider col)
    {
        if (col.GetComponentInParent<EnemyCastleMage>() != null)
            return true;
        Transform t = col.transform;
        while (t != null)
        {
            if (t.name == "MageHeart" || t.name == "EnemyMage")
                return true;
            t = t.parent;
        }

        return false;
    }

    void TickGrow()
    {
        _growElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_growElapsed / Mathf.Max(0.05f, _growDuration));
        transform.localScale = Vector3.one * Mathf.Lerp(MinGrowScale, FullScale, t);
        if (t < 1f)
            return;
        Release();
    }

    void Release()
    {
        if (_released)
            return;
        _released = true;

        _homeCastle = GetComponentInParent<SquareCastle>();
        IgnoreHomeObstacles(true);
        transform.SetParent(null, true);

        Vector3 inward = Flatten(
            (_homeCastle != null ? _homeCastle.transform.position : transform.position) - transform.position);
        if (inward.sqrMagnitude < 0.01f)
            inward = _homeCastle != null ? Flatten(-_homeCastle.transform.forward) : Flatten(transform.forward);
        inward.Normalize();
        transform.position += inward * 0.35f;
        transform.localScale = Vector3.one * FullScale;
        _exitPhase = PlayerInsideHome() || _homeCastle == null ? ExitPhase.Hunt : ExitPhase.Exit;
        _phaseElapsed = 0f;

        if (_body == null)
            _body = GetComponent<Rigidbody>();
        if (_body != null)
        {
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _body.isKinematic = false;
            _body.useGravity = false;
            _body.linearDamping = 1.8f;
            _body.angularDamping = 4f;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.maxDepenetrationVelocity = 8f;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            HoldHover();
            _body.linearVelocity = inward * (_flySpeed * 0.5f);
        }

        _nextFire = Time.time + 0.45f;
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_dead || amount <= 0f)
            return;

        _health -= amount;
        if (_health > 0f)
            return;

        _dead = true;
        LootDrop.Enemy(transform.position);
        Destroy(gameObject);
    }

    void IgnoreHomeObstacles(bool ignore)
    {
        if (_selfCol == null)
            _selfCol = GetComponent<Collider>();
        if (_selfCol == null)
            return;

        if (_homeCastle != null)
        {
            var cols = _homeCastle.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i] != _selfCol)
                    Physics.IgnoreCollision(_selfCol, cols[i], ignore);
            }
        }
    }

    bool PlayerInsideHome()
    {
        if (_player == null || _homeCastle == null)
            return false;
        return PlanarDistance(_player.position, _homeCastle.transform.position) <= HomeYardRadius;
    }

    void CachePlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        _player = go != null ? go.transform : null;
    }

    void ApplyColor()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", _color);
        block.SetColor("_Color", _color);
        renderer.SetPropertyBlock(block);
    }

    static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        return Flatten(a - b).magnitude;
    }
}
