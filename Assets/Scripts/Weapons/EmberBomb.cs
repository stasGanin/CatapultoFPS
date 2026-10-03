using UnityEngine;

/// <summary>Arcing ember bomb. Used by bombardier enemies and the player ember launcher.</summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class EmberBomb : MonoBehaviour
{
    float _damage;
    float _radius;
    bool _spent;
    bool _hurtPlayer;
    GameObject _owner;

    // Подброс вверх у ручной пусковой — без него бомба игрока летит слишком плоско.
    const float LauncherUpKick = 3.2f;

    public static EmberBomb Spawn(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float damage,
        float radius,
        GameObject owner,
        bool hurtPlayer)
    {
        Vector3 velocity = direction.normalized * speed + Vector3.up * LauncherUpKick;
        return SpawnWithVelocity(origin, velocity, damage, radius, owner, hurtPlayer);
    }

    /// <summary>Exact launch velocity — for enemies that solve the ballistic arc themselves.</summary>
    public static EmberBomb SpawnWithVelocity(
        Vector3 origin,
        Vector3 velocity,
        float damage,
        float radius,
        GameObject owner,
        bool hurtPlayer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EmberBomb";
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * 0.28f;
        Object.Destroy(go.GetComponent<Collider>());

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        col.isTrigger = true;

        var body = go.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.mass = 0.8f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = velocity;

        if (owner != null)
        {
            var cc = owner.GetComponent<CharacterController>();
            if (cc != null)
                Physics.IgnoreCollision(col, cc, true);
            foreach (var ownerCol in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(col, ownerCol, true);
        }

        var rend = go.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null && rend != null)
            rend.sharedMaterial = new Material(shader);
        if (rend != null)
        {
            var block = new MaterialPropertyBlock();
            Color c = new Color(1f, 0.45f, 0.12f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            block.SetColor("_EmissionColor", c * 1.6f);
            rend.SetPropertyBlock(block);
        }

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.15f);
        light.range = 4.5f;
        light.intensity = 2.4f;
        light.shadows = LightShadows.None;

        var bomb = go.AddComponent<EmberBomb>();
        bomb._damage = damage;
        bomb._radius = radius;
        bomb._owner = owner;
        bomb._hurtPlayer = hurtPlayer;
        Object.Destroy(go, 6f);
        return bomb;
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
        // Вражеская бомба пролетает сквозь своих, а не рвётся о соседа по отряду.
        if (_hurtPlayer && EnemySenses.IsEnemy(other))
            return;
        Explode(transform.position);
    }

    void Explode(Vector3 point)
    {
        if (_spent)
            return;
        _spent = true;
        HitSparkVfx.PlayDust(point, Vector3.up, 14);
        // Вражеская бомба бьёт игрока и его стены, но не своих; бомба игрока — наоборот.
        DamageUtility.ApplyInRadius(point, _radius, _damage, Vector3.up, _owner, fromPlayer: !_hurtPlayer);
        Destroy(gameObject);
    }
}
