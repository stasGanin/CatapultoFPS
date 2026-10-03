using UnityEngine;

/// <summary>
/// Witch-bolt: corkscrew around the aim, then curves toward the player. Stops on walls.
/// Visual is Hovl Toon Projectile 4 (flight / muzzle / hit).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyProjectile : MonoBehaviour
{
    const float SpiralRadius = 1.15f;
    const float SpiralHz = 1.8f;
    const float SpiralDecay = 1.7f;
    const float HomeDelay = 0.32f;
    const float HomeRate = 3.4f;
    const float Lifetime = 5f;
    const string VisualsResource = "Enemies/EnemyBoltVisuals";

    float _damage = 1f;
    float _speed;
    float _age;
    float _spinSign = 1f;
    bool _spent;
    GameObject _owner;
    Transform _target;
    Vector3 _heading;
    Vector3 _right;
    Vector3 _planarForward;
    Vector3 _spine;
    Rigidbody _body;
    GameObject _impactPrefab;

    public static EnemyProjectile Spawn(
        Vector3 origin, Vector3 direction, float speed, float damage, GameObject owner, Transform homingTarget)
    {
        Vector3 heading = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        var visuals = Resources.Load<EnemyBoltVisuals>(VisualsResource);
        GameObject flight = visuals != null ? visuals.Flight : null;
        Vector3 spawn = origin + heading * 0.25f;
        GameObject go = ProjectileVfx.SpawnPackProjectile(flight, spawn, heading);
        go.name = "EnemyBolt";
        ProjectileVfx.IgnoreOwner(go, owner);

        Rigidbody body = ProjectileVfx.DriveBody(go, heading, Mathf.Max(4f, speed));
        var bolt = go.AddComponent<EnemyProjectile>();
        bolt._damage = damage;
        bolt._speed = Mathf.Max(4f, speed);
        bolt._owner = owner;
        bolt._target = homingTarget;
        bolt._body = body;
        bolt._spinSign = (Random.value < 0.5f) ? 1f : -1f;
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

        if (_target != null && _age >= HomeDelay)
        {
            Vector3 want = _target.position + Vector3.up * 1.05f - _spine;
            if (want.sqrMagnitude > 0.04f)
            {
                _heading = Vector3.Slerp(_heading, want.normalized, 1f - Mathf.Exp(-HomeRate * dt)).normalized;
                RebuildBasis();
            }
        }

        _spine += _heading * (_speed * dt);

        float spiral = SpiralRadius * Mathf.Exp(-_age * SpiralDecay);
        float ang = _age * SpiralHz * Mathf.PI * 2f * _spinSign;
        Vector3 offset = (_right * Mathf.Cos(ang) + _planarForward * Mathf.Sin(ang)) * spiral;
        Vector3 next = _spine + offset;

        Vector3 delta = next - _body.position;
        if (delta.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(delta.normalized);
        _body.linearVelocity = delta / Mathf.Max(1e-5f, dt);
    }

    void RebuildBasis()
    {
        Vector3 planar = _heading;
        planar.y = 0f;
        if (planar.sqrMagnitude < 0.01f)
            planar = Vector3.forward;
        _planarForward = planar.normalized;
        _right = Vector3.Cross(Vector3.up, _planarForward);
        if (_right.sqrMagnitude < 0.01f)
            _right = Vector3.right;
        else
            _right.Normalize();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_spent || other == null)
            return;
        if (DamageUtility.ProjectileShouldIgnore(other))
            return;
        if (_owner != null && (other.transform == _owner.transform || other.transform.IsChildOf(_owner.transform)))
            return;

        if (other.GetComponentInParent<EnemyUnit>() != null)
            return;
        if (other.GetComponentInParent<EnemyRoller>() != null)
            return;
        if (other.GetComponentInParent<EnemySpawnerModule>() != null)
            return;

        if (other.GetComponentInParent<EnemyCastleMage>() != null)
        {
            Detonate(other);
            return;
        }

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            Vector3 n = (transform.position - other.bounds.center).normalized;
            health.ApplyDamage(_damage, new DamageInfo(transform.position, n, _heading, fromPlayer: false));
            Detonate(other);
            return;
        }

        var chunk = other.GetComponentInParent<CastleWallChunk>();
        if (chunk != null && !chunk.IsDetached)
        {
            Vector3 n = (transform.position - other.bounds.center).normalized;
            chunk.ApplyHit(Mathf.CeilToInt(_damage), transform.position, n, fromPlayer: false);
            Detonate(other);
            return;
        }

        if (!other.isTrigger)
            Detonate(other);
    }

    void Detonate(Collider hit)
    {
        if (_spent)
            return;
        _spent = true;
        Vector3 n = hit != null
            ? (transform.position - hit.bounds.center)
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
