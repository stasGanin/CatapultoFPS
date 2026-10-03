using UnityEngine;

/// <summary>Short expanding magic sphere when an enemy castle heart dies.</summary>
public sealed class CastleMagicBlast : MonoBehaviour
{
    float _radius;
    float _age;
    const float Life = 0.85f;

    public static void Spawn(Vector3 center, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CastleMagicBlast";
        go.transform.position = center;
        go.transform.localScale = Vector3.one * 0.2f;
        Object.Destroy(go.GetComponent<Collider>());

        var flash = go.AddComponent<CastleMagicBlast>();
        flash._radius = Mathf.Max(4f, radius);

        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null)
                rend.sharedMaterial = new Material(shader);
            var block = new MaterialPropertyBlock();
            Color c = new Color(0.7f, 0.25f, 1f, 0.55f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            rend.SetPropertyBlock(block);
        }

        Object.Destroy(go, Life);
    }

    void Update()
    {
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / Life);
        float s = Mathf.Lerp(0.4f, _radius * 2f, 1f - Mathf.Pow(1f - t, 2f));
        transform.localScale = Vector3.one * s;

        var rend = GetComponent<MeshRenderer>();
        if (rend == null)
            return;
        var block = new MaterialPropertyBlock();
        Color c = new Color(0.75f, 0.3f, 1f, Mathf.Lerp(0.55f, 0f, t));
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        rend.SetPropertyBlock(block);
    }
}
