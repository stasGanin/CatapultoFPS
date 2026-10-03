using UnityEngine;

/// <summary>Player crossbow bolt — damages any IDamageable (enemies, spawner, segments).</summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class CrossbowBolt : MonoBehaviour
{
    float _damage;
    bool _spent;
    GameObject _owner;
    GameObject _impactVisual;

    public static CrossbowBolt Spawn(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float radius,
        float damage,
        float lifetime,
        GameObject owner,
        GameObject flightVisual,
        GameObject impactVisual)
    {
        var go = new GameObject("CrossbowBolt");
        go.transform.position = origin + direction.normalized * (radius + 0.05f);
        go.transform.rotation = Quaternion.LookRotation(direction);

        var col = go.AddComponent<SphereCollider>();
        col.radius = radius;
        col.isTrigger = true;
        if (owner != null)
        {
            var cc = owner.GetComponent<CharacterController>();
            if (cc != null)
                Physics.IgnoreCollision(col, cc, true);
            foreach (var ownerCol in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(col, ownerCol, true);
        }

        var body = go.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = direction.normalized * speed;

        var bolt = go.AddComponent<CrossbowBolt>();
        bolt._damage = damage;
        bolt._owner = owner;
        bolt._impactVisual = impactVisual;

        ProjectileVfx.AttachFlight(go.transform, flightVisual, direction);

        Object.Destroy(go, lifetime);
        return bolt;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_spent || other == null)
            return;
        if (DamageUtility.ProjectileShouldIgnore(other))
            return;
        if (_owner != null &&
            (other.transform == _owner.transform ||
             other.transform.IsChildOf(_owner.transform)))
            return;

        _spent = true;
        Vector3 point = DamageUtility.ClosestPoint(other, transform.position);
        Vector3 normal = transform.position - point;
        if (normal.sqrMagnitude < 0.0001f)
            normal = -transform.forward;
        else
            normal.Normalize();
        Vector3 dir = -normal;

        DamageUtility.ApplyToCollider(other, _damage, point, normal, dir);

        ProjectileVfx.SpawnImpact(_impactVisual, point, normal, other.transform);
        Destroy(gameObject);
    }
}
