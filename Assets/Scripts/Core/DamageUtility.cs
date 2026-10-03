using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared hit resolve for projectiles against damageable targets.</summary>
public static class DamageUtility
{
    static readonly Collider[] OverlapScratch = new Collider[32];
    static readonly HashSet<int> HitIds = new HashSet<int>();

    public static bool ProjectileShouldIgnore(Collider other)
    {
        if (other == null)
            return true;
        if (other.GetComponentInParent<WorldLootPickup>() != null)
            return true;
        if (other.GetComponentInParent<CrossbowBolt>() != null)
            return true;
        if (other.GetComponentInParent<HomingMagicMissile>() != null)
            return true;
        if (other.GetComponentInParent<EnemyProjectile>() != null)
            return true;
        if (other.GetComponentInParent<Cannonball>() != null)
            return true;
        if (other.GetComponentInParent<EmberBomb>() != null)
            return true;
        if (other.GetComponentInParent<ScatterPellet>() != null)
            return true;
        if (other.GetComponentInParent<FurnitureSlot>() != null)
            return true;
        return false;
    }

    public static void ApplyToCollider(
        Collider col, float amount, Vector3 point, Vector3 normal, Vector3 direction, bool fromPlayer = true,
        Vector3? numberPoint = null)
    {
        if (col == null || amount <= 0f)
            return;

        // У взрыва точка удара общая для всех целей: цифры привязываем к самой цели, чтобы не слипались.
        Vector3 labelAt = numberPoint ?? point;
        if (fromPlayer)
            amount *= PlayerTalents.Multiplier(TalentStat.Damage);

        var chunk = col.GetComponentInParent<CastleWallChunk>();
        if (chunk != null && !chunk.IsDetached)
        {
            chunk.ApplyHit(Mathf.CeilToInt(amount), point, normal, fromPlayer);
            if (fromPlayer)
                DamageNumbers.Spawn(labelAt, Mathf.CeilToInt(amount));
            return;
        }

        var damageable = col.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;
        damageable.ApplyDamage(amount, new DamageInfo(point, normal, direction, fromPlayer));
        if (fromPlayer)
            DamageNumbers.Spawn(labelAt, amount);

        // Хитмаркер только по живым целям: попадание в стену не должно ощущаться как «попал».
        if (fromPlayer && !(damageable is CarcassWallBreakable) && !(damageable is CastleModuleBreakable)
            && !(damageable is MineableDebris))
            CombatFeedback.RaiseHitConfirmed(point);
    }

    /// <summary>Enemy-side hit: damages the player and player castle walls, never enemy castles.</summary>
    public static void ApplyEnemyHit(Collider col, float amount, Vector3 point, Vector3 normal, Vector3 direction)
    {
        ApplyToCollider(col, amount, point, normal, direction, fromPlayer: false);
    }

    /// <summary>
    /// ClosestPoint only works on primitives and convex meshes; castle art is concave.
    /// </summary>
    public static Vector3 ClosestPoint(Collider col, Vector3 world)
    {
        if (col == null)
            return world;
        if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
            return col.ClosestPoint(world);
        if (col is MeshCollider mesh && mesh.convex)
            return col.ClosestPoint(world);
        return col.ClosestPointOnBounds(world);
    }

    public static void ApplyInRadius(
        Vector3 point, float radius, float amount, Vector3 outwardHint, GameObject ignoreRoot = null,
        bool fromPlayer = true)
    {
        if (radius <= 0f || amount <= 0f)
            return;

        ExplosionEffect.Spawn(point, radius);

        HitIds.Clear();
        int count = Physics.OverlapSphereNonAlloc(point, radius, OverlapScratch, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var col = OverlapScratch[i];
            if (col == null)
                continue;
            if (ignoreRoot != null &&
                (col.transform == ignoreRoot.transform || col.transform.IsChildOf(ignoreRoot.transform)))
                continue;
            // Вражеские взрывы не задевают других врагов.
            if (!fromPlayer && EnemySenses.IsEnemy(col))
                continue;

            var chunk = col.GetComponentInParent<CastleWallChunk>();
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (chunk == null && damageable == null)
                continue;

            int id = chunk != null
                ? chunk.GetInstanceID()
                : (damageable as Component)?.GetInstanceID() ?? 0;
            if (id != 0 && !HitIds.Add(id))
                continue;

            Vector3 to = col.bounds.center - point;
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : outwardHint;
            ApplyToCollider(col, amount, point, -dir, dir, fromPlayer, ClosestPoint(col, point));
        }
    }
}
