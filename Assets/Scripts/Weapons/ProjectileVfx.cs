using UnityEngine;

/// <summary>
/// Hovl Toon Projectiles: the prefab IS the shot (particles + trail + light).
/// Demo usage is Instantiate(prefab, firePoint.position, firePoint.rotation).
/// We only strip ProjectileMover so our gameplay scripts own motion and hits.
/// </summary>
public static class ProjectileVfx
{
    const float MuzzleFlashLifetime = 0.45f;
    const float ImpactLifetime = 1.4f;
    const float TrailLinger = 1.6f;

    public static Transform CreateUnscaledAnchor(Transform physicsParent)
    {
        var anchor = new GameObject("Vfx").transform;
        anchor.SetParent(physicsParent, false);
        float parentScale = physicsParent.localScale.x;
        if (Mathf.Abs(parentScale) > 1e-4f)
            anchor.localScale = Vector3.one / parentScale;
        return anchor;
    }

    /// <summary>Spawn the pack prefab as the flying projectile.</summary>
    public static GameObject SpawnPackProjectile(GameObject prefab, Vector3 origin, Vector3 heading, float scale = 1f)
    {
        Quaternion rot = heading.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(heading.normalized)
            : Quaternion.identity;

        GameObject go;
        if (prefab != null)
        {
            var hide = new GameObject("PackSpawnHide");
            hide.SetActive(false);
            go = Object.Instantiate(prefab, hide.transform, false);
            StripHovlDemoScripts(go, immediate: true);
            go.transform.SetParent(null, false);
            Object.Destroy(hide);
            go.SetActive(true);
        }
        else
        {
            go = new GameObject("Projectile");
        }

        go.transform.SetPositionAndRotation(origin, rot);
        go.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        EnsureTriggerCollider(go);
        return go;
    }

    public static Rigidbody DriveBody(GameObject go, Vector3 heading, float speed)
    {
        var body = go.GetComponent<Rigidbody>();
        if (body == null)
            body = go.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = heading.normalized * speed;
        return body;
    }

    public static void IgnoreOwner(GameObject projectile, GameObject owner)
    {
        if (projectile == null || owner == null)
            return;

        var cols = projectile.GetComponentsInChildren<Collider>(true);
        var cc = owner.GetComponent<CharacterController>();
        var ownerCols = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cc != null)
                Physics.IgnoreCollision(cols[i], cc, true);
            for (int j = 0; j < ownerCols.Length; j++)
            {
                if (ownerCols[j] != null)
                    Physics.IgnoreCollision(cols[i], ownerCols[j], true);
            }
        }
    }

    public static void AttachFlight(Transform parent, GameObject prefab, Vector3 worldForward, float worldScale = 1f)
    {
        if (parent == null || prefab == null)
            return;

        var hide = new GameObject("VfxSpawnHide");
        hide.SetActive(false);
        GameObject visual = Object.Instantiate(prefab, hide.transform, false);
        visual.name = "FlightVfx";
        StripAllScripts(visual, immediate: true);
        StripPhysics(visual, immediate: true);

        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * Mathf.Max(0.1f, worldScale);
        Object.Destroy(hide);
        visual.SetActive(true);
    }

    public static void SpawnImpact(GameObject prefab, Vector3 point, Vector3 normal, Transform attachTo)
    {
        if (prefab == null)
            return;

        Vector3 n = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
        GameObject fx = Object.Instantiate(prefab, point, Quaternion.LookRotation(n));
        fx.name = "ImpactVfx";
        if (attachTo != null)
            fx.transform.SetParent(attachTo, true);
        StripAllScripts(fx, immediate: false);
        StripPhysics(fx, immediate: false);
        Object.Destroy(fx, ImpactLifetime);
    }

    public static void SpawnMuzzleFlash(GameObject prefab, Transform muzzle, Vector3 origin, Vector3 forward)
    {
        if (prefab == null)
            return;

        Quaternion rot = forward.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(forward)
            : Quaternion.identity;
        GameObject fx;
        if (muzzle != null)
        {
            fx = Object.Instantiate(prefab, muzzle.position, muzzle.rotation);
        }
        else
        {
            fx = Object.Instantiate(prefab, origin, rot);
        }

        fx.name = "MuzzleFlash";
        StripAllScripts(fx, immediate: false);
        StripPhysics(fx, immediate: false);
        Object.Destroy(fx, MuzzleFlashLifetime);
    }

    /// <summary>Pack trails are child particle objects; unparent so they linger after the shot dies.</summary>
    public static void ReleaseTrails(GameObject root)
    {
        if (root == null)
            return;
        Transform t = root.transform;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform child = t.GetChild(i);
            if (child.GetComponent<ParticleSystem>() == null &&
                child.GetComponentInChildren<ParticleSystem>() == null)
                continue;
            child.SetParent(null, true);
            Object.Destroy(child.gameObject, TrailLinger);
        }
    }

    static void EnsureTriggerCollider(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider>(true);
        if (cols.Length == 0)
        {
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.12f;
            col.isTrigger = true;
            return;
        }

        for (int i = 0; i < cols.Length; i++)
            cols[i].isTrigger = true;
    }

    static void StripAllScripts(GameObject root, bool immediate)
    {
        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            if (behaviours[i] != null)
                Drop(behaviours[i], immediate);
        }
    }

    static void StripHovlDemoScripts(GameObject root, bool immediate)
    {
        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null)
                continue;
            if (mb is ProjectileMover || mb is AutoDestroyPS)
                Drop(mb, immediate);
        }
    }

    static void StripPhysics(GameObject root, bool immediate)
    {
        var bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = bodies.Length - 1; i >= 0; i--)
        {
            if (bodies[i] == null)
                continue;
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
            Drop(bodies[i], immediate);
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = colliders.Length - 1; i >= 0; i--)
        {
            if (colliders[i] == null)
                continue;
            colliders[i].enabled = false;
            Drop(colliders[i], immediate);
        }
    }

    static void Drop(Object obj, bool immediate)
    {
        if (immediate)
            Object.DestroyImmediate(obj);
        else
            Object.Destroy(obj);
    }
}
