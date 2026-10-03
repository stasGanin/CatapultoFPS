using UnityEngine;

/// <summary>
/// One pre-sliced module piece. Intact = kinematic. On break: either falls as debris or is destroyed in place.
/// Loot is authored on this component in the module prefab.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CastleWallChunk : MonoBehaviour
{
    const string PhysicsChildName = "Physics";
    const float DefaultColliderShrink = 0.88f;

    [SerializeField] int _detachHp = 8;
    [SerializeField] int _mineHp = 10;
    [SerializeField] float _detachImpulse = 0.55f;
    [SerializeField] float _detachNudge = 0.14f;
    [SerializeField, Range(0.7f, 1f)] float _colliderShrink = DefaultColliderShrink;
    [SerializeField] bool _destroyInPlace;

    [Header("Loot (pickaxe after detach, or on destroy-in-place)")]
    [SerializeField] ItemDefinition _lootItem;
    [SerializeField] int _lootCount = 1;

    int _hp;
    bool _detached;
    Rigidbody _body;
    MeshCollider _meshCollider;
    PreSlicedCastleWall _wall;
    Transform _restParent;
    Vector3 _restLocalPos;
    Quaternion _restLocalRot = Quaternion.identity;
    Vector3 _restLocalScale = Vector3.one;
    bool _restPoseCaptured;

    public bool IsDetached => _detached;
    public bool IsDamaged => !_detached && _hp < _detachHp;
    public bool NeedsRestore => _detached || _hp < _detachHp;
    public bool DestroyInPlace => _destroyInPlace;
    public bool BelongsToPlayerCastle
    {
        get
        {
            var square = GetComponentInParent<SquareCastle>();
            return square != null && square.IsPlayerOwned;
        }
    }
    public Collider ChunkCollider => _meshCollider;
    public ItemDefinition LootItem => _lootItem;
    public int LootCount => Mathf.Max(1, _lootCount);

    public void Heal(int amount)
    {
        if (_detached || amount <= 0)
            return;
        _hp = Mathf.Min(_detachHp, _hp + amount);
    }

    public void RestoreFull()
    {
        if (_detached)
            return;
        _hp = _detachHp;
    }

    public void CaptureRestPose()
    {
        if (_restPoseCaptured || transform.parent == null)
            return;
        _restParent = transform.parent;
        _restLocalPos = transform.localPosition;
        _restLocalRot = transform.localRotation;
        _restLocalScale = transform.localScale;
        _restPoseCaptured = true;
    }

    public void RestoreToSocket()
    {
        var shrink = GetComponent<DebrisShrinkDespawn>();
        if (shrink != null)
        {
            shrink.Cancel();
            Destroy(shrink);
        }

        var mineable = GetComponent<MineableDebris>();
        if (mineable != null)
            Destroy(mineable);

        if (_restParent != null)
            transform.SetParent(_restParent, false);
        transform.localPosition = _restLocalPos;
        transform.localRotation = _restLocalRot;
        transform.localScale = _restLocalScale;

        SetVisualsHidden(false);
        HoldInPlace();
        EnsurePhysicsCollider();
        if (_meshCollider != null)
            _meshCollider.enabled = true;
        if (_body != null)
            _body.detectCollisions = true;
        _hp = _detachHp;
        enabled = true;
    }

    public void ParkForRepair()
    {
        var shrink = GetComponent<DebrisShrinkDespawn>();
        if (shrink != null)
            Destroy(shrink);

        var mineable = GetComponent<MineableDebris>();
        if (mineable != null)
            Destroy(mineable);

        transform.localScale = _restLocalScale;
        if (_restParent != null)
        {
            transform.SetParent(_restParent, false);
            transform.localPosition = _restLocalPos;
            transform.localRotation = _restLocalRot;
        }

        if (_body == null)
            _body = GetComponent<Rigidbody>();
        if (_body != null)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.detectCollisions = false;
            _body.constraints = RigidbodyConstraints.FreezeAll;
        }

        if (_meshCollider != null)
            _meshCollider.enabled = false;

        SetVisualsHidden(true);
        _detached = true;
        enabled = false;
    }

    void SetVisualsHidden(bool hidden)
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = !hidden;
        }
    }

    public void Configure(int detachHp, int mineHp, float detachImpulse, PreSlicedCastleWall wall)
    {
        _detachHp = Mathf.Max(1, detachHp);
        _mineHp = Mathf.Max(1, mineHp);
        _detachImpulse = Mathf.Max(0f, detachImpulse);
        _hp = _detachHp;
        _wall = wall;
        EnsurePhysicsCollider();
        HoldInPlace();
        CaptureRestPose();
    }

    public void SetLoot(ItemDefinition item, int count)
    {
        _lootItem = item;
        _lootCount = Mathf.Max(1, count);
    }

    public void ConfigureSegment(int detachHp, int mineHp, float detachImpulse, bool destroyInPlace)
    {
        _detachHp = Mathf.Max(1, detachHp);
        _mineHp = Mathf.Max(1, mineHp);
        _detachImpulse = Mathf.Max(0f, detachImpulse);
        _destroyInPlace = destroyInPlace;
        _hp = _detachHp;
        EnsurePhysicsCollider();
        HoldInPlace();
        CaptureRestPose();
    }

    void Awake()
    {
        _body = GetComponent<Rigidbody>();
        EnsurePhysicsCollider();
        if (_hp <= 0)
            _hp = _detachHp;
        CaptureRestPose();
        HoldInPlace();
    }

    public void EnsurePhysicsCollider()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        foreach (var col in GetComponents<Collider>())
            DestroyCompat(col);

        Transform child = transform.Find(PhysicsChildName);
        if (child == null)
        {
            var go = new GameObject(PhysicsChildName);
            child = go.transform;
            child.SetParent(transform, false);
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        // Attached: exact mesh so holes/courtyard stay empty. Shrink only after detach.
        float shrink = _detached ? Mathf.Clamp(_colliderShrink, 0.7f, 1f) : 1f;
        child.localScale = Vector3.one * shrink;

        _meshCollider = child.GetComponent<MeshCollider>();
        if (_meshCollider == null)
            _meshCollider = child.gameObject.AddComponent<MeshCollider>();

        _meshCollider.sharedMesh = mf.sharedMesh;
        // Convex hull of a holed wall fills the room and traps player/camera.
        _meshCollider.convex = _detached;
        _meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
    }

    public void HoldInPlace()
    {
        _detached = false;
        if (_body == null)
            _body = GetComponent<Rigidbody>();
        if (_body == null)
            return;

        if (!_body.isKinematic)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        _body.isKinematic = true;
        _body.useGravity = false;
        _body.detectCollisions = true;
        _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _body.interpolation = RigidbodyInterpolation.None;
        _body.constraints = RigidbodyConstraints.FreezeAll;

        if (_meshCollider != null)
            _meshCollider.convex = false;
    }

    public bool ApplyHit(int damage, Vector3 hitPoint, Vector3 hitNormal, bool fromPlayer = true)
    {
        if (_detached || damage <= 0)
            return false;
        // Player cannot grief own walls; enemies cannot siege their own castle.
        if (fromPlayer && BelongsToPlayerCastle)
            return false;
        if (!fromPlayer && !BelongsToPlayerCastle)
            return false;

        _hp -= damage;
        if (_hp > 0)
            return true;

        if (_destroyInPlace && !BelongsToPlayerCastle)
            DestroyCompletely(hitPoint);
        else
            Detach(hitPoint, hitNormal, _detachImpulse);
        return true;
    }

    public void DestroyCompletely(Vector3 hitPoint)
    {
        if (_detached)
            return;

        _detached = true;
        NotifyHosts();

        if (_lootItem != null)
            WorldLootPickup.Spawn(_lootItem, LootCount, hitPoint, 1.6f);

        Destroy(gameObject);
    }

    public void Detach(Vector3 hitPoint, Vector3 outward, float impulse = -1f)
    {
        if (_detached)
            return;

        if (_destroyInPlace && !BelongsToPlayerCastle)
        {
            DestroyCompletely(hitPoint);
            return;
        }

        _detached = true;
        CaptureRestPose();
        bool vanishAsRubble = BelongsToPlayerCastle;

        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector3.up;
        else
            outward.Normalize();

        float force = impulse >= 0f ? impulse : _detachImpulse;
        NotifyHosts();

        transform.position += outward * _detachNudge;
        transform.SetParent(null, true);

        if (_body == null)
            _body = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();

        if (_meshCollider != null)
        {
            Transform physics = _meshCollider.transform;
            physics.localScale = Vector3.one * Mathf.Clamp(_colliderShrink, 0.7f, 1f);
            _meshCollider.convex = true;
        }

        _body.constraints = RigidbodyConstraints.None;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.mass = Mathf.Clamp(_body.mass, 2f, 12f);
        _body.linearDamping = 0.55f;
        _body.angularDamping = 0.9f;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.maxDepenetrationVelocity = 0.8f;
        _body.interpolation = RigidbodyInterpolation.Interpolate;

        Vector3 push = (outward + Vector3.up * 0.2f).normalized * force;
        _body.AddForce(push, ForceMode.Impulse);
        _body.AddTorque(Random.onUnitSphere * (force * 0.06f), ForceMode.Impulse);

        if (vanishAsRubble)
        {
            var shrink = gameObject.GetComponent<DebrisShrinkDespawn>();
            if (shrink == null)
                shrink = gameObject.AddComponent<DebrisShrinkDespawn>();
            shrink.Play(3f);
        }
        else
        {
            var mineable = GetComponent<MineableDebris>();
            if (mineable == null)
                mineable = gameObject.AddComponent<MineableDebris>();
            mineable.ResetHp(_mineHp);
            if (_lootItem != null)
                mineable.ConfigureLoot(_lootItem, LootCount);
        }

        enabled = false;
    }

    void NotifyHosts()
    {
        var module = GetComponentInParent<CastleModuleRoot>();
        if (module != null)
            module.OnChunkDetached(this);

        var square = GetComponentInParent<SquareCastle>();
        if (square != null)
            square.OnChunkDetached(this);

        if (_wall != null)
            _wall.OnChunkDetached(this);
    }

    static void DestroyCompat(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}
