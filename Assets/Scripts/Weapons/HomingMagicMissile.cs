using UnityEngine;

/// <summary>Player staff missile: homes on a locked enemy or enemy-castle piece, stops on walls.</summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class HomingMagicMissile : MonoBehaviour
{
    const float HomeRate = 7.5f;
    const float Lifetime = 5f;

    float _damage;
    float _speed;
    bool _spent;
    GameObject _owner;
    Transform _target;
    Vector3 _heading;
    Rigidbody _body;
    GameObject _impactPrefab;

    public static HomingMagicMissile Spawn(
        Vector3 origin,
        Vector3 direction,
        Transform target,
        float speed,
        float radius,
        float damage,
        float lifetime,
        GameObject owner,
        GameObject flightVisual,
        GameObject impactVisual)
    {
        Vector3 heading = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        Vector3 spawn = origin + heading * 0.35f;
        GameObject go = ProjectileVfx.SpawnPackProjectile(flightVisual, spawn, heading, scale: 3.5f);
        go.name = "MagicMissile";

        var col = go.GetComponent<SphereCollider>();
        if (col != null)
            col.radius = Mathf.Max(col.radius, radius);
        ProjectileVfx.IgnoreOwner(go, owner);

        Rigidbody body = ProjectileVfx.DriveBody(go, heading, Mathf.Max(4f, speed));
        var missile = go.AddComponent<HomingMagicMissile>();
        missile._damage = damage;
        missile._speed = Mathf.Max(4f, speed);
        missile._owner = owner;
        missile._target = target;
        missile._body = body;
        missile._heading = heading;
        missile._impactPrefab = impactVisual;

        Object.Destroy(go, lifetime > 0.2f ? lifetime : Lifetime);
        return missile;
    }

    void FixedUpdate()
    {
        if (_spent || _body == null)
            return;

        float dt = Time.fixedDeltaTime;

        Vector3 want = AimPoint() - _body.position;
        if (want.sqrMagnitude > 0.04f)
            _heading = Vector3.Slerp(_heading, want.normalized, 1f - Mathf.Exp(-HomeRate * dt)).normalized;

        if (_heading.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(_heading);
        _body.linearVelocity = _heading * _speed;
    }

    Vector3 AimPoint()
    {
        if (_target == null)
            return _body.position + _heading;
        var col = _target.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;
        return _target.position + Vector3.up * 0.4f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_spent || other == null)
            return;
        if (DamageUtility.ProjectileShouldIgnore(other))
            return;
        if (_owner != null &&
            (other.transform == _owner.transform || other.transform.IsChildOf(_owner.transform)))
            return;
        if (other.GetComponentInParent<PlayerHealth>() != null)
            return;

        Vector3 point = DamageUtility.ClosestPoint(other, transform.position);
        Vector3 normal = transform.position - point;
        if (normal.sqrMagnitude < 0.0001f)
            normal = -_heading;
        else
            normal.Normalize();

        var chunk = other.GetComponentInParent<CastleWallChunk>();
        if (chunk != null && chunk.BelongsToPlayerCastle)
        {
            if (!other.isTrigger)
                Detonate(other, point, normal);
            return;
        }

        bool hitSomething = false;
        if (chunk != null && !chunk.IsDetached)
        {
            chunk.ApplyHit(Mathf.CeilToInt(_damage), point, normal, fromPlayer: true);
            hitSomething = true;
        }
        else if (StaffWeapon.IsHostileTarget(other))
        {
            DamageUtility.ApplyToCollider(other, _damage, point, normal, _heading);
            hitSomething = true;
        }
        else if (!other.isTrigger)
            hitSomething = true;

        if (hitSomething)
            Detonate(other, point, normal);
    }

    void Detonate(Collider hit, Vector3 point, Vector3 normal)
    {
        if (_spent)
            return;
        _spent = true;
        ProjectileVfx.ReleaseTrails(gameObject);
        ProjectileVfx.SpawnImpact(_impactPrefab, point, normal, hit != null ? hit.transform : null);
        Destroy(gameObject);
    }
}
