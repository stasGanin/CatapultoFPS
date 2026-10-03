using UnityEngine;

/// <summary>Упреждение по падению ядра: физическое ядро летит под гравитацией, а башня целится прямой линией.</summary>
public static class TowerBallistics
{
    public static Vector3 LobDirection(Vector3 muzzle, Vector3 target, float muzzleSpeed)
    {
        float flightTime = Vector3.Distance(muzzle, target) / Mathf.Max(1f, muzzleSpeed);
        Vector3 compensated = target - Physics.gravity * (0.5f * flightTime * flightTime);
        return (compensated - muzzle).normalized;
    }
}
