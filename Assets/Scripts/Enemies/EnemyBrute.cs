using UnityEngine;

/// <summary>
/// Walking brute: leaves via the door, then closes on the player and slams.
/// </summary>
public sealed class EnemyBrute : MonoBehaviour, IDamageable
{
    const float FullScale = 1f;
    const float MinGrowScale = 0.12f;
    const float WalkSpeed = 4.1f;
    const float MeleeRange = 2.15f;
    const float MeleeDamage = 12f;
    const float MeleeCooldown = 1.15f;
    const float ExitTimeout = 5f;

    [SerializeField] float _maxHealth = 36f;
    [SerializeField] Color _color = new Color(0.38f, 0.42f, 0.36f);

    float _health;
    float _growDuration = 1f;
    float _growElapsed;
    float _phaseElapsed;
    float _nextMelee;
    bool _dead;
    bool _released;
    bool _exiting = true;
    Transform _player;
    SquareCastle _homeCastle;
    Rigidbody _body;
    CapsuleCollider _capsule;

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

        go.transform.localScale = Vector3.one * MinGrowScale;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var brute = go.AddComponent<EnemyBrute>();
        brute._growDuration = Mathf.Max(0.2f, growDuration);
        brute._body = body;
        brute._capsule = go.GetComponent<CapsuleCollider>();
        brute._homeCastle = socket != null ? socket.GetComponentInParent<SquareCastle>() : null;
        brute.Tint();
        return brute;
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

        TickHunt();
    }

    void TickGrow()
    {
        _growElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_growElapsed / _growDuration);
        transform.localScale = new Vector3(0.85f, 1.05f, 0.85f) * Mathf.Lerp(MinGrowScale, FullScale, t);
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
        transform.localScale = new Vector3(0.85f, 1.05f, 0.85f);
        _exiting = _homeCastle != null;
        _phaseElapsed = 0f;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearDamping = 2.4f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SnapFeet();
    }

    void TickExit()
    {
        _phaseElapsed += Time.fixedDeltaTime;
        Vector3 door = EnemyNav.DoorExitPoint(_homeCastle, _body.position);
        MoveToward(door, WalkSpeed * 1.15f, 1.1f);
        if (_phaseElapsed > ExitTimeout || EnemyNav.Flatten(door - _body.position).magnitude < 1.4f)
            _exiting = false;
    }

    void TickHunt()
    {
        if (_player == null)
            return;

        Vector3 target = _player.position;
        float dist = EnemyNav.Flatten(target - _body.position).magnitude;
        if (dist <= MeleeRange)
        {
            _body.linearVelocity = new Vector3(0f, _body.linearVelocity.y, 0f);
            Face(target);
            TryMelee();
            return;
        }

        Vector3 desired = EnemyNav.Flatten(target - _body.position).normalized;
        desired = EnemyNav.SteerFromWalls(_body.position, desired, 0.45f);
        MoveToward(_body.position + desired * 4f, WalkSpeed, 0.4f);
        Face(target);
    }

    void TryMelee()
    {
        if (Time.time < _nextMelee || _player == null)
            return;
        _nextMelee = Time.time + MeleeCooldown;
        var hp = _player.GetComponent<PlayerHealth>();
        if (hp == null)
            return;
        Vector3 point = _player.position + Vector3.up;
        hp.ApplyDamage(MeleeDamage, new DamageInfo(point, Vector3.up, transform.forward));
        HitSparkVfx.PlayDust(point, transform.forward, 8);
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
        float feet = y + 1.05f;
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
            Time.fixedDeltaTime * 8f);
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_dead || amount <= 0f)
            return;
        _health -= amount;
        if (_health > 0f)
            return;
        _dead = true;
        LootDrop.Brute(transform.position);
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
