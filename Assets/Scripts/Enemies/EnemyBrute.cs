using UnityEngine;

/// <summary>
/// Walking brute: closes on the player (or a player wall during a raid), winds up, then slams.
/// The slam only lands if the target is still in reach after the wind-up — players can dodge.
/// </summary>
public sealed class EnemyBrute : MonoBehaviour, IDamageable
{
    const string ConfigPath = "Enemies/BruteConfig";
    static readonly Vector3 BodyScale = new Vector3(0.85f, 1.05f, 0.85f);
    const float MinGrowScale = 0.12f;
    const float Acceleration = 20f;
    const float MaxReachHeight = 1.6f;
    // Удар засчитывается с небольшим запасом дистанции, иначе отскок на полшага всегда спасает.
    const float StrikeReachBonus = 1.2f;
    const float WallStrikeRadius = 1.1f;
    const float GuardRadius = 8f;
    const float FallRescueDepth = 2f;

    [SerializeField] Color _color = new Color(0.38f, 0.42f, 0.36f);

    static readonly Collider[] StrikeHits = new Collider[16];

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

    public bool IsGrowing => !_released && !_dead;

    public static EnemyBrute SpawnInSocket(Transform socket, float growDuration)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "EnemyBrute";
        if (socket != null)
        {
            go.transform.SetParent(socket, false);
            go.transform.localPosition = Vector3.up * 0.2f;
            go.transform.localRotation = Quaternion.identity;
        }

        go.transform.localScale = BodyScale * MinGrowScale;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var brute = go.AddComponent<EnemyBrute>();
        brute._growDuration = Mathf.Max(0.2f, growDuration);
        brute._spawnCastle = EnemyNav.HomeCastleOf(socket);
        return brute;
    }

    void Awake()
    {
        _config = EnemyConfig.Load(ConfigPath);
        _targeting = new EnemyTargeting(_config, transform);
        _health = _config.MaxHealth;
        _body = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
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

        if (_targeting.Tick(EyePosition()))
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
                look = _targeting.SeesPlayer ? EnemySenses.Player.position : _targeting.LastSeenPos;
                desired = InMeleeReachOfPlayer() ? Vector3.zero : DirectionTo(look);
                break;
            case EnemyTargeting.Goal.Siege:
                look = _targeting.SiegeWall != null ? _targeting.SiegeWall.AimPoint : _body.position;
                desired = WallInReach() ? Vector3.zero : DirectionTo(look);
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
            Strike();
            _nextAttack = Time.time + _config.RollCooldown();
            return;
        }

        if (Time.time < _nextAttack)
            return;
        bool hasTarget = _targeting.Current == EnemyTargeting.Goal.Engage
            ? InMeleeReachOfPlayer()
            : _targeting.Current == EnemyTargeting.Goal.Siege && WallInReach();
        if (!hasTarget)
            return;

        _windingUp = true;
        _windUpTimer = _config.TelegraphTime;
    }

    void Strike()
    {
        Vector3 forward = EnemySenses.Flatten(transform.forward).normalized;
        if (_targeting.Current == EnemyTargeting.Goal.Engage)
        {
            if (!InMeleeReachOfPlayer(StrikeReachBonus))
                return;
            var hp = EnemySenses.Player.GetComponent<PlayerHealth>();
            if (hp == null)
                return;
            Vector3 point = EnemySenses.PlayerAimPoint();
            hp.ApplyDamage(_config.Damage, new DamageInfo(point, -forward, forward, fromPlayer: false));
            HitSparkVfx.PlayDust(point, forward, 8);
            return;
        }

        Collider wallCol = FindWallColliderInReach();
        if (wallCol == null)
            return;
        Vector3 hit = DamageUtility.ClosestPoint(wallCol, StrikeOrigin());
        DamageUtility.ApplyEnemyHit(wallCol, _config.SiegeDamage, hit, -forward, forward);
        HitSparkVfx.PlayDust(hit, -forward, 10);
    }

    bool InMeleeReachOfPlayer(float bonus = 0f)
    {
        Transform player = EnemySenses.Player;
        if (player == null)
            return false;
        // Проверка высоты и прямой видимости: раньше брут бил сквозь стены и на второй этаж.
        if (Mathf.Abs(player.position.y - FeetY()) > MaxReachHeight)
            return false;
        if (EnemySenses.PlanarDistance(_body.position, player.position) > _config.AttackRange + bonus)
            return false;
        return EnemySenses.HasClearPath(EyePosition(), EnemySenses.PlayerAimPoint(), 0f, transform, player);
    }

    bool WallInReach() => FindWallColliderInReach() != null;

    Collider FindWallColliderInReach()
    {
        CarcassWallBreakable wall = _targeting.SiegeWall;
        if (wall == null)
            return null;
        int count = Physics.OverlapSphereNonAlloc(StrikeOrigin(), WallStrikeRadius, StrikeHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (StrikeHits[i] != null && StrikeHits[i].transform.IsChildOf(wall.transform))
                return StrikeHits[i];
        }

        return null;
    }

    // ---------- movement ----------

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
            Time.fixedDeltaTime * 8f);
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
        _nextAttack = Time.time + 0.5f;
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
        LootDrop.Brute(transform.position);
        Destroy(gameObject);
    }

    // ---------- helpers ----------

    Vector3 DirectionTo(Vector3 point)
    {
        Vector3 flat = EnemySenses.Flatten(point - _body.position);
        return flat.sqrMagnitude > 0.04f ? flat.normalized : Vector3.zero;
    }

    Vector3 EyePosition() => _body.position + Vector3.up * (HalfHeight() * 0.6f);
    Vector3 StrikeOrigin() => _body.position + EnemySenses.Flatten(transform.forward).normalized * (CastRadius() + 0.6f);
    float FeetY() => _body.position.y - HalfHeight();
    float HalfHeight() => _capsule != null ? _capsule.height * 0.5f * transform.lossyScale.y : 1f;
    float CastRadius() => _capsule != null ? _capsule.radius * transform.lossyScale.x : 0.45f;
}
