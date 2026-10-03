using UnityEngine;

/// <summary>Short expanding wire sphere at a cannon blast. Scene Gizmos + Game debug lines.</summary>
public sealed class BlastRadiusFlash : MonoBehaviour
{
    const float Life = 0.35f;
    float _radius;
    float _age;

    public static void Spawn(Vector3 point, float radius)
    {
        var go = new GameObject("BlastRadiusFlash");
        go.transform.position = point;
        var flash = go.AddComponent<BlastRadiusFlash>();
        flash._radius = Mathf.Max(0.05f, radius);
        Object.Destroy(go, Life);
    }

    void Update()
    {
        _age += Time.deltaTime;
        DrawCircle(transform.position, ShownRadius(), new Color(1f, 0.45f, 0.1f, 1f));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 1f);
        Gizmos.DrawWireSphere(transform.position, ShownRadius());
    }

    float ShownRadius()
    {
        float t = Mathf.Clamp01(_age / 0.12f);
        return _radius * t;
    }

    static void DrawCircle(Vector3 center, float radius, Color color)
    {
        const int steps = 32;
        Vector3 prev = center + Vector3.right * radius;
        for (int i = 1; i <= steps; i++)
        {
            float a = (i / (float)steps) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Debug.DrawLine(prev, next, color, Time.deltaTime, false);
            prev = next;
        }

        prev = center + Vector3.right * radius;
        for (int i = 1; i <= steps; i++)
        {
            float a = (i / (float)steps) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            Debug.DrawLine(prev, next, color, Time.deltaTime, false);
            prev = next;
        }
    }
}
