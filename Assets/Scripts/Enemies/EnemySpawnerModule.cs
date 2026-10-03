using UnityEngine;

/// <summary>
/// Monolithic enemy-castle module: one cube, destroyed as a whole, drops metal.
/// Spawns capsule enemies around itself while alive.
/// </summary>
public sealed class EnemySpawnerModule : MonoBehaviour, IDamageable
{
    [SerializeField] float _maxHealth = 80f;
    [SerializeField] float _spawnInterval = 3.5f;
    [SerializeField] int _maxAlive = 4;
    [SerializeField] float _spawnRadius = 2.2f;
    [SerializeField] int _metalDrop = 8;
    [SerializeField] ItemDefinition _metalItem;
    [SerializeField] Color _color = new Color(0.35f, 0.38f, 0.45f);

    float _health;
    float _nextSpawn;
    bool _dead;
    readonly System.Collections.Generic.List<EnemyUnit> _alive = new();

    public static EnemySpawnerModule Spawn(Vector3 position, ItemDefinition metalItem)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "EnemySpawner";
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);

        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var module = go.AddComponent<EnemySpawnerModule>();
        module._metalItem = metalItem;
        module.ApplyColor();
        return module;
    }

    void Awake()
    {
        _health = _maxHealth;
        _nextSpawn = Time.time + 1f;
        if (_metalItem == null)
            _metalItem = Resources.Load<ItemDefinition>("Items/MetalItem");
        ApplyColor();
    }

    void Update()
    {
        if (_dead)
            return;

        PruneDead();
        if (_alive.Count >= _maxAlive || Time.time < _nextSpawn)
            return;

        _nextSpawn = Time.time + _spawnInterval;
        SpawnOne();
    }

    void SpawnOne()
    {
        Vector2 ring = Random.insideUnitCircle.normalized * _spawnRadius;
        Vector3 pos = transform.position + new Vector3(ring.x, 0f, ring.y);
        pos.y = transform.position.y - transform.localScale.y * 0.5f + 1f;

        var enemy = EnemyUnit.Spawn(pos);
        _alive.Add(enemy);
    }

    void PruneDead()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
                _alive.RemoveAt(i);
        }
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_dead || amount <= 0f)
            return;

        _health -= amount;
        Flash();

        if (_health > 0f)
            return;

        _dead = true;
        DropMetal(info.Point);
        Destroy(gameObject);
    }

    void DropMetal(Vector3 point)
    {
        if (_metalItem == null)
            _metalItem = Resources.Load<ItemDefinition>("Items/MetalItem");
        if (_metalItem == null)
            return;

        Vector3 spawnAt = point.sqrMagnitude > 0.01f ? point : transform.position + Vector3.up * 0.5f;
        WorldLootPickup.Spawn(_metalItem, Mathf.Max(1, _metalDrop), spawnAt);
    }

    void Flash()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", Color.Lerp(_color, Color.white, 0.55f));
        block.SetColor("_Color", Color.Lerp(_color, Color.white, 0.55f));
        renderer.SetPropertyBlock(block);
        CancelInvoke(nameof(ApplyColor));
        Invoke(nameof(ApplyColor), 0.08f);
    }

    void ApplyColor()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null && renderer.sharedMaterial == null)
            renderer.sharedMaterial = new Material(shader);
        else if (shader != null && renderer.sharedMaterial.shader != shader)
            renderer.sharedMaterial = new Material(shader);

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", _color);
        block.SetColor("_Color", _color);
        renderer.SetPropertyBlock(block);
    }
}
