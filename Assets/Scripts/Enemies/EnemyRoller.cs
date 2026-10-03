using UnityEngine;

/// <summary>
/// Ground boulder: rolls in, crouches (telegraph), then pounces along an arc into the player or a wall.
/// The pounce is swept every physics step, so it stops on obstacles instead of passing through them.
/// </summary>
public sealed class EnemyRoller : MonoBehaviour, IDamageable
{
    const string ConfigPath = "Enemies/RollerConfig";
    const float FullScale = 1.15f;
    const float MinGrowScale = 0.1f;
    const float Acceleration = 20f;
    const float PounceMaxTravel = 6.5f;
    const float PounceMinTravel = 1.4f;
    const float PounceHeight = 2.15f;
    const float PounceMinDuration = 0.42f;
    const float PounceMaxDuration = 0.62f;
    const float WallEngage = 4.2f;
    const float StuckFlipTime = 1.4f;
    const float GuardRadius = 8f;
    const float FallRescueDepth = 2f;

    [SerializeField] Color _color = new Color(0.82f, 0.42f, 0.12f);

    enum Phase { Growing, Roll, Crouch, Pounce, Recover }

    EnemyConfig _config;
    EnemyTargeting _targeting;
    EnemyBodyView _view;
    float _health;
    float _growDuration;
    float _growElapsed;
    bool _dead;
    Phase _phase = Phase.Growing;
    float _phaseElapsed;
    float _orbitSign = 1f;
    float _stuckTime;
    float _bestTargetDist = float.MaxValue;
    bool _hitThisPounce;
    Vector3 _pounceFrom;
    Vector3 _pounceTo;
    float _pounceDuration;
    float _recoverDuration;
    Transform _spawnCastle;
    SphereCollider _sphere;
    Rigidbody _body;

    public bool IsGrowing => _phase == Phase.Growing && !_dead;

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
        unit._spawnCastle = EnemyNav.HomeCastleOf(socket);
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

    void Update()
    {
        if (_dead)
            return;
        if (_phase == Phase.Growing)
        {
            TickGrow();
            return;
        }

        if (_targeting.Tick(_body.position))
            EnemyNav.IgnoreCastle(_sphere, _targeting.HomeCastle, false);
        float charge = _phase == Phase.Crouch
            ? _phaseElapsed / Mathf.Max(0.01f, _config.TelegraphTime)
            : _phase == Phase.Pounce ? 1f : 0f;
        _view.Tick(charge);
        SpinByVelocity();
    }

    void FixedUpdate()
    {
        if (_dead || _phase == Phase.Growing)
            return;

        _phaseElapsed += Time.fixedDeltaTime;
        switch (_phase)
        {
            case Phase.Roll:
                TickRoll();
                break;
            case Phase.Crouch:
                BrakePlanar();
                if (_phaseElapsed >= _config.TelegraphTime)
                    StartPounce();
                break;
            case Phase.Pounce:
                TickPounce();
                break;
            case Phase.Recover:
                BrakePlanar();
                if (_phaseElapsed >= _recoverDuration)
                    EnterPhase(Phase.Roll);
                break;
        }

        if (_phase != Phase.Pounce)
            RescueIfFellThrough();
    }

    // ---------- phases ----------

    void TickRoll()
    {
        Vector3 target = RollTarget(out float engage);
        float dist = EnemySenses.PlanarDistance(_body.position, target);
        if (engage > 0f && dist <= engage && ClearToPounce(target))
        {
            EnterPhase(Phase.Crouch);
            return;
        }

        // Если не приближаемся к цели — меняем сторону обхода, а не бьёмся в угол бесконечно.
        if (dist + 0.4f < _bestTargetDist)
        {
            _bestTargetDist = dist;
            _stuckTime = 0f;
        }
        else
        {
            _stuckTime += Time.fixedDeltaTime;
            if (_stuckTime > StuckFlipTime)
            {
                _stuckTime = 0f;
                _orbitSign = -_orbitSign;
                _bestTargetDist = dist;
            }
        }

        Vector3 desired = _targeting.Current == EnemyTargeting.Goal.Guard ? GuardMove() : DirectionTo(target);
        desired = EnemyNav.SteerFromWalls(_body.position, desired, Radius(), transform);
        Vector3 planar = Vector3.MoveTowards(
            EnemySenses.Flatten(_body.linearVelocity),
            EnemySenses.Flatten(desired) * _config.MoveSpeed,
            Acceleration * Time.fixedDeltaTime);
        _body.linearVelocity = new Vector3(planar.x, _body.linearVelocity.y, planar.z);
    }

    /// <summary>Returns where to roll and at what distance to pounce (0 = never pounce at this target).</summary>
    Vector3 RollTarget(out float engage)
    {
        engage = 0f;
        switch (_targeting.Current)
        {
            case EnemyTargeting.Goal.Exit:
                return EnemyNav.DoorExitPoint(_targeting.HomeCastle, _body.position);
            case EnemyTargeting.Goal.Engage:
                if (!_targeting.SeesPlayer || EnemySenses.Player == null)
                    return _targeting.LastSeenPos;
                engage = _config.AttackRange;
                return EnemySenses.Player.position;
            case EnemyTargeting.Goal.Siege:
                if (_targeting.SiegeWall == null)
                    return _body.position;
                engage = WallEngage;
                return _targeting.SiegeWall.AimPoint;
            default:
                return _body.position;
        }
    }

    bool ClearToPounce(Vector3 target)
    {
        Transform root = _targeting.Current == EnemyTargeting.Goal.Siege && _targeting.SiegeWall != null
            ? _targeting.SiegeWall.transform
            : EnemySenses.Player;
        Vector3 aim = _targeting.Current == EnemyTargeting.Goal.Engage ? EnemySenses.PlayerAimPoint() : target;
        return EnemySenses.HasClearPath(_body.position, aim, Radius() * 0.6f, transform, root);
    }

    void StartPounce()
    {
        Vector3 aim = RollTarget(out _);
        Vector3 planar = EnemySenses.Flatten(aim - _body.position);
        float dist = planar.magnitude;
        Vector3 dir = dist > 0.05f ? planar / dist : EnemySenses.Flatten(transform.forward).normalized;
        dist = Mathf.Clamp(dist, PounceMinTravel, PounceMaxTravel);

        _pounceFrom = _body.position;
        _pounceTo = _pounceFrom + dir * dist;
        _pounceTo.y = EnemyNav.GroundY(_pounceTo, _pounceFrom.y - Radius(), transform) + Radius();
        _pounceDuration = Mathf.Lerp(PounceMinDuration, PounceMaxDuration, dist / PounceMaxTravel);
        _hitThisPounce = false;
        EnterPhase(Phase.Pounce);
        // Unity 6 не даёт писать скорость кинематическому телу — обнуляем, пока оно ещё динамическое.
        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;
        _body.isKinematic = true;
        _body.useGravity = false;
    }

    void TickPounce()
    {
        float t = Mathf.Clamp01(_phaseElapsed / Mathf.Max(0.05f, _pounceDuration));
        float u = t * t * (3f - 2f * t);
        Vector3 next = Vector3.Lerp(_pounceFrom, _pounceTo, u);
        next.y += 4f * PounceHeight * t * (1f - t);

        // Кинематическое тело проходит сквозь стены, поэтому каждый шаг прыжка проверяем свипом.
        Vector3 step = next - _body.position;
        float length = step.magnitude;
        if (length > 0.0001f
            && Physics.SphereCast(_body.position, Radius() * 0.9f, step / length, out RaycastHit hit, length, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform)
            && !EnemySenses.IsEnemy(hit.collider))
        {
            _body.MovePosition(_body.position + step / length * Mathf.Max(0f, hit.distance - 0.02f));
            ImpactOn(hit.collider, hit.point);
            StartRecover();
            return;
        }

        _body.MovePosition(next);
        if (t >= 1f)
            StartRecover();
    }

    void ImpactOn(Collider col, Vector3 point)
    {
        if (_hitThisPounce)
            return;
        _hitThisPounce = true;
        Vector3 dir = EnemySenses.Flatten(_pounceTo - _pounceFrom).normalized;
        float damage = EnemySenses.IsPlayer(col) ? _config.Damage : _config.SiegeDamage;
        DamageUtility.ApplyEnemyHit(col, damage, point, -dir, dir);
        HitSparkVfx.PlayDust(point, -dir, 10);
    }

    void StartRecover()
    {
        EnterPhase(Phase.Recover);
        _recoverDuration = _config.RollCooldown();
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearVelocity = Vector3.zero;
    }

    void EnterPhase(Phase phase)
    {
        _phase = phase;
        _phaseElapsed = 0f;
        if (phase == Phase.Roll)
        {
            _stuckTime = 0f;
            _bestTargetDist = float.MaxValue;
        }
    }

    // ---------- movement helpers ----------

    Vector3 GuardMove()
    {
        Vector3 offset = EnemySenses.Flatten(_body.position - _targeting.HomeCenter);
        if (offset.magnitude <= GuardRadius)
            return Vector3.zero;
        return -offset.normalized * 0.5f;
    }

    void BrakePlanar()
    {
        if (_body.isKinematic)
            return;
        Vector3 planar = Vector3.MoveTowards(EnemySenses.Flatten(_body.linearVelocity), Vector3.zero, Acceleration * Time.fixedDeltaTime);
        _body.linearVelocity = new Vector3(planar.x, _body.linearVelocity.y, planar.z);
    }

    /// <summary>Страховка от проваливания сквозь пол; обычную высоту держит физика.</summary>
    void RescueIfFellThrough()
    {
        float ground = EnemyNav.GroundY(_body.position, _body.position.y, transform);
        if (_body.position.y < ground - FallRescueDepth)
        {
            Vector3 p = _body.position;
            p.y = ground + Radius();
            _body.position = p;
            _body.linearVelocity = EnemySenses.Flatten(_body.linearVelocity);
        }
    }

    void SpinByVelocity()
    {
        Vector3 v = _phase == Phase.Pounce
            ? EnemySenses.Flatten(_pounceTo - _pounceFrom) / Mathf.Max(0.05f, _pounceDuration)
            : EnemySenses.Flatten(_body.linearVelocity);
        float speed = v.magnitude;
        if (speed < 0.08f)
            return;
        Vector3 axis = Vector3.Cross(Vector3.up, v / speed);
        transform.Rotate(axis, speed / Radius() * Mathf.Rad2Deg * Time.deltaTime, Space.World);
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
        if (_spawnCastle == null)
            _spawnCastle = EnemyNav.HomeCastleOf(transform);
        transform.SetParent(null, true);
        _view = new EnemyBodyView(transform, _color, Vector3.one * FullScale);

        if (_targeting.Begin(_spawnCastle, transform.position))
            EnemyNav.IgnoreCastle(_sphere, _spawnCastle, true);

        _body.constraints = RigidbodyConstraints.FreezeRotation;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 0f;
        _body.angularDamping = 1.2f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.maxDepenetrationVelocity = 2f;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        EnterPhase(Phase.Roll);
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

    Vector3 DirectionTo(Vector3 point)
    {
        Vector3 flat = EnemySenses.Flatten(point - _body.position);
        return flat.sqrMagnitude > 0.04f ? flat.normalized : Vector3.zero;
    }

    float Radius()
    {
        float r = _sphere != null ? _sphere.radius : 0.5f;
        return Mathf.Max(0.16f, r * FullScale);
    }
}
