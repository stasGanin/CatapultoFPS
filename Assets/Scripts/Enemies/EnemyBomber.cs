using UnityEngine;

/// <summary>
/// Kiting bombardier: walks to a standoff, then lobs arcing ember bombs.
/// </summary>
public sealed class EnemyBomber : MonoBehaviour, IDamageable
{
    const float FullScale = 1f;
    const float MinGrowScale = 0.12f;
    const float WalkSpeed = 4.6f;
    const float Standoff = 12f;
    const float FireInterval = 2.15f;
    const float ExitTimeout = 5f;
    const float LobSpeed = 14f;

    [SerializeField] float _maxHealth = 22f;
    [SerializeField] Color _color = new Color(0.85f, 0.32f, 0.12f);

    float _health;
    float _growDuration = 1f;
    float _growElapsed;
    float _phaseElapsed;
    float _nextFire;
    bool _dead;
    bool _released;
    bool _exiting = true;
    Transform _player;
    SquareCastle _homeCastle;
    Rigidbody _body;
    CapsuleCollider _capsule;

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

        go.transform.localScale = Vector3.one * MinGrowScale;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var bomber = go.AddComponent<EnemyBomber>();
        bomber._growDuration = Mathf.Max(0.2f, growDuration);
        bomber._body = body;
        bomber._capsule = go.GetComponent<CapsuleCollider>();
        bomber._homeCastle = socket != null ? socket.GetComponentInParent<SquareCastle>() : null;
        bomber.Tint();
        return bomber;
    }

    void Awake()
    {
        _health = _maxHealth;
        if (_body == null)
            _body = GetComponent<Rigidbody>();
        if (_capsule == null)
            _capsule = GetComponent<CapsuleCollider>();
        _player = EnemyNav.FindPlayer();
        Tint();
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
            _player = EnemyNav.FindPlayer();
        if (!_exiting)
            TryLob();
    }

    void FixedUpdate()
    {
        if (_dead || !_released || _body == null)
            return;
        if (_exiting)
        {
            TickExit();
            return;
        }

        TickKite();
    }

    void TickGrow()
    {
        _growElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_growElapsed / _growDuration);
        transform.localScale = new Vector3(0.7f, 0.9f, 0.7f) * Mathf.Lerp(MinGrowScale, FullScale, t);
        if (t >= 1f)
            Release();
    }

    void Release()
    {
        if (_released)
            return;
        _released = true;
        _homeCastle = GetComponentInParent<SquareCastle>();
        EnemyNav.IgnoreCastle(_capsule, _homeCastle, true);
        transform.SetParent(null, true);
        transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        _exiting = _homeCastle != null;
        _phaseElapsed = 0f;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 2.2f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SnapFeet();
    }

    void TickExit()
    {
        _phaseElapsed += Time.fixedDeltaTime;
        Vector3 door = EnemyNav.DoorExitPoint(_homeCastle, _body.position);
        MoveToward(door, WalkSpeed * 1.1f, 1.1f);
        if (_phaseElapsed > ExitTimeout || EnemyNav.Flatten(door - _body.position).magnitude < 1.4f)
            _exiting = false;
    }

    void TickKite()
    {
        if (_player == null)
            return;

        Vector3 to = EnemyNav.Flatten(_player.position - _body.position);
        float dist = to.magnitude;
        Vector3 desired;
        if (dist < Standoff - 1.5f)
            desired = -to.normalized;
        else if (dist > Standoff + 2f)
            desired = to.normalized;
        else
            desired = Vector3.Cross(Vector3.up, to.normalized);

        desired = EnemyNav.SteerFromWalls(_body.position, desired, 0.4f);
        MoveToward(_body.position + desired * 3f, WalkSpeed, 0.35f);
        Face(_player.position);
    }

    void TryLob()
    {
        if (_player == null || Time.time < _nextFire)
            return;
        Vector3 to = _player.position - transform.position;
        if (to.magnitude > 22f || to.magnitude < 4f)
            return;

        _nextFire = Time.time + FireInterval;
        Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.4f;
        Vector3 aim = _player.position + Vector3.up * 0.9f;
        EmberBomb.Spawn(origin, AimArc(origin, aim, LobSpeed), LobSpeed, 14f, 2.4f, gameObject, hurtPlayer: true);
    }

    static Vector3 AimArc(Vector3 from, Vector3 to, float speed)
    {
        Vector3 delta = to - from;
        delta.y += 2.4f;
        if (delta.sqrMagnitude < 0.01f)
            return Vector3.up;
        return delta.normalized;
    }

    void MoveToward(Vector3 world, float speed, float stop)
    {
        Vector3 flat = EnemyNav.Flatten(world - _body.position);
        float dist = flat.magnitude;
        Vector3 vel = dist > stop ? flat / dist * speed : Vector3.zero;
        vel.y = _body.linearVelocity.y;
        _body.linearVelocity = vel;
        SnapFeet();
    }

    void SnapFeet()
    {
        float y = EnemyNav.GroundY(_body.position, _body.position.y);
        Vector3 p = _body.position;
        float feet = y + 0.9f;
        if (p.y < feet)
        {
            p.y = feet;
            _body.position = p;
            if (_body.linearVelocity.y < 0f)
                _body.linearVelocity = new Vector3(_body.linearVelocity.x, 0f, _body.linearVelocity.z);
        }
    }

    void Face(Vector3 world)
    {
        Vector3 flat = EnemyNav.Flatten(world - transform.position);
        if (flat.sqrMagnitude < 0.01f)
            return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(flat.normalized, Vector3.up),
            Time.fixedDeltaTime * 7f);
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_dead || amount <= 0f)
            return;
        _health -= amount;
        if (_health > 0f)
            return;
        _dead = true;
        LootDrop.Bomber(transform.position);
        Destroy(gameObject);
    }

    void Tint()
    {
        var rend = GetComponent<MeshRenderer>();
        if (rend == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            rend.sharedMaterial = new Material(shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", _color);
        block.SetColor("_Color", _color);
        rend.SetPropertyBlock(block);
    }
}
