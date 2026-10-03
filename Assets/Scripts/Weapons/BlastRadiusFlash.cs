using UnityEngine;

/// <summary>
/// Видимый радиус взрыва: расширяющаяся полупрозрачная сфера (работает и в билде)
/// и wire-гизмо в Scene view на весь срок жизни.
/// </summary>
public sealed class BlastRadiusFlash : MonoBehaviour
{
    const float Life = 0.6f;
    const float ExpandTime = 0.15f;
    static readonly Color FlashColor = new Color(1f, 0.45f, 0.1f, 0.35f);

    float _radius;
    float _age;
    MeshRenderer _renderer;
    Material _material;

    public static void Spawn(Vector3 point, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BlastRadiusFlash";
        go.transform.position = point;
        Destroy(go.GetComponent<Collider>());

        var flash = go.AddComponent<BlastRadiusFlash>();
        flash._radius = Mathf.Max(0.05f, radius);
        flash.SetupMaterial(go.GetComponent<MeshRenderer>());
        Destroy(go, Life);
    }

    void SetupMaterial(MeshRenderer rend)
    {
        _renderer = rend;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        _material = new Material(shader);
        // URP Unlit по умолчанию непрозрачный: без этого альфа игнорируется.
        _material.SetFloat("_Surface", 1f);
        _material.SetFloat("_Blend", 0f);
        _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _material.SetInt("_ZWrite", 0);
        _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        rend.sharedMaterial = _material;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ApplyVisual();
    }

    void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    void Update()
    {
        _age += Time.deltaTime;
        ApplyVisual();
    }

    void ApplyVisual()
    {
        float expand = Mathf.Clamp01(_age / ExpandTime);
        transform.localScale = Vector3.one * (_radius * 2f * expand);

        Color c = FlashColor;
        c.a = FlashColor.a * (1f - Mathf.Clamp01(_age / Life));
        _material.SetColor("_BaseColor", c);
        _material.SetColor("_Color", c);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 1f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
