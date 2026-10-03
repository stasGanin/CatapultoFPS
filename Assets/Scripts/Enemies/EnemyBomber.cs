using UnityEngine;

/// <summary>
/// Kiting bombardier: keeps a standoff and lobs ember bombs on a solved ballistic arc.
/// Throws only when the arc is clear, after a visible wind-up.
/// </summary>
public sealed class EnemyBomber : MonoBehaviour, IDamageable
{
    const string ConfigPath = "Enemies/BomberConfig";
    static readonly Vector3 BodyScale = new Vector3(0.7f, 0.9f, 0.7f);
    const float MinGrowScale = 0.12f;
    const float Acceleration = 18f;
    const float MinThrowDistance = 4f;
    const float BlastRadius = 2.4f;
    const float HandHeight = 1.2f;
    const float GuardRadius = 8f;
    const float StrafeFlipMin = 2f;
    const float StrafeFlipMax = 4f;
    const float FallRescueDepth = 2f;
    const float CancelledThrowCooldown = 0.6f;

    [SerializeField] Color _color = new Color(0.85f, 0.32f, 0.12f);

    EnemyConfig _config;
    EnemyTargeting _targeting;
    EnemyBodyView _view;
    float _health;
    float _growDuration = 1f;
    float _growElapsed;
    bool _dead;
    bool _released;
    Transform _spawnCastle;
    Rigidbody _body;
    CapsuleCollider _capsule;

    bool _windingUp;
    float _windUpTimer;
    float _nextAttack;
    float _orbitSign = 1f;
    float _nextStrafeFlip;

    public bool IsGrowing => !_released && !_dead;

    public static EnemyBomber SpawnInSocket(Transform socket, float growDuration)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "EnemyBomber";
        if (socket != null)
        {
            go.transform.SetParent(socket, false);
            go.transform.localPosition = Vector3.up * 0.15f;
        }

        go.transform.localScale = BodyScale * MinGrowScale;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var bomber = go.AddComponent<EnemyBomber>();
        bomber._growDuration = Mathf.Max(0.2f, growDuration);
        bomber._spawnCastle = EnemyNav.HomeCastleOf(socket);
        return bomber;
    }

    void Awake()
    {
        _config = EnemyConfig.Load(ConfigPath);
        _targeting = new EnemyTargeting(_config, transform);
        _health = _config.MaxHealth;
        _body = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
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

        if (_targeting.Tick(HandPosition()))
            EnemyNav.IgnoreCastle(_capsule, _targeting.HomeCastle, false);
        TickAttack(Time.deltaTime);
        _view.Tick(_windingUp ? 1f - _windUpTimer / Mathf.Max(0.01f, _config.TelegraphTime) : 0f);
    }

    void FixedUpdate()
    {
        if (_dead || !_released)
            return;

        Vector3 desired;
        Vector3 look;
        switch (_targeting.Current)
        {
            case EnemyTargeting.Goal.Exit:
                look = EnemyNav.DoorExitPoint(_targeting.HomeCastle, _body.position);
                desired = DirectionTo(look);
                break;
            case EnemyTargeting.Goal.Engage:
                look = _targeting.SeesPlayer ? EnemySenses.PlayerAimPoint() : _targeting.LastSeenPos;
                desired = _targeting.SeesPlayer ? KiteMove(look) : DirectionTo(look);
                break;
            case EnemyTargeting.Goal.Siege:
                look = _targeting.SiegeWall != null ? _targeting.SiegeWall.AimPoint : _body.position;
                desired = KiteMove(look);
                break;
            default:
                look = _body.position + _body.linearVelocity;
                desired = GuardMove();
                break;
        }

        if (_windingUp)
            desired = Vector3.zero;

        desired = EnemyNav.SteerFromWalls(_body.position, desired, CastRadius(), transform);
        Vector3 planar = Vector3.MoveTowards(
            EnemySenses.Flatten(_body.linearVelocity),
            EnemySenses.Flatten(desired) * _config.MoveSpeed,
            Acceleration * Time.fixedDeltaTime);
        _body.linearVelocity = new Vector3(planar.x, _body.linearVelocity.y, planar.z);

        RescueIfFellThrough();
        Face(look);
    }

    // ---------- attack ----------

    void TickAttack(float dt)
    {
        if (_windingUp)
        {
            _windUpTimer -= dt;
            if (_windUpTimer > 0f)
                return;
            _windingUp = false;
            bool thrown = TryThrow();
            _nextAttack = Time.time + (thrown ? _config.RollCooldown() : CancelledThrowCooldown);
            EnemyAttackTokens.Release(this);
            return;
        }

        if (Time.time < _nextAttack || !TryGetTarget(out _, out _, out bool onPlayer))
            return;
        if (onPlayer && !EnemyAttackTokens.TryAcquire(this))
            return;
        if (!TrySolveThrow(out _))
        {
            EnemyAttackTokens.Release(this);
            return;
        }

        _windingUp = true;
        _windUpTimer = _config.TelegraphTime;
    }

    bool TryThrow()
    {
        if (!TrySolveThrow(out Vector3 velocity))
            return false;
        bool onPlayer = _targeting.Current == EnemyTargeting.Goal.Engage;
        float damage = onPlayer ? _config.Damage : _config.SiegeDamage;
        EmberBomb.SpawnWithVelocity(HandPosition(), velocity, damage, BlastRadius, gameObject, hurtPlayer: true);
        return true;
    }

    /// <summary>Сначала пробуем настильную дугу (быстрее, честнее), потом навесную — через препятствие.</summary>
    bool TrySolveThrow(out Vector3 velocity)
    {
        velocity = Vector3.zero;
        if (!TryGetTarget(out Vector3 target, out Transform targetRoot, out _))
            return false;

        Vector3 origin = HandPosition();
        float speed = _config.ProjectileSpeed;
        if (EnemyBallistics.TrySolve(origin, target, speed, high: false, out velocity)
            && EnemyBallistics.ArcIsClear(origin, velocity, target, transform, targetRoot))
            return true;
        return EnemyBallistics.TrySolve(origin, target, speed, high: true, out velocity)
               && EnemyBallistics.ArcIsClear(origin, velocity, target, transform, targetRoot);
    }

    bool TryGetTarget(out Vector3 target, out Transform root, out bool onPlayer)
    {
        target = Vector3.zero;
        root = null;
        onPlayer = false;
        if (_targeting.Current == EnemyTargeting.Goal.Engage && _targeting.SeesPlayer && EnemySenses.Player != null)
        {
            root = EnemySenses.Player;
            target = EnemySenses.PlayerAimPoint();
            onPlayer = true;
        }
        else if (_targeting.Current == EnemyTargeting.Goal.Siege && _targeting.SiegeWall != null)
        {
            root = _targeting.SiegeWall.transform;
            target = _targeting.SiegeWall.AimPoint;
        }
        else
        {
            return false;
        }

        float dist = EnemySenses.PlanarDistance(_body.position, target);
        return dist >= MinThrowDistance && dist <= _config.AttackRange;
    }

    // ---------- movement ----------

    Vector3 KiteMove(Vector3 target)
    {
        Vector3 to = EnemySenses.Flatten(target - _body.position);
        float dist = to.magnitude;
        Vector3 toDir = dist > 0.01f ? to / dist : transform.forward;
        float standoff = _config.StandoffDistance;
        if (dist < standoff - 1.5f)
            return -toDir;
        if (dist > standoff + 2f)
            return toDir;

        if (Time.time >= _nextStrafeFlip)
        {
            _nextStrafeFlip = Time.time + Random.Range(StrafeFlipMin, StrafeFlipMax);
            _orbitSign = -_orbitSign;
        }

        return Vector3.Cross(Vector3.up, toDir) * (_orbitSign * 0.5f);
    }

    Vector3 GuardMove()
    {
        Vector3 offset = EnemySenses.Flatten(_body.position - _targeting.HomeCenter);
        if (offset.magnitude <= GuardRadius)
            return Vector3.zero;
        return -offset.normalized * 0.5f;
    }

    /// <summary>Страховка от проваливания сквозь пол; обычную высоту держит физика.</summary>
    void RescueIfFellThrough()
    {
        float ground = EnemyNav.GroundY(_body.position, _body.position.y, transform);
        if (_body.position.y < ground - FallRescueDepth)
        {
            Vector3 p = _body.position;
            p.y = ground + HalfHeight();
            _body.position = p;
            _body.linearVelocity = EnemySenses.Flatten(_body.linearVelocity);
        }
    }

    void Face(Vector3 world)
    {
        Vector3 flat = EnemySenses.Flatten(world - transform.position);
        if (flat.sqrMagnitude < 0.01f)
            return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(flat.normalized, Vector3.up),
            Time.fixedDeltaTime * 7f);
    }

    // ---------- lifecycle ----------

    void TickGrow()
    {
        _growElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_growElapsed / _growDuration);
        transform.localScale = BodyScale * Mathf.Lerp(MinGrowScale, 1f, t);
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
        _view = new EnemyBodyView(transform, _color, BodyScale);

        if (_targeting.Begin(_spawnCastle, transform.position))
            EnemyNav.IgnoreCastle(_capsule, _spawnCastle, true);

        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 0f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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
        LootDrop.Bomber(transform.position);
        Destroy(gameObject);
    }

    // ---------- helpers ----------

    Vector3 DirectionTo(Vector3 point)
    {
        Vector3 flat = EnemySenses.Flatten(point - _body.position);
        return flat.sqrMagnitude > 0.04f ? flat.normalized : Vector3.zero;
    }

    Vector3 HandPosition() => _body.position + Vector3.up * (HandHeight - HalfHeight())
                              + EnemySenses.Flatten(transform.forward).normalized * (CastRadius() + 0.25f);
    float HalfHeight() => _capsule != null ? _capsule.height * 0.5f * transform.lossyScale.y : 0.9f;
    float CastRadius() => _capsule != null ? _capsule.radius * transform.lossyScale.x : 0.4f;
}
