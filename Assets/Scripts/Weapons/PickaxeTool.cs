using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Кирка: сначала обломки (камень), иначе стена (откалывает кубики).
/// </summary>
public class PickaxeTool : MonoBehaviour
{
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] Transform _viewModel;
    [SerializeField] ItemDefinition _stoneItem;
    [SerializeField] float _swingSpeed = 9f;
    [SerializeField] float _swingAngle = 55f;
    [SerializeField] float _mineRange = 6f;
    [SerializeField] float _hitInterval = 0.35f;
    [SerializeField] float _lootPickupRadius = 2f;
    [SerializeField] int _damage = 5;
    [SerializeField] int _stonePerDebris = 1;
    [Tooltip("Бонус кирки к дропу с мусора и деревьев, % (100 = обычно).")]
    [SerializeField, Min(1)] int _lootPercent = 200;

    [Header("Heavy swing (RMB)")]
    [Tooltip("Урон по площади; примерно в 1.5 раза больше ядра пушки.")]
    [SerializeField, Min(0f)] float _heavyDamage = 440f;
    [SerializeField, Min(0.5f)] float _heavyRadius = 3f;
    [SerializeField, Min(0.5f)] float _heavyCooldown = 6f;

    Quaternion _restLocalRotation;
    float _swingPhase;
    float _nextHitTime;
    CharacterController _selfController;
    bool _equipped;
    float _heavyReadyTime;
    readonly HashSet<MineableDebris> _minedScratch = new HashSet<MineableDebris>();

    public bool IsEquipped => _equipped;
    /// <summary>Заряд сильного удара 0..1; 1 = можно бить.</summary>
    public float HeavySwingCharge => Mathf.Clamp01(1f - (_heavyReadyTime - Time.time) / _heavyCooldown);

    float LootMultiplier => _lootPercent / 100f;

    public GameObject ViewModelObject => _viewModel != null ? _viewModel.gameObject : null;

    public void SetEquipped(bool equipped)
    {
        _equipped = equipped;
        if (!equipped)
        {
            _swingPhase = 0f;
            if (_viewModel != null)
                _viewModel.localRotation = _restLocalRotation;
        }
    }

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        _selfController = GetComponent<CharacterController>();
        if (_viewModel == null)
            EnsurePickaxeView();

        if (_viewModel != null)
            _restLocalRotation = _viewModel.localRotation;
    }

    void Update()
    {
        if (!_equipped || _input == null)
            return;
        if (_inventory != null && _inventory.BlocksGameplayInput)
            return;

        if (_viewModel != null)
        {
            if (_input.AttackHeld)
            {
                _swingPhase += Time.deltaTime * _swingSpeed;
                float wave = Mathf.Sin(_swingPhase);
                float t = wave * wave;
                float signed = wave >= 0f ? t : -t;
                _viewModel.localRotation = _restLocalRotation * Quaternion.Euler(signed * _swingAngle, 0f, signed * 12f);
            }
            else
            {
                _swingPhase = 0f;
                _viewModel.localRotation = Quaternion.Slerp(
                    _viewModel.localRotation,
                    _restLocalRotation,
                    1f - Mathf.Exp(-14f * Time.deltaTime));
            }
        }

        if (_input.SecondaryPressed)
            TryHeavySwing();

        bool wantHit = _input.AttackPressed || (_input.AttackHeld && Time.time >= _nextHitTime);
        if (!wantHit)
            return;

        _nextHitTime = Time.time + _hitInterval;
        TryMineHit();
    }

    void TryMineHit()
    {
        if (TryRaycastTarget(out RaycastHit hit))
            ApplyMineHit(hit);
    }

    void TryHeavySwing()
    {
        if (Time.time < _heavyReadyTime)
            return;

        // Удар в пустоту не должен тратить перезарядку.
        if (!TryRaycastTarget(out RaycastHit hit))
            return;

        _heavyReadyTime = Time.time + _heavyCooldown;
        MineDebrisInRadius(hit.point);
        // Своего игрока исключаем из радиуса; враги и стены получают урон как от взрыва.
        DamageUtility.ApplyInRadius(hit.point, _heavyRadius, _heavyDamage, hit.normal, gameObject);
    }

    /// <summary>Обломки в радиусе добываются как киркой (камень + бонус), а не просто разбиваются.</summary>
    void MineDebrisInRadius(Vector3 center)
    {
        var colliders = Physics.OverlapSphere(center, _heavyRadius, ~0, QueryTriggerInteraction.Ignore);
        _minedScratch.Clear();
        for (int i = 0; i < colliders.Length; i++)
        {
            var debris = colliders[i].GetComponentInParent<MineableDebris>();
            if (debris == null || !_minedScratch.Add(debris))
                continue;

            Vector3 point = DamageUtility.ClosestPoint(colliders[i], center);
            debris.TryMine(Mathf.CeilToInt(_heavyDamage), _stoneItem, _stonePerDebris, _lootPickupRadius,
                point, (point - center).normalized, LootMultiplier);
        }
    }

    bool TryRaycastTarget(out RaycastHit hit)
    {
        hit = default;
        if (_camera == null)
            return false;

        // Строго центр экрана / прицел → вперёд
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Первый «чужой» хит вдоль луча (пропускаем только себя)
        float traveled = 0f;
        Vector3 origin = ray.origin;
        const float skin = 0.01f;
        bool found = false;

        while (traveled < _mineRange)
        {
            float remain = _mineRange - traveled;
            if (!Physics.Raycast(origin, ray.direction, out hit, remain, ~0, QueryTriggerInteraction.Ignore))
                break;

            traveled += hit.distance + skin;
            origin = hit.point + ray.direction * skin;

            if (IsOwnCollider(hit.collider))
                continue;

            found = true;
            break;
        }

        return found;
    }

    void ApplyMineHit(RaycastHit hit)
    {
        var tree = hit.collider.GetComponentInParent<HarvestableTree>();
        if (tree != null)
        {
            tree.TryChop(_damage, hit.point, hit.normal, LootMultiplier);
            return;
        }

        MineableDebris debris = hit.collider.GetComponentInParent<MineableDebris>();
        if (debris != null)
        {
            debris.TryMine(_damage, _stoneItem, _stonePerDebris, _lootPickupRadius, hit.point, hit.normal, LootMultiplier);
            return;
        }

        var moduleBreakable = hit.collider.GetComponentInParent<CastleModuleBreakable>();
        if (moduleBreakable != null && !moduleBreakable.IsDestroyed)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            moduleBreakable.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var mage = hit.collider.GetComponentInParent<EnemyCastleMage>();
        if (mage != null)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            mage.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var chunk = hit.collider.GetComponentInParent<CastleWallChunk>();
        if (chunk != null && !chunk.IsDetached)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            var hostWall = chunk.GetComponentInParent<PreSlicedCastleWall>();
            if (hostWall != null && chunk.GetComponentInParent<CastleModuleRoot>() == null)
                hostWall.ApplyPickaxeHit(hit.point, hit.normal, _damage);
            else
                chunk.ApplyHit(_damage, hit.point, hit.normal);
            return;
        }

        var castleSpawner = hit.collider.GetComponentInParent<EnemyCastleSpawnModule>();
        if (castleSpawner != null)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            castleSpawner.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var spawner = hit.collider.GetComponentInParent<EnemySpawnerModule>();
        if (spawner != null)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            spawner.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var roller = hit.collider.GetComponentInParent<EnemyRoller>();
        if (roller != null)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            roller.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var enemy = hit.collider.GetComponentInParent<EnemyUnit>();
        if (enemy != null)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            enemy.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var other = hit.collider.GetComponentInParent<IDamageable>();
        if (other != null && !(other is PlayerHealth))
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            other.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        var segment = hit.collider.GetComponentInParent<Catapulto.Battle.Castle.CastleSegmentView>();
        if (segment != null)
        {
            HitSparkVfx.Play(hit.point, hit.normal);
            segment.ApplyDamage(_damage, new DamageInfo(hit.point, hit.normal, -hit.normal));
            return;
        }

        CastleWall wall = hit.collider.GetComponentInParent<CastleWall>();
        if (wall == null)
            return;

        HitSparkVfx.Play(hit.point, hit.normal);
        wall.ApplyPickaxeHit(hit.point, hit.normal, _damage, out _);
    }

    bool IsOwnCollider(Collider col)
    {
        if (col == null)
            return true;
        if (_selfController != null && ReferenceEquals(col, _selfController))
            return true;
        Transform t = col.transform;
        if (t == transform)
            return true;
        return t.IsChildOf(transform) && t != transform;
    }

    void EnsurePickaxeView()
    {
        Camera cam = _camera != null ? _camera : GetComponentInChildren<Camera>();
        if (cam == null)
            return;

        Transform cameraPivot = cam.transform;
        Transform existing = cameraPivot.Find("Pickaxe");
        if (existing != null)
        {
            _viewModel = existing;
            return;
        }

        var root = new GameObject("Pickaxe");
        root.transform.SetParent(cameraPivot, false);
        root.transform.localPosition = new Vector3(0.35f, -0.28f, 0.45f);
        root.transform.localRotation = Quaternion.Euler(8f, -20f, -25f);
        root.transform.localScale = Vector3.one;

        var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle";
        handle.transform.SetParent(root.transform, false);
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        handle.transform.localScale = new Vector3(0.035f, 0.22f, 0.035f);
        Object.Destroy(handle.GetComponent<Collider>());
        ApplyColor(handle, new Color(0.35f, 0.22f, 0.12f));

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0.02f, 0.2f, 0f);
        head.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        head.transform.localScale = new Vector3(0.22f, 0.05f, 0.05f);
        Object.Destroy(head.GetComponent<Collider>());
        ApplyColor(head, new Color(0.45f, 0.48f, 0.52f));

        _viewModel = root.transform;
        _restLocalRotation = _viewModel.localRotation;
        root.SetActive(false);
    }

    static void ApplyColor(GameObject target, Color color)
    {
        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
