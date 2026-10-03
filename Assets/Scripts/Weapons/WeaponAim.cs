using UnityEngine;

/// <summary>Camera-center aim: hits go to the crosshair, projectiles still spawn at the muzzle.</summary>
public static class WeaponAim
{
    const QueryTriggerInteraction Triggers = QueryTriggerInteraction.Ignore;
    static readonly RaycastHit[] Hits = new RaycastHit[24];

    public static Vector3 GetPoint(Camera camera, GameObject owner, float range)
    {
        if (camera == null)
            return Vector3.zero;

        Transform cam = camera.transform;
        Vector3 origin = cam.position;
        Vector3 forward = cam.forward;
        float maxRange = Mathf.Max(1f, range);

        int count = Physics.RaycastNonAlloc(origin, forward, Hits, maxRange, ~0, Triggers);
        float bestDist = float.PositiveInfinity;
        Vector3 bestPoint = origin + forward * maxRange;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null || hit.distance < 0.02f)
                continue;
            if (IsOwnedBy(hit.collider, owner))
                continue;
            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestPoint = hit.point;
            }
        }

        return bestPoint;
    }

    public static Vector3 GetDirection(Vector3 muzzle, Vector3 aimPoint, Vector3 cameraForward)
    {
        Vector3 toAim = aimPoint - muzzle;
        if (toAim.sqrMagnitude < 0.0001f)
            return cameraForward.sqrMagnitude > 0.01f ? cameraForward.normalized : Vector3.forward;

        Vector3 dir = toAim.normalized;
        if (Vector3.Dot(dir, cameraForward) < 0.15f)
            return cameraForward.normalized;
        return dir;
    }

    static bool IsOwnedBy(Collider col, GameObject owner)
    {
        if (owner == null || col == null)
            return false;
        Transform t = col.transform;
        return t == owner.transform || t.IsChildOf(owner.transform);
    }
}
