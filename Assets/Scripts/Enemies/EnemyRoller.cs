using UnityEngine;

/// <summary>
/// Ground boulder: roll in, then pounce along an arc into the player or a wall.
/// </summary>
public sealed class EnemyRoller : MonoBehaviour, IDamageable
{
    const float FullScale = 1.15f;
    const float MinGrowScale = 0.1f;
    const float RollSpeed = 6.2f;
    const float PounceMaxTravel = 6.5f;
    const float PounceHeight = 2.15f;
    const float PounceMinDuration = 0.42f;
    const float PounceMaxDuration = 0.62f;
    const float RecoverDuration = 0.45f;
    const float PlayerEngage = 6.5f;
    const float WallEngage = 4.2f;
    const float PlayerPreferRange = 32f;
    const float HomeYardRadius = 16f;
    const float ExitTimeout = 4f;
    const float PlayerDamage = 10f;
    const int WallDamage = 4;

    [SerializeField] float _maxHealth = 20f;
    [SerializeField] Color _color = new Color(0.82f, 0.42f, 0.12f);

    enum Phase
    {
        Growing,
        Exit,
        Hunt,
        Pounce,
        Recover
    }

    float _health;
    float _growDuration;
    float _growElapsed;
    float _orbitSign = 1f;
    float _phaseElapsed;
    float _stuckTime;
    float _lastTargetDist = 999f;
    bool _dead;
    bool _released;
    bool _hitThisDash;
    Phase _phase;
    Vector3 _pounceFrom;
    Vector3 _pounceTo;
    float _pounceDuration;
    Transform _player;
    SquareCastle _playerCastle;
    SquareCastle _homeCastle;
    CastleWallChunk _siegeChunk;
    SphereCollider _sphere;
    Rigidbody _body;
    Collider _selfCol;

    public bool IsGrowing => !_released && !_dead;

    public static EnemyRoller SpawnInSocket(Transform socket, float growDuration)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyRoller";
        if (socket != null)
        {
            go.transform.SetParent(socket, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }

        go.transform.localScale = Vector3.one * MinGrowScale;

        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var unit = go.AddComponent<EnemyRoller>();
        unit._growDuration = Mathf.Max(0.2f, growDuration);
        unit._body = body;
        unit._sphere = go.GetComponent<SphereCollider>();
        unit._selfCol = unit._sphere;
        unit._homeCastle = socket != null ? socket.GetComponentInParent<SquareCastle>() : null;
        unit.ApplyColor();
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
        CacheRefs();
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

        CacheRefs();
        SpinByVelocity();
    }

    void FixedUpdate()
    {
        if (_dead || !_released || _body == null)
            return;
        CacheRefs();

        switch (_phase)
        {
            case Phase.Exit:
                TickExit();
                break;
            case Phase.Hunt:
                TickHunt();
                break;
            case Phase.Pounce:
                TickPounce();
                break;
            case Phase.Recover:
                TickRecover();
                break;
        }
    }

    void TickGrow()
    {
        _growElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_growElapsed / Mathf.Max(0.05f, _growDuration));
        transform.localScale = Vector3.one * Mathf.Lerp(MinGrowScale, FullScale, t);
        if (t >= 1f)
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
        transform.localScale = Vector3.one * FullScale;
        _phase = PlayerInsideHome() || _homeCastle == null ? Phase.Hunt : Phase.Exit;
        _phaseElapsed = 0f;
        _stuckTime = 0f;

        _body.constraints = RigidbodyConstraints.FreezeRotation;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 1.2f;
        _body.angularDamping = 1.2f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.maxDepenetrationVelocity = 2f;
        _body.interpolation = RigidbodyInterpolation.Interpolate;

        Vector3 toDoor = Flatten(DoorExitPoint() - transform.position);
        if (toDoor.sqrMagnitude < 0.01f)
            toDoor = Flatten(transform.forward);
        toDoor.Normalize();
        transform.position += toDoor * 0.4f;
        SnapOntoGround();
        _body.linearVelocity = toDoor * RollSpeed;
    }

    void TickExit()
    {
        if (PlayerInsideHome())
        {
            BeginHunt();
            return;
        }

        Vector3 target = DoorExitPoint();
        RollToward(target);
        _phaseElapsed += Time.fixedDeltaTime;

        bool outside = PlanarDistance(_body.position, _homeCastle != null
            ? _homeCastle.transform.position
            : _body.position) > 10f;
        if (PlanarDistance(_body.position, target) <= 2.4f || outside || _phaseElapsed >= ExitTimeout)
        {
            if (_phaseElapsed >= ExitTimeout)
            {
                _body.position = target;
                SnapOntoGround();
            }

            BeginHunt();
        }
    }

    void BeginHunt()
    {
        _phase = Phase.Hunt;
        _phaseElapsed = 0f;
        _stuckTime = 0f;
        _lastTargetDist = 999f;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 1.2f;
    }

    void TickHunt()
    {
        Vector3 target = HuntPoint();
        float dist = PlanarDistance(_body.position, target);
        float engage = WantsPlayer() ? PlayerEngage : WallEngage;
        if (dist <= engage)
        {
            _stuckTime = 0f;
            StartPounce();
            return;
        }

        if (dist + 0.4f < _lastTargetDist)
        {
            _lastTargetDist = dist;
            _stuckTime = 0f;
        }
        else
            _stuckTime += Time.fixedDeltaTime;

        if (_stuckTime > 1.4f)
        {
            _stuckTime = 0f;
            _orbitSign *= -1f;
            _lastTargetDist = dist;
        }

        RollToward(target);
    }

    void StartPounce()
    {
        Vector3 aim = HuntPoint();
        Vector3 planar = Flatten(aim - _body.position);
        float dist = planar.magnitude;
        Vector3 dir = dist > 0.05f ? planar / dist : Flatten(transform.forward);
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector3.forward;
        dist = Mathf.Clamp(dist, 1.4f, PounceMaxTravel);

        _pounceFrom = _body.position;
        _pounceTo = _pounceFrom + dir * dist;
        _pounceTo.y = GroundY(_pounceTo) + ScaledRadius();
        _pounceDuration = Mathf.Lerp(PounceMinDuration, PounceMaxDuration, dist / PounceMaxTravel);
        _phaseElapsed = 0f;
        _hitThisDash = false;
        _phase = Phase.Pounce;
        // Unity 6 refuses velocity writes on kinematic bodies — zero while still dynamic.
        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;
        _body.isKinematic = true;
        _body.useGravity = false;
    }

    void TickPounce()
    {
        _phaseElapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_phaseElapsed / Mathf.Max(0.05f, _pounceDuration));
        float u = t * t * (3f - 2f * t);
        Vector3 p = Vector3.Lerp(_pounceFrom, _pounceTo, u);
        p.y = Mathf.Lerp(_pounceFrom.y, _pounceTo.y, u) + 4f * PounceHeight * t * (1f - t);
        _body.MovePosition(p);
        TryPounceHits();
        if (t < 1f)
            return;
        StartRecover();
    }

    void StartRecover()
    {
        _phase = Phase.Recover;
        _phaseElapsed = 0f;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 8f;
        Vector3 v = Flatten(_body.linearVelocity) * 0.05f;
        v.y = 0f;
        _body.linearVelocity = v;
        SnapOntoGround();
    }

    void TickRecover()
    {
        _phaseElapsed += Time.fixedDeltaTime;
        Vector3 v = _body.linearVelocity;
        v.x *= 0.75f;
        v.z *= 0.75f;
        if (Flatten(v).magnitude < 0.4f)
        {
            v.x = 0f;
            v.z = 0f;
        }

        float maxY = GroundY(_body.position) + ScaledRadius() + 0.35f;
        if (_body.position.y > maxY)
        {
            Vector3 p = _body.position;
            p.y = maxY;
            _body.position = p;
            v.y = Mathf.Min(v.y, 0f);
        }

        _body.linearVelocity = v;
        if (_phaseElapsed < RecoverDuration)
            return;
        if (_body.position.y > GroundY(_body.position) + ScaledRadius() + 0.25f)
            return;

        _body.linearDamping = 1.2f;
        _phase = Phase.Hunt;
        _lastTargetDist = 999f;
    }

    void RollToward(Vector3 target)
    {
        Vector3 planar = Flatten(target - _body.position);
        Vector3 desired = planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.zero;
        desired = Steer(desired);

        Vector3 v = desired * RollSpeed;
        float ground = GroundY(_body.position) + ScaledRadius();
        float y = _body.position.y;
        if (y > ground + 0.2f)
            v.y = Mathf.Min(_body.linearVelocity.y, -3f);
        else
            v.y = _body.linearVelocity.y;
        _body.linearVelocity = v;

        if (y < ground)
        {
            Vector3 p = _body.position;
            p.y = ground;
            _body.position = p;
        }
    }

    Vector3 Steer(Vector3 desired)
    {
        if (desired.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        float radius = Mathf.Max(0.12f, ScaledRadius() * 0.8f);
        if (!Physics.SphereCast(_body.position, radius, desired, out RaycastHit hit, 1.15f, ~0, QueryTriggerInteraction.Ignore))
            return desired;
        if (ShouldIgnoreSteer(hit.collider))
            return desired;

        Vector3 slide = Vector3.ProjectOnPlane(desired, hit.normal);
        slide.y = 0f;
        if (slide.sqrMagnitude < 0.04f)
            slide = Vector3.Cross(hit.normal, Vector3.up) * _orbitSign;
        slide.y = 0f;
        if (slide.sqrMagnitude < 0.001f)
            return desired;

        Vector3 bias = Flatten(HuntPoint() - _body.position);
        if (_phase == Phase.Exit)
            bias = Flatten(DoorExitPoint() - _body.position);
        if (bias.sqrMagnitude > 0.01f)
            slide = (slide.normalized * 0.65f + bias.normalized * 0.35f);
        return slide.normalized;
    }

    void TryPounceHits()
    {
        if (_hitThisDash)
            return;

        if (WantsPlayer() && PlanarDistance(_body.position, _player.position) <= ScaledRadius() + 0.75f
            && Mathf.Abs(_body.position.y - _player.position.y) < 2.2f)
        {
            HitPlayer();
            return;
        }

        float radius = ScaledRadius() * 1.25f;
        var hits = Physics.OverlapSphere(_body.position, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null || IsSelfOrAlly(col))
                continue;
            if (IsPlayer(col))
            {
                HitPlayer();
                return;
            }

            var chunk = col.GetComponentInParent<CastleWallChunk>();
            if (chunk == null || chunk.IsDetached)
                continue;
            var castle = chunk.GetComponentInParent<SquareCastle>();
            if (castle == null || !castle.IsPlayerOwned)
                continue;
            chunk.ApplyHit(WallDamage, _body.position, -PounceDir(), fromPlayer: false);
            _hitThisDash = true;
            return;
        }
    }

    void HitPlayer()
    {
        var health = _player != null ? _player.GetComponentInParent<PlayerHealth>() : null;
        if (health != null)
        {
            Vector3 n = Flatten(_player.position - _body.position);
            if (n.sqrMagnitude < 0.01f)
                n = PounceDir();
            health.ApplyDamage(PlayerDamage, new DamageInfo(_body.position, n.normalized, PounceDir(), fromPlayer: false));
        }

        _hitThisDash = true;
    }

    Vector3 PounceDir()
    {
        Vector3 d = Flatten(_pounceTo - _pounceFrom);
        return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
    }

    Vector3 HuntPoint()
    {
        if (WantsPlayer())
            return _player.position;

        RefreshSiegeChunk();
        if (_siegeChunk != null && !_siegeChunk.IsDetached)
            return _siegeChunk.transform.position;
        if (_player != null)
            return _player.position;
        if (_playerCastle != null)
            return _playerCastle.transform.position;
        return _body.position;
    }

    bool WantsPlayer()
    {
        if (_player == null)
            return false;
        if (PlayerInsideHome())
            return true;
        if (_phase == Phase.Exit)
            return false;
        float dist = PlanarDistance(_body.position, _player.position);
        if (dist <= PlayerEngage + 2f)
            return true;
        if (dist > PlayerPreferRange)
            return false;
        return HasLineToPlayer();
    }

    bool PlayerInsideHome()
    {
        if (_player == null || _homeCastle == null)
            return false;
        return PlanarDistance(_player.position, _homeCastle.transform.position) <= HomeYardRadius;
    }

    bool HasLineToPlayer()
    {
        Vector3 origin = _body.position + Vector3.up * 0.4f;
        Vector3 target = _player.position + Vector3.up * 1.0f;
        Vector3 delta = target - origin;
        float dist = delta.magnitude;
        if (dist < 0.05f)
            return true;
        if (!Physics.Raycast(origin, delta / dist, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            return true;
        return IsPlayer(hit.collider);
    }

    void RefreshSiegeChunk()
    {
        _siegeChunk = null;
        if (_playerCastle == null)
            return;
        var chunks = _playerCastle.GetComponentsInChildren<CastleWallChunk>(true);
        float best = float.MaxValue;
        Vector3 origin = _body.position;
        for (int i = 0; i < chunks.Length; i++)
        {
            var c = chunks[i];
            if (c == null || c.IsDetached)
                continue;
            float d = PlanarDistance(c.transform.position, origin);
            if (d >= best)
                continue;
            best = d;
            _siegeChunk = c;
        }
    }

    Vector3 DoorExitPoint()
    {
        if (_homeCastle == null)
            return _body != null ? _body.position : transform.position;

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

        doorPos.y = GroundY(doorPos) + ScaledRadius();
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

    void SpinByVelocity()
    {
        Vector3 v = Flatten(_body.linearVelocity);
        float speed = v.magnitude;
        if (speed < 0.08f)
            return;
        Vector3 axis = Vector3.Cross(Vector3.up, v.normalized);
        if (axis.sqrMagnitude < 0.001f)
            return;
        transform.Rotate(axis, speed / ScaledRadius() * Mathf.Rad2Deg * Time.deltaTime, Space.World);
    }

    void SnapOntoGround()
    {
        Vector3 p = _body != null ? _body.position : transform.position;
        p.y = GroundY(p) + ScaledRadius();
        if (_body != null)
            _body.position = p;
        else
            transform.position = p;
    }

    float GroundY(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * 2.8f;
        var hits = Physics.SphereCastAll(origin, 0.14f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == _selfCol || IsSelfOrAlly(hit.collider) || IsPlayer(hit.collider))
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

    float ScaledRadius()
    {
        float r = _sphere != null ? _sphere.radius : 0.5f;
        return Mathf.Max(0.16f, r * FullScale);
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

    bool ShouldIgnoreSteer(Collider col)
    {
        if (col == null || IsSelfOrAlly(col) || IsPlayer(col) || IsMage(col))
            return true;
        if (col.GetComponent<TerrainCollider>() != null)
            return true;
        if (col.GetComponent<CastlePlatform>() != null)
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

    void CacheRefs()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            _player = go != null ? go.transform : null;
        }

        if (_playerCastle == null)
            _playerCastle = SquareCastle.FindPlayerOwned();
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

    bool IsPlayer(Collider col)
    {
        if (col == null || _player == null)
            return false;
        return col.transform == _player || col.transform.IsChildOf(_player)
               || col.GetComponentInParent<PlayerHealth>() != null
               || col.GetComponentInParent<CharacterController>() != null &&
                  col.GetComponentInParent<PlayerHealth>() != null;
    }

    bool IsSelfOrAlly(Collider col)
    {
        if (col == null)
            return true;
        if (col == _selfCol || col.transform == transform || col.transform.IsChildOf(transform))
            return true;
        return col.GetComponentInParent<EnemyRoller>() != null
               || col.GetComponentInParent<EnemyUnit>() != null;
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
