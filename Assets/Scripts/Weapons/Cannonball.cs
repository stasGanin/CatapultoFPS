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
    // > 0 — выстрел вражеской башни: бьёт игрока и его замок фиксированным уроном, чужие стены не рвёт.
    float _enemyDamage;
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

    /// <summary>
    /// Спавнит физическое ядро. Общий для ручной пушки и башни; <paramref name="ignored"/> — коллайдеры стрелка,
    /// о которые ядро не должно взрываться на старте.
    /// </summary>
    public static Cannonball Launch(WeaponConfig config, Vector3 origin, Vector3 direction, params Collider[] ignored)
    {
        float diameter = config.ProjectileRadius * 2f;

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Cannonball";
        ball.transform.position = origin + direction * (config.ProjectileRadius + 0.05f);
        ball.transform.rotation = Quaternion.LookRotation(direction);
        ball.transform.localScale = Vector3.one * diameter;

        var meshRenderer = ball.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = config.ProjectileVisual == null;

        SphereCollider ballCollider = ball.GetComponent<SphereCollider>();
        if (ballCollider != null)
        {
            ballCollider.isTrigger = true;
            for (int i = 0; i < ignored.Length; i++)
            {
                if (ignored[i] != null)
                    Physics.IgnoreCollision(ballCollider, ignored[i], true);
            }
        }

        Rigidbody body = ball.AddComponent<Rigidbody>();
        body.mass = config.ProjectileMass;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = direction * config.MuzzleSpeed;

        var projectile = ball.AddComponent<Cannonball>();
        projectile.Init(config.BlastRadius, config.ExplosionForce, config.ProjectileLifetime, config.ImpactVisual);

        var vfxAnchor = new GameObject("Vfx");
        vfxAnchor.transform.SetParent(ball.transform, false);
        float ballScale = ball.transform.localScale.x;
        if (ballScale > 1e-4f)
            vfxAnchor.transform.localScale = Vector3.one / ballScale;
        ProjectileVfx.AttachFlight(vfxAnchor.transform, config.ProjectileVisual, direction);

        if (config.ProjectileVisual == null)
            ApplyColor(ball, new Color(0.12f, 0.12f, 0.14f));
        return projectile;
    }

    public void MarkEnemyShot(float damage) => _enemyDamage = damage;

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

        if (_enemyDamage > 0f)
        {
            DamageUtility.ApplyInRadius(point, Mathf.Max(_blastRadius, 0.75f), _enemyDamage, outward, fromPlayer: false);
            ProjectileVfx.SpawnImpact(_impactVisual, point, outward, other.transform);
            Destroy(gameObject, 0.02f);
            return;
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
        Destroy(gameObject, 0.02f);
    }
}
