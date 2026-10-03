using UnityEngine;

/// <summary>Launch-velocity solver and arc clearance check for lobbed enemy projectiles.</summary>
public static class EnemyBallistics
{
    const int ArcSegments = 10;
    const float ArcProbeRadius = 0.15f;

    /// <summary>
    /// Скорость вылета, чтобы при заданной скорости попасть в target под гравитацией.
    /// high=false — настильная дуга, high=true — навесная (через стены). False, если цель вне досягаемости.
    /// </summary>
    public static bool TrySolve(Vector3 origin, Vector3 target, float speed, bool high, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        float g = -Physics.gravity.y;
        Vector3 planar = EnemySenses.Flatten(target - origin);
        float x = planar.magnitude;
        float y = target.y - origin.y;
        if (x < 0.1f || g <= 0f)
            return false;

        float v2 = speed * speed;
        float discriminant = v2 * v2 - g * (g * x * x + 2f * y * v2);
        if (discriminant < 0f)
            return false;

        float root = Mathf.Sqrt(discriminant);
        float angle = Mathf.Atan((v2 + (high ? root : -root)) / (g * x));
        velocity = planar / x * (Mathf.Cos(angle) * speed) + Vector3.up * (Mathf.Sin(angle) * speed);
        return true;
    }

    /// <summary>Проходит ли дуга до цели, ни во что не упираясь по дороге.</summary>
    public static bool ArcIsClear(Vector3 origin, Vector3 velocity, Vector3 target, Transform self, Transform targetRoot)
    {
        Vector3 planar = EnemySenses.Flatten(target - origin);
        float planarSpeed = EnemySenses.Flatten(velocity).magnitude;
        if (planarSpeed < 0.01f)
            return false;

        float flightTime = planar.magnitude / planarSpeed;
        Vector3 gravity = Physics.gravity;
        Vector3 previous = origin;
        // Последний отрезок не проверяем: у цели бомба и должна во что-то врезаться.
        for (int i = 1; i < ArcSegments; i++)
        {
            float t = flightTime * i / ArcSegments;
            Vector3 point = origin + velocity * t + 0.5f * gravity * t * t;
            if (!EnemySenses.HasClearPath(previous, point, ArcProbeRadius, self, targetRoot))
                return false;
            previous = point;
        }

        return true;
    }
}
