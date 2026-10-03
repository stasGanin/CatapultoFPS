using UnityEngine;

/// <summary>
/// Witch-bolt: flies almost straight with a small cosmetic corkscrew and brief early homing.
/// Visual is Hovl Toon Projectile 4 (flight / muzzle / hit).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyProjectile : MonoBehaviour
{
    // Спираль стартует с нуля и остаётся малой: раньше болт на первом кадре прыгал на 1.15 м вбок
    // и цеплял косяки окон/дверей, хотя линия прицела была чистой.
    const float SpiralRadius = 0.3f;
    const float SpiralRamp = 6f;
    const float SpiralDecay = 1.4f;
    const float SpiralHz = 1.6f;
    // Доводка только в начале полёта: болт не огибает углы за спрятавшимся игроком.
    const float HomeStart = 0.12f;
    const float HomeEnd = 0.65f;
    const float HomeRate = 2.2f;
    const float Lifetime = 5f;
    const float MinSpeed = 4f;
    const string VisualsResource = "Enemies/EnemyBoltVisuals";

    float _damage;
    float _speed;
    float _age;
    float _spinSign = 1f;
    bool _spent;
    bool _homeOnPlayer;
    GameObject _owner;
    Vector3 _heading;
    Vector3 _right;
    Vector3 _up;
    Vector3 _spine;
    Rigidbody _body;
    GameObject _impactPrefab;

    public static EnemyProjectile Spawn(
        Vector3 origin, Vector3 direction, float speed, float damage, GameObject owner, bool homeOnPlayer)
    {
        Vector3 heading = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        var visuals = Resources.Load<EnemyBoltVisuals>(VisualsResource);
        GameObject flight = visuals != null ? visuals.Flight : null;
        GameObject go = ProjectileVfx.SpawnPackProjectile(flight, origin, heading);
        go.name = "EnemyBolt";
        ProjectileVfx.IgnoreOwner(go, owner);

        float clampedSpeed = Mathf.Max(MinSpeed, speed);
        Rigidbody body = ProjectileVfx.DriveBody(go, heading, clampedSpeed);
        var bolt = go.AddComponent<EnemyProjectile>();
        bolt._damage = damage;
        bolt._speed = clampedSpeed;
        bolt._owner = owner;
        bolt._homeOnPlayer = homeOnPlayer;
        bolt._body = body;
        bolt._spinSign = Random.value < 0.5f ? 1f : -1f;
        bolt._spine = go.transform.position;
        bolt._heading = heading;
        bolt.RebuildBasis();
        if (visuals != null)
        {
            bolt._impactPrefab = visuals.Impact;
            ProjectileVfx.SpawnMuzzleFlash(visuals.Muzzle, null, origin, heading);
        }

        Object.Destroy(go, Lifetime);
        return bolt;
    }

    void FixedUpdate()
    {
        if (_spent || _body == null)
            return;

        float dt = Time.fixedDeltaTime;
        _age += dt;

        if (_homeOnPlayer && _age >= HomeStart && _age <= HomeEnd && EnemySenses.Player != null)
        {
            Vector3 want = EnemySenses.PlayerAimPoint() - _spine;
            if (want.sqrMagnitude > 0.04f)
            {
                _heading = Vector3.Slerp(_heading, want.normalized, 1f - Mathf.Exp(-HomeRate * dt)).normalized;
                RebuildBasis();
            }
        }

        _spine += _heading * (_speed * dt);

        float radius = SpiralRadius * (1f - Mathf.Exp(-_age * SpiralRamp)) * Mathf.Exp(-_age * SpiralDecay);
        float angle = _age * SpiralHz * Mathf.PI * 2f * _spinSign;
        Vector3 offset = (_right * Mathf.Cos(angle) + _up * Mathf.Sin(angle)) * radius;
        Vector3 next = _spine + offset;

        Vector3 delta = next - _body.position;
        if (delta.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(delta.normalized);
        _body.linearVelocity = delta / Mathf.Max(1e-5f, dt);
    }

    /// <summary>Спираль строится вокруг направления полёта, а не в горизонтальной плоскости.</summary>
    void RebuildBasis()
    {
        _right = Vector3.Cross(Vector3.up, _heading);
        if (_right.sqrMagnitude < 0.01f)
            _right = Vector3.right;
        _right.Normalize();
        _up = Vector3.Cross(_heading, _right).normalized;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_spent || other == null)
            return;
        if (DamageUtility.ProjectileShouldIgnore(other))
            return;
        if (_owner != null && other.transform.IsChildOf(_owner.transform))
            return;
        // Вражеские замки и юниты для своих болтов прозрачны: иначе болт рвётся о родную стену.
        if (EnemySenses.IsEnemy(other) || other.GetComponentInParent<EnemyCastle>() != null)
            return;
        if (other.isTrigger)
            return;

        Vector3 normal = (transform.position - other.bounds.center).normalized;
        DamageUtility.ApplyEnemyHit(other, _damage, transform.position, normal, _heading);
        Detonate(other);
    }

    void Detonate(Collider hit)
    {
        if (_spent)
            return;
        _spent = true;
        Vector3 n = hit != null
            ? transform.position - hit.bounds.center
            : -_heading;
        if (n.sqrMagnitude < 0.0001f)
            n = -_heading;
        else
            n.Normalize();
        ProjectileVfx.ReleaseTrails(gameObject);
        ProjectileVfx.SpawnImpact(_impactPrefab, transform.position, n, hit != null ? hit.transform : null);
        Destroy(gameObject);
    }
}
