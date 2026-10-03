using UnityEngine;

/// <summary>
/// Автоогонь вражеской башни: бьёт по игроку, если виден, иначе по нашему замку.
/// Включается кнопкой F7 (<see cref="EnemySpawnDebugHud"/>). Стреляет ядрами (урон «не от игрока»): темп и урон — Enemies/TowerConfig, вид и скорость ядра — CannonTowerConfig.
/// </summary>
public sealed class EnemyTowerFire : MonoBehaviour
{
    const string ConfigPath = "Enemies/TowerConfig";
    const string ShotConfigPath = "Castle/Towers/CannonTowerConfig";

    CastleTower _tower;
    EnemyConfig _config;
    WeaponConfig _shotConfig;
    Collider[] _ownColliders;
    float _nextFireTime;

    void Awake()
    {
        _tower = GetComponent<CastleTower>();
        _config = EnemyConfig.Load(ConfigPath);
        _shotConfig = Resources.Load<WeaponConfig>(ShotConfigPath);
        if (_tower == null || _config == null || _shotConfig == null)
        {
            Debug.LogError("EnemyTowerFire: нет башни или Resources/Enemies/TowerConfig.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (!EnemySpawnDebugHud.TowersEnabled || !TryPickTarget(out Vector3 point))
            return;

        Vector3 origin = _tower.Muzzle.position;
        Vector3 direction = TowerBallistics.LobDirection(origin, point, _shotConfig.MuzzleSpeed);
        _tower.AimAt(direction);
        if (Time.time < _nextFireTime)
            return;

        _nextFireTime = Time.time + _config.RollCooldown();
        // Колайдеры своего замка игнорируем, иначе ядро взорвётся о зубцы у самой башни.
        _ownColliders ??= _tower.Castle.GetComponentsInChildren<Collider>();
        ProjectileVfx.SpawnMuzzleFlash(_shotConfig.MuzzleFlash, _tower.Muzzle, origin, direction);
        Cannonball.Launch(_shotConfig, origin, direction, _ownColliders).MarkEnemyShot(_config.Damage);
    }

    bool TryPickTarget(out Vector3 point)
    {
        point = default;
        Vector3 origin = _tower.Muzzle.position;
        float range = _config.AttackRange;

        Transform player = EnemySenses.Player;
        if (player != null)
        {
            Vector3 aim = EnemySenses.PlayerAimPoint();
            if ((aim - origin).sqrMagnitude <= range * range
                && EnemySenses.HasClearPath(origin, aim, 0.18f, _tower.Castle.transform, player))
            {
                point = aim;
                return true;
            }
        }

        SquareCastle castle = EnemySenses.PlayerCastle;
        if (castle == null)
            return false;
        Vector3 center = EnemySenses.CastleCenter(castle.transform);
        if ((center - origin).sqrMagnitude > range * range)
            return false;

        point = center;
        return true;
    }
}
