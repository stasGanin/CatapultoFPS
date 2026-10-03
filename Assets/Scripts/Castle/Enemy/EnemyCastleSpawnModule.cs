using UnityEngine;

/// <summary>Wall-mounted enemy spawner for enemy castles.</summary>
public sealed class EnemyCastleSpawnModule : MonoBehaviour, IDamageable, ICastleModuleBehavior
{
    [SerializeField] float _maxHealth = 60f;
    [SerializeField] float _spawnInterval = 3f;
    [SerializeField] int _maxAlive = 3;

    float _health;
    float _nextSpawn;
    bool _dead;
    readonly System.Collections.Generic.List<MonoBehaviour> _alive = new();

    public bool IsOperational => !_dead;

    void Awake()
    {
        _health = _maxHealth;
        _nextSpawn = Time.time + 1.5f;
    }

    void Update()
    {
        if (_dead)
            return;

        Prune();
        if (_alive.Count >= _maxAlive || Time.time < _nextSpawn)
            return;
        if (HasGrowing())
            return;

        Transform socket = ResolveSocket();
        _nextSpawn = Time.time + _spawnInterval;
        _alive.Add(SpawnMinion(socket));
    }

    void Prune()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
                _alive.RemoveAt(i);
        }
    }

    public void OnModuleDestroyed()
    {
        _dead = true;
        enabled = false;
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i] != null && IsGrowing(_alive[i]))
                Destroy(_alive[i].gameObject);
        }
    }

    bool HasGrowing()
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i] != null && IsGrowing(_alive[i]))
                return true;
        }

        return false;
    }

    Transform ResolveSocket()
    {
        var module = GetComponent<CastleModuleRoot>();
        if (module != null && module.GolemSocket != null)
            return module.GolemSocket;
        return transform;
    }

    MonoBehaviour SpawnMinion(Transform socket)
    {
        int cycle = _alive.Count % 4;
        if (cycle == 0)
            return EnemyRoller.SpawnInSocket(socket, _spawnInterval);
        if (cycle == 1)
            return EnemyUnit.SpawnInSocket(socket, _spawnInterval);
        if (cycle == 2)
            return EnemyBrute.SpawnInSocket(socket, _spawnInterval);
        return EnemyBomber.SpawnInSocket(socket, _spawnInterval);
    }

    static bool IsGrowing(MonoBehaviour minion)
    {
        if (minion is EnemyRoller roller)
            return roller.IsGrowing;
        if (minion is EnemyUnit unit)
            return unit.IsGrowing;
        if (minion is EnemyBrute brute)
            return brute.IsGrowing;
        if (minion is EnemyBomber bomber)
            return bomber.IsGrowing;
        return false;
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        // Authored wall spawners break via chunks; runtime totems have no chunks and use HP.
        var authored = GetComponent<CastleModuleRoot>();
        if (authored != null && GetComponentInChildren<CastleWallChunk>(true) != null)
            return;

        var breakable = GetComponentInParent<CastleModuleBreakable>();
        if (breakable != null && !breakable.IsDestroyed)
        {
            breakable.ApplyDamage(amount, info);
            return;
        }

        if (_dead || amount <= 0f)
            return;
        _health -= amount;
        if (_health > 0f)
            return;
        OnModuleDestroyed();
        LootDrop.Module(CastleModuleKind.Spawner, transform.position);
        Destroy(gameObject);
    }
}
