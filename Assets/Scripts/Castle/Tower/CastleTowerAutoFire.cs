using UnityEngine;

/// <summary>
/// Автоогонь башни игрока: раз в 0.25 с выбирает ближайшего врага с чистой линией и стреляет ядром из сундуков.
/// Пока игрок сидит за пушкой, молчит — стреляет вручную.
/// </summary>
public sealed class CastleTowerAutoFire : MonoBehaviour
{
    const string ConfigPath = "Castle/Towers/CannonTowerConfig";
    const float ScanInterval = 0.25f;
    const int ScanBufferSize = 64;

    static readonly Collider[] Scratch = new Collider[ScanBufferSize];

    CastleTower _tower;
    WeaponConfig _config;
    float _nextScanTime;
    float _nextFireTime;
    bool _hasTarget;
    Vector3 _aimPoint;

    void Awake()
    {
        _tower = GetComponent<CastleTower>();
        _config = Resources.Load<WeaponConfig>(ConfigPath);
        if (_tower == null || _config == null)
        {
            Debug.LogError("CastleTowerAutoFire: нет башни или конфига CannonTowerConfig.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (TowerOperator.IsOperating)
            return;

        if (Time.time >= _nextScanTime)
        {
            _nextScanTime = Time.time + ScanInterval;
            _hasTarget = TryFindTarget(out _aimPoint);
        }

        if (!_hasTarget)
            return;

        Vector3 direction = TowerBallistics.LobDirection(_tower.Muzzle.position, _aimPoint, _config.MuzzleSpeed);
        _tower.AimAt(direction);
        if (Time.time < _nextFireTime || !CastleAmmoStorage.TryConsume(_tower.Castle))
            return;

        _nextFireTime = Time.time + _config.FireInterval;
        Vector3 origin = _tower.Muzzle.position;
        ProjectileVfx.SpawnMuzzleFlash(_config.MuzzleFlash, _tower.Muzzle, origin, direction);
        Cannonball.Launch(_config, origin, direction, _tower.GetComponent<Collider>());
    }

    bool TryFindTarget(out Vector3 point)
    {
        point = default;
        Vector3 origin = _tower.Muzzle.position;
        int count = Physics.OverlapSphereNonAlloc(origin, _config.AutoRange, Scratch, ~0, QueryTriggerInteraction.Ignore);
        float bestSqr = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider col = Scratch[i];
            Transform root = FindEnemyRoot(col);
            if (root == null)
                continue;

            Vector3 center = col.bounds.center;
            float sqr = (center - origin).sqrMagnitude;
            if (sqr >= bestSqr)
                continue;
            if (!EnemySenses.HasClearPath(origin, center, _config.ProjectileRadius, _tower.Castle.transform, root))
                continue;

            bestSqr = sqr;
            point = center;
            found = true;
        }

        return found;
    }

    /// <summary>Враги — юниты и маг вражеского замка: стены и модули автоогонь не расходует.</summary>
    static Transform FindEnemyRoot(Collider col)
    {
        if (col == null)
            return null;
        var mage = col.GetComponentInParent<EnemyCastleMage>();
        if (mage != null && !mage.IsDead)
            return mage.transform;
        if (!EnemySenses.IsEnemy(col))
            return null;
        return col.transform.root;
    }
}
