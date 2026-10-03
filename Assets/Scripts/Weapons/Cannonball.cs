using UnityEngine;

/// <summary>
/// Физическое ядро: на ударе отключает свой коллайдер и отрывает куски наружу (не внутрь стены).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Cannonball : MonoBehaviour
{
    float _blastRadius;
    float _explosionForce;
    float _lifetime;
    bool _detonated;
    Rigidbody _body;
    GameObject _impactVisual;

    public void Init(float blastRadius, float explosionForce, float lifetime, GameObject impactVisual)
    {
        _blastRadius = blastRadius;
        _explosionForce = explosionForce;
        _lifetime = lifetime;
        _impactVisual = impactVisual;
        _body = GetComponent<Rigidbody>();
        Destroy(gameObject, _lifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_detonated || other == null)
            return;
        if (DamageUtility.ProjectileShouldIgnore(other))
            return;

        _detonated = true;

        Vector3 point = DamageUtility.ClosestPoint(other, transform.position);
        Vector3 incoming = _body != null ? _body.linearVelocity : Vector3.zero;
        Vector3 outward = (transform.position - point);
        if (outward.sqrMagnitude < 0.0001f)
            outward = incoming.sqrMagnitude > 0.01f ? -incoming.normalized : Vector3.forward;
        else
            outward.Normalize();

        // Stop pushing the wall physically this frame
        var col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
        if (_body != null)
        {
            _body.detectCollisions = false;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _body.isKinematic = true;
        }

        var hitChunk = other.GetComponentInParent<CastleWallChunk>();
        if (hitChunk != null && !hitChunk.IsDetached && !hitChunk.BelongsToPlayerCastle)
            hitChunk.Detach(point, outward, Mathf.Max(1.2f, _explosionForce * 0.04f));

        var walls = FindObjectsByType<PreSlicedCastleWall>(FindObjectsSortMode.None);
        for (int i = 0; i < walls.Length; i++)
            walls[i].ApplyBlast(point, _blastRadius, _explosionForce, outward);

        var squares = FindObjectsByType<SquareCastle>(FindObjectsSortMode.None);
        for (int i = 0; i < squares.Length; i++)
            squares[i].ApplyBlast(point, _blastRadius, _explosionForce, outward);

        CastleWall wall = other.GetComponentInParent<CastleWall>();
        if (wall != null)
            wall.ApplyBlast(point, _blastRadius, _explosionForce, incoming);

        // Enemies / monolithic spawner modules (splash covers the direct hit too)
        float unitDamage = Mathf.Max(40f, _explosionForce * 0.45f);
        DamageUtility.ApplyInRadius(point, Mathf.Max(_blastRadius, 0.75f), unitDamage, outward);

        ProjectileVfx.SpawnImpact(_impactVisual, point, outward, other.transform);
        BlastRadiusFlash.Spawn(point, _blastRadius);
        Destroy(gameObject, 0.02f);
    }
}
