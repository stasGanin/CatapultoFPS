using System;
using UnityEngine;

/// <summary>Gameplay → HUD feedback events (hit markers etc.) so weapons never reference UI.</summary>
public static class CombatFeedback
{
    public static event Action<Vector3> HitConfirmed;

    public static void RaiseHitConfirmed(Vector3 point) => HitConfirmed?.Invoke(point);
}
