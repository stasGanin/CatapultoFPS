using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>AOE damage for cannon / explosions against segment walls.</summary>
    public static class CastleSegmentBlast
    {
        public static void Apply(Vector3 worldPoint, float radius, float damage, float explosionForce)
        {
            if (radius <= 0f || damage <= 0f)
                return;

            Collider[] hits = Physics.OverlapSphere(worldPoint, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                var seg = hits[i].GetComponentInParent<CastleSegmentView>();
                if (seg == null || seg.IsDestroyed)
                    continue;

                Vector3 closest = hits[i].ClosestPoint(worldPoint);
                float dist = Vector3.Distance(worldPoint, closest);
                float t = 1f - Mathf.Clamp01(dist / radius);
                float amount = damage * t;
                if (amount <= 0f)
                    continue;

                seg.ApplyDamage(amount);
            }

            if (explosionForce <= 0f)
                return;

            // Push any dynamic debris / remaining bodies in radius
            hits = Physics.OverlapSphere(worldPoint, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic)
                    continue;
                rb.AddExplosionForce(explosionForce, worldPoint, radius, 0.35f, ForceMode.Impulse);
            }
        }
    }
}
