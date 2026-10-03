using UnityEngine;

/// <summary>
/// Shot origin. Local +Z is the bolt direction. Move this empty in the weapon prefab.
/// </summary>
public sealed class WeaponMuzzle : MonoBehaviour
{
    const float GizmoLength = 0.45f;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * GizmoLength);
        Gizmos.DrawWireSphere(transform.position, 0.02f);
    }
}
