using UnityEngine;

/// <summary>
/// Module with shared HP. On death: shuts down ICastleModuleBehavior children,
/// hides intact visuals, spawns authored debris prefab (mineable pieces).
/// Put this on the module prefab root (same footprint as EmptyWall).
/// </summary>
[DisallowMultipleComponent]
public sealed class CastleModuleBreakable : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] float _maxHealth = 100f;

    [Header("Intact")]
    [Tooltip("Hidden when destroyed. If empty, disables renderers/colliders on this object.")]
    [SerializeField] GameObject _intactRoot;

    [Header("Debris")]
    [Tooltip("Pre-sliced debris prefab spawned on destroy. Configure MineableDebris loot on its pieces.")]
    [SerializeField] GameObject _debrisPrefab;
    [SerializeField] Transform _debrisSpawnParent;
    [SerializeField] float _debrisImpulse = 1.2f;
    [SerializeField] bool _matchDebrisWorldScale = true;
    [SerializeField] bool _destroyIntactObject;

    float _health;
    bool _destroyed;
    ICastleModuleBehavior[] _behaviors;

    public float Health => _health;
    public float MaxHealth => _maxHealth;
    public bool IsDestroyed => _destroyed;

    void Awake()
    {
        _health = Mathf.Max(1f, _maxHealth);
        CacheBehaviors();

        if (_intactRoot == null)
            _intactRoot = gameObject;
    }

    void CacheBehaviors()
    {
        var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        var list = new System.Collections.Generic.List<ICastleModuleBehavior>(4);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICastleModuleBehavior module)
                list.Add(module);
        }

        _behaviors = list.ToArray();
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_destroyed || amount <= 0f)
            return;

        _health -= amount;
        if (_health > 0f)
            return;

        _health = 0f;
        Break(info);
    }

    void Break(in DamageInfo info)
    {
        if (_destroyed)
            return;
        _destroyed = true;

        if (_behaviors == null)
            CacheBehaviors();

        for (int i = 0; i < _behaviors.Length; i++)
        {
            if (_behaviors[i] != null)
                _behaviors[i].OnModuleDestroyed();
        }

        HideIntact();
        SpawnDebris(info);
        DropRaidLoot();

        if (_destroyIntactObject && _intactRoot != null && _intactRoot != gameObject)
            Destroy(_intactRoot);
    }

    void DropRaidLoot()
    {
        var castle = GetComponentInParent<SquareCastle>();
        if (castle != null && castle.IsPlayerOwned)
            return;

        var root = GetComponent<CastleModuleRoot>() ?? GetComponentInParent<CastleModuleRoot>();
        LootDrop.Module(root != null ? root.Kind : CastleModuleKind.Wall, transform.position);
    }

    void HideIntact()
    {
        GameObject root = _intactRoot != null ? _intactRoot : gameObject;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        var bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            var rb = bodies[i];
            if (rb == null)
                continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    void SpawnDebris(in DamageInfo info)
    {
        if (_debrisPrefab == null)
            return;

        Transform parent = _debrisSpawnParent;
        var instance = Instantiate(_debrisPrefab, transform.position, transform.rotation, parent);
        ApplyDebrisScale(instance.transform);

        Vector3 pushDir = info.Direction.sqrMagnitude > 0.0001f
            ? info.Direction.normalized
            : (Random.onUnitSphere + Vector3.up * 0.4f).normalized;

        if (_debrisImpulse <= 0f)
            return;

        var bodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            if (body == null)
                continue;
            if (body.isKinematic)
            {
                body.isKinematic = false;
                body.useGravity = true;
            }

            body.AddForce(pushDir * _debrisImpulse, ForceMode.Impulse);
            body.AddTorque(Random.onUnitSphere * (_debrisImpulse * 0.08f), ForceMode.Impulse);
        }
    }

    void ApplyDebrisScale(Transform instance)
    {
        if (!_matchDebrisWorldScale)
            return;

        Vector3 target = transform.lossyScale;
        if (instance.parent == null)
        {
            instance.localScale = target;
            return;
        }

        Vector3 p = instance.parent.lossyScale;
        instance.localScale = new Vector3(
            SafeDiv(target.x, p.x),
            SafeDiv(target.y, p.y),
            SafeDiv(target.z, p.z));
    }

    static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;

#if UNITY_EDITOR
    void OnValidate()
    {
        _maxHealth = Mathf.Max(1f, _maxHealth);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = _destroyed ? Color.gray : new Color(0.9f, 0.35f, 0.2f, 0.85f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, new Vector3(1.2f, 3f, 2.5f));
    }
#endif
}
