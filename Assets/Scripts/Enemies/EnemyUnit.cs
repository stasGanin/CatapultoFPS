using UnityEngine;

/// <summary>
/// Flying caster: grows in a socket, then fights the player, sieges player walls or guards home.
/// Shots are telegraphed, fired in bursts and only along a verified clear path.
/// </summary>
public sealed class EnemyUnit : MonoBehaviour, IDamageable
{
    const string ConfigPath = "Enemies/FlyerConfig";
    const float FullScale = 0.85f;
    const float MinGrowScale = 0.08f;
    const float Acceleration = 16f;
    const float SteerLookAhead = 1.8f;
    const float AltitudeGain = 2.5f;
    const float MaxClimbSpeed = 3f;
    const float GuardRadius = 9f;
    const float BoltRadius = 0.18f;
    const float StrafeFlipMin = 2.5f;
    const float StrafeFlipMax = 5f;
    const float CancelledShotCooldown = 0.6f;

    [SerializeField] Color _color = new Color(0.72f, 0.22f, 0.2f);

    enum AttackState { Idle, Charging, Bursting }

    static readonly Collider[] Neighbours = new Collider[12];

    EnemyConfig _config;
    EnemyTargeting _targeting;
    EnemyBodyView _view;
    float _health;
    float _growDuration;
    float _growElapsed;
    bool _released;
    bool _dead;
    Transform _spawnCastle;
    SphereCollider _sphere;
    Rigidbody _body;

    float _orbitSign = 1f;
    float _nextStrafeFlip;
    AttackState _attack;
    float _attackTimer;
    float _nextAttack;
    int _shotsLeft;
    bool _attackOnPlayer;

    public bool IsGrowing => !_released && !_dead;

    public static EnemyUnit SpawnInSocket(Transform socket, float growDuration)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Enemy";
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

        var unit = go.AddComponent<EnemyUnit>();
        unit._growDuration = Mathf.Max(0.2f, growDuration);
        unit._spawnCastle = EnemyNav.HomeCastleOf(socket);
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
        _config = EnemyConfig.Load(ConfigPath);
        _targeting = new EnemyTargeting(_config, transform);
        _health = _config.MaxHealth;
        _body = GetComponent<Rigidbody>();
        _sphere = GetComponent<SphereCollider>();
        _orbitSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
    }

    void OnDestroy()
    {
        EnemyAttackTokens.Release(this);
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

        if (_targeting.Tick(_body.position))
            EnemyNav.IgnoreCastle(_sphere, _targeting.HomeCastle, false);
        TickAttack(Time.deltaTime);
        _view.Tick(AttackCharge());
    }

    void FixedUpdate()
    {
        if (_dead || !_released)
            return;

        Vector3 desired = _targeting.Current switch
        {
            EnemyTargeting.Goal.Exit => DirectionTo(EnemyNav.DoorExitPoint(_targeting.HomeCastle, _body.position)),
            EnemyTargeting.Goal.Engage => EngageMove(),
            EnemyTargeting.Goal.Siege => SiegeMove(),
            _ => GuardMove()
        };

        // Во время замаха и очереди стрелок замирает — это и есть телеграф для игрока.
        if (_attack != AttackState.Idle)
            desired = Vector3.zero;

        desired = Separate(desired);
        desired = Steer(desired);

        Vector3 planarTarget = EnemySenses.Flatten(desired) * _config.MoveSpeed;
        Vector3 planar = Vector3.MoveTowards(
            EnemySenses.Flatten(_body.linearVelocity), planarTarget, Acceleration * Time.fixedDeltaTime);
        float climb = Mathf.Clamp((TargetAltitude() - _body.position.y) * AltitudeGain, -MaxClimbSpeed, MaxClimbSpeed);
        _body.linearVelocity = new Vector3(planar.x, climb, planar.z);

        FaceTowards(CurrentLookPoint());
    }

    // ---------- movement ----------

    Vector3 EngageMove()
    {
        if (!_targeting.SeesPlayer)
            return DirectionTo(_targeting.LastSeenPos);

        Vector3 to = EnemySenses.Flatten(EnemySenses.PlayerAimPoint() - _body.position);
        float dist = to.magnitude;
        Vector3 toDir = dist > 0.01f ? to / dist : transform.forward;
        float standoff = _config.StandoffDistance;

        if (dist > standoff + 1.5f)
            return toDir;
        if (dist < standoff - 1.5f)
            return -toDir;

        if (Time.time >= _nextStrafeFlip)
        {
            _nextStrafeFlip = Time.time + Random.Range(StrafeFlipMin, StrafeFlipMax);
            _orbitSign = -_orbitSign;
        }

        return Vector3.Cross(Vector3.up, toDir) * (_orbitSign * 0.6f);
    }

    Vector3 SiegeMove()
    {
        CarcassWallBreakable wall = _targeting.SiegeWall;
        if (wall == null)
            return Vector3.zero;
        Vector3 aim = wall.AimPoint;
        float hold = Mathf.Min(_config.StandoffDistance, _config.AttackRange * 0.6f);
        if (Vector3.Distance(_body.position, aim) <= hold
            && EnemySenses.HasClearPath(_body.position, aim, BoltRadius, transform, wall.transform))
            return Vector3.zero;
        return DirectionTo(aim);
    }

    Vector3 GuardMove()
    {
        Vector3 offset = EnemySenses.Flatten(_body.position - _targeting.HomeCenter);
        if (offset.sqrMagnitude < 0.01f)
            offset = Vector3.forward;
        Vector3 radial = offset.normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial) * _orbitSign;
        float error = offset.magnitude - GuardRadius;
        return (tangent * 0.5f - radial * Mathf.Clamp(error * 0.2f, -1f, 1f)).normalized * 0.6f;
    }

    float TargetAltitude()
    {
        float ground = EnemySenses.GroundBelow(_body.position, 0.3f, transform, _body.position.y - _config.HoverHeight);
        return ground + _config.HoverHeight;
    }

    Vector3 Steer(Vector3 desired)
    {
        Vector3 flat = EnemySenses.Flatten(desired);
        if (flat.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 dir = flat.normalized;
        if (!Physics.SphereCast(_body.position, CastRadius(), dir, out RaycastHit hit, SteerLookAhead, ~0, QueryTriggerInteraction.Ignore))
            return desired;
        Collider col = hit.collider;
        if (col.transform.IsChildOf(transform) || EnemySenses.IsEnemy(col) || EnemySenses.IsPlayer(col))
            return desired;
        if (_targeting.Current == EnemyTargeting.Goal.Exit && _targeting.HomeCastle != null
            && col.transform.IsChildOf(_targeting.HomeCastle))
            return desired;

        // Скользим вдоль препятствия в сторону цели, без телепортов и рывков вверх.
        Vector3 slide = Vector3.ProjectOnPlane(dir, hit.normal);
        slide.y = 0f;
        if (slide.sqrMagnitude < 0.04f)
            slide = Vector3.Cross(Vector3.up, hit.normal) * _orbitSign;
        return slide.normalized * flat.magnitude;
    }

    Vector3 Separate(Vector3 desired)
    {
        int count = Physics.OverlapSphereNonAlloc(_body.position, CastRadius() * 3f, Neighbours, ~0, QueryTriggerInteraction.Ignore);
        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider col = Neighbours[i];
            if (col == null || col.transform.IsChildOf(transform) || !EnemySenses.IsEnemy(col))
                continue;
            Vector3 away = EnemySenses.Flatten(_body.position - col.bounds.center);
            if (away.sqrMagnitude > 0.0001f)
                push += away.normalized;
        }

        return push.sqrMagnitude > 0.0001f ? desired + push.normalized * 0.7f : desired;
    }

    // ---------- attack ----------

    void TickAttack(float dt)
    {
        switch (_attack)
        {
            case AttackState.Idle:
                TryBeginAttack();
                break;
            case AttackState.Charging:
                _attackTimer -= dt;
                if (_attackTimer <= 0f)
                {
                    _attack = AttackState.Bursting;
                    _shotsLeft = _config.BurstCount;
                    _attackTimer = 0f;
                }
                break;
            case AttackState.Bursting:
                _attackTimer -= dt;
                if (_attackTimer > 0f)
                    break;
                if (!FireShot())
                {
                    EndAttack(CancelledShotCooldown);
                    break;
                }

                _shotsLeft--;
                _attackTimer = _config.BurstInterval;
                if (_shotsLeft <= 0)
                    EndAttack(_config.RollCooldown());
                break;
        }
    }

    void TryBeginAttack()
    {
        if (Time.time < _nextAttack || _targeting.Current == EnemyTargeting.Goal.Exit)
            return;
        if (EnemySenses.IsInsideCastle(_targeting.HomeCastle, _body.position))
            return;

        if (_targeting.Current == EnemyTargeting.Goal.Engage && _targeting.SeesPlayer)
        {
            if (Vector3.Distance(_body.position, EnemySenses.PlayerAimPoint()) > _config.AttackRange)
                return;
            if (!EnemyAttackTokens.TryAcquire(this))
                return;
            _attackOnPlayer = true;
        }
        else if (_targeting.Current == EnemyTargeting.Goal.Siege && _targeting.SiegeWall != null)
        {
            CarcassWallBreakable wall = _targeting.SiegeWall;
            if (Vector3.Distance(_body.position, wall.AimPoint) > _config.AttackRange)
                return;
            if (!EnemySenses.HasClearPath(_body.position, wall.AimPoint, BoltRadius, transform, wall.transform))
                return;
            _attackOnPlayer = false;
        }
        else
        {
            return;
        }

        _attack = AttackState.Charging;
        _attackTimer = _config.TelegraphTime;
    }

    /// <summary>Returns false when the shot is no longer clean — the burst is then cancelled.</summary>
    bool FireShot()
    {
        Transform targetRoot;
        Vector3 aim;
        if (_attackOnPlayer)
        {
            targetRoot = EnemySenses.Player;
            if (targetRoot == null)
                return false;
            aim = EnemySenses.PlayerAimPoint();
            float travel = Vector3.Distance(_body.position, aim) / Mathf.Max(1f, _config.ProjectileSpeed);
            aim += EnemySenses.PlayerVelocity() * (travel * _config.LeadFactor);
        }
        else
        {
            CarcassWallBreakable wall = _targeting.SiegeWall;
            if (wall == null || wall.IsBreached)
                return false;
            targetRoot = wall.transform;
            aim = wall.AimPoint;
        }

        Vector3 dir = (aim - _body.position).normalized;
        // Дуло — по направлению выстрела, а не по forward, который доворачивается с задержкой.
        Vector3 muzzle = _body.position + dir * (CastRadius() + BoltRadius + 0.1f);
        if (!EnemySenses.HasClearPath(muzzle, aim, BoltRadius, transform, targetRoot))
            return false;

        float damage = _attackOnPlayer ? _config.Damage : _config.SiegeDamage;
        EnemyProjectile.Spawn(muzzle, dir, _config.ProjectileSpeed, damage, gameObject, homeOnPlayer: _attackOnPlayer);
        return true;
    }

    void EndAttack(float cooldown)
    {
        _attack = AttackState.Idle;
        _nextAttack = Time.time + Mathf.Max(0.1f, cooldown);
        EnemyAttackTokens.Release(this);
    }

    float AttackCharge()
    {
        if (_attack == AttackState.Bursting)
            return 1f;
        if (_attack == AttackState.Charging)
            return 1f - _attackTimer / Mathf.Max(0.01f, _config.TelegraphTime);
        return 0f;
    }

    // ---------- lifecycle ----------

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

        if (_spawnCastle == null)
            _spawnCastle = EnemyNav.HomeCastleOf(transform);
        transform.SetParent(null, true);
        _view = new EnemyBodyView(transform, _color, Vector3.one * FullScale);

        // Спавнеры каркаса стоят снаружи стен — выход через дверь нужен, только если родились внутри.
        if (_targeting.Begin(_spawnCastle, transform.position))
            EnemyNav.IgnoreCastle(_sphere, _spawnCastle, true);

        _body.constraints = RigidbodyConstraints.FreezeRotation;
        _body.isKinematic = false;
        _body.useGravity = false;
        _body.linearDamping = 0f;
        _body.angularDamping = 4f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.maxDepenetrationVelocity = 4f;
        _body.interpolation = RigidbodyInterpolation.Interpolate;

        _nextAttack = Time.time + _config.RollCooldown() * 0.5f;
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_dead || amount <= 0f || !info.FromPlayer)
            return;

        _health -= amount;
        _view?.Flash();
        _targeting.NotifyDamaged();
        if (_health > 0f)
            return;

        _dead = true;
        LootDrop.Enemy(transform.position);
        Destroy(gameObject);
    }

    // ---------- helpers ----------

    Vector3 CurrentLookPoint()
    {
        if (_targeting.Current == EnemyTargeting.Goal.Engage)
            return _targeting.SeesPlayer ? EnemySenses.PlayerAimPoint() : _targeting.LastSeenPos;
        if (_targeting.Current == EnemyTargeting.Goal.Siege && _targeting.SiegeWall != null)
            return _targeting.SiegeWall.AimPoint;
        return _body.position + _body.linearVelocity;
    }

    void FaceTowards(Vector3 point)
    {
        Vector3 planar = EnemySenses.Flatten(point - transform.position);
        if (planar.sqrMagnitude < 0.001f)
            return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(planar.normalized, Vector3.up),
            1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
    }

    Vector3 DirectionTo(Vector3 point)
    {
        Vector3 flat = EnemySenses.Flatten(point - _body.position);
        return flat.sqrMagnitude > 0.04f ? flat.normalized : Vector3.zero;
    }

    float CastRadius()
    {
        float r = _sphere != null ? _sphere.radius : 0.5f;
        return Mathf.Max(0.12f, r * FullScale * 0.9f);
    }
}
