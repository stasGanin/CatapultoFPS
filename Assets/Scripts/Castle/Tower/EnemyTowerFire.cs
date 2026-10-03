using UnityEngine;

/// <summary>
/// Автоогонь вражеской башни: бьёт по игроку, если виден, иначе по нашему замку.
/// Стреляет болтами врага (урон «не от игрока»), баланс — в Resources/Enemies/TowerConfig.
/// </summary>
public sealed class EnemyTowerFire : MonoBehaviour
{
    const string ConfigPath = "Enemies/TowerConfig";

    CastleTower _tower;
    EnemyConfig _config;
    float _nextFireTime;

    void Awake()
    {
        _tower = GetComponent<CastleTower>();
        _config = EnemyConfig.Load(ConfigPath);
        if (_tower == null || _config == null)
        {
            Debug.LogError("EnemyTowerFire: нет башни или Resources/Enemies/TowerConfig.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (!TryPickTarget(out Vector3 point))
            return;

        Vector3 origin = _tower.Muzzle.position;
        Vector3 direction = (point - origin).normalized;
        _tower.AimAt(direction);
        if (Time.time < _nextFireTime)
            return;

        _nextFireTime = Time.time + _config.RollCooldown();
        EnemyProjectile.Spawn(origin, direction, _config.ProjectileSpeed, _config.Damage, gameObject, homeOnPlayer: false);
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
