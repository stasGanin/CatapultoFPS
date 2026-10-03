using UnityEngine;

/// <summary>One pellet from the scattergun. Short-lived, no gravity.</summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class ScatterPellet : MonoBehaviour
{
    float _damage;
    bool _spent;
    GameObject _owner;

    public static void SpawnVolley(
        Vector3 origin,
        Vector3 forward,
        int count,
        float spreadDeg,
        float speed,
        float damage,
        float lifetime,
        GameObject owner)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.Euler(
                Random.Range(-spreadDeg, spreadDeg),
                Random.Range(-spreadDeg, spreadDeg),
                0f) * forward;
            SpawnOne(origin, dir, speed, damage, lifetime, owner);
        }
    }

    static void SpawnOne(Vector3 origin, Vector3 direction, float speed, float damage, float lifetime, GameObject owner)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ScatterPellet";
        go.transform.position = origin + direction.normalized * 0.2f;
        go.transform.localScale = Vector3.one * 0.07f;
        Object.Destroy(go.GetComponent<Collider>());

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        col.isTrigger = true;

        var body = go.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction.normalized * speed;

        if (owner != null)
        {
            var cc = owner.GetComponent<CharacterController>();
            if (cc != null)
                Physics.IgnoreCollision(col, cc, true);
        }

        var pellet = go.AddComponent<ScatterPellet>();
        pellet._damage = damage;
        pellet._owner = owner;
        Object.Destroy(go, lifetime);
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

        _spent = true;
        Vector3 point = DamageUtility.ClosestPoint(other, transform.position);
        Vector3 normal = (transform.position - point);
        if (normal.sqrMagnitude < 0.0001f)
            normal = -transform.forward;
        else
            normal.Normalize();
        DamageUtility.ApplyToCollider(other, _damage, point, normal, -normal);
        HitSparkVfx.Play(point, normal);
        Destroy(gameObject);
    }
}
