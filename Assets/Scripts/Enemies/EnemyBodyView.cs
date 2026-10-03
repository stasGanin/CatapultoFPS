using UnityEngine;

/// <summary>
/// Placeholder-body feedback shared by all enemies: base tint, attack telegraph glow/pulse, hit flash.
/// </summary>
public sealed class EnemyBodyView
{
    const float HitFlashTime = 0.08f;
    const float PulseAmplitude = 0.2f;
    const float PulseHz = 6f;
    const float GlowIntensity = 2.5f;
    static readonly Color TelegraphColor = new Color(1f, 0.85f, 0.35f);

    readonly Transform _transform;
    readonly Renderer _renderer;
    readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
    readonly Color _baseColor;
    readonly Vector3 _baseScale;
    float _flashUntil;

    public EnemyBodyView(Transform transform, Color baseColor, Vector3 baseScale)
    {
        _transform = transform;
        _renderer = transform.GetComponent<Renderer>();
        _baseColor = baseColor;
        _baseScale = baseScale;
        ApplyMaterial();
    }

    public void Flash() => _flashUntil = Time.time + HitFlashTime;

    /// <summary>charge 0..1 — насколько близок удар; на 1 враг ярко светится и пульсирует.</summary>
    public void Tick(float charge)
    {
        charge = Mathf.Clamp01(charge);
        if (_renderer != null)
        {
            Color color = Time.time < _flashUntil ? Color.white : Color.Lerp(_baseColor, TelegraphColor, charge);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _block.SetColor("_EmissionColor", TelegraphColor * (charge * GlowIntensity));
            _renderer.SetPropertyBlock(_block);
        }

        float pulse = 1f + PulseAmplitude * charge * (0.6f + 0.4f * Mathf.Sin(Time.time * PulseHz * Mathf.PI * 2f));
        _transform.localScale = _baseScale * pulse;
    }

    void ApplyMaterial()
    {
        if (_renderer == null)
            return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;
        var material = new Material(shader);
        material.EnableKeyword("_EMISSION");
        _renderer.sharedMaterial = material;
        Tick(0f);
    }
}
