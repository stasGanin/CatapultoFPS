using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>Shared impact speed math and bilateral damage for debris collisions.</summary>
    public static class CastleImpactPhysics
    {
        public static Vector3 GetRigidbodyVelocity(Rigidbody rb) => rb.linearVelocity;

        public static float ComputeClosureSpeed(Collision collision, Rigidbody selfRb)
        {
            float rel = collision.relativeVelocity.magnitude;
            if (rel >= 0.35f)
                return rel;

            var otherRb = collision.collider.attachedRigidbody;
            Vector3 vSelf = GetRigidbodyVelocity(selfRb);
            if (otherRb == null)
                return Mathf.Max(rel, vSelf.magnitude);

            return Mathf.Max(rel, (vSelf - GetRigidbodyVelocity(otherRb)).magnitude);
        }

        public static CastleSegmentView SegmentOwningRigidbody(Rigidbody rb)
        {
            if (rb == null)
                return null;
            return rb.GetComponent<CastleSegmentView>() ?? rb.GetComponentInParent<CastleSegmentView>();
        }

        public static void ApplyCollisionImpulseDamage(Rigidbody selfRb, Collision collision, CastleImpactSettings data)
        {
            if (selfRb == null || collision == null || collision.collider == null || data == null)
                return;

            var otherRb = collision.collider.attachedRigidbody;
            if (!IsCollisionPairAuthority(selfRb, otherRb))
                return;

            float speed = ComputeClosureSpeed(collision, selfRb);
            float damage = data.ComputeImpactDamage(speed);
            var selfSeg = SegmentOwningRigidbody(selfRb);
            var otherSeg = collision.collider.GetComponentInParent<CastleSegmentView>();

            if (data.DebugLogDebrisImpacts)
            {
                Debug.Log(
                    $"[CastleImpact] '{selfRb.name}' vs '{collision.collider.name}' " +
                    $"closure={speed:0.###} m/s dmg={damage:0.###}");
            }

            if (damage <= 0f)
                return;

            if (CanSegmentReceiveImpact(data, selfSeg))
                selfSeg.ApplyDamage(damage);
            if (CanSegmentReceiveImpact(data, otherSeg) && otherSeg != selfSeg)
                otherSeg.ApplyDamage(damage);
        }

        static bool IsCollisionPairAuthority(Rigidbody selfRb, Rigidbody otherRb)
        {
            if (selfRb.isKinematic)
                return false;
            if (otherRb == null || otherRb.isKinematic)
                return true;
            return selfRb.GetInstanceID() < otherRb.GetInstanceID();
        }

        static bool CanSegmentReceiveImpact(CastleImpactSettings data, CastleSegmentView seg)
        {
            return seg != null && !seg.IsDestroyed && data.LayerCanReceiveImpactDamage(seg.gameObject.layer);
        }
    }
}
