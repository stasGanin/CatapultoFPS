using UnityEngine;

/// <summary>Всплывающая цифра урона в мире: летит вверх, затухает, всегда смотрит на камеру.</summary>
public sealed class DamageNumbers : MonoBehaviour
{
    const float Life = 0.9f;
    const float RiseSpeed = 1.6f;
    const float SidewaysJitter = 0.35f;
    // Размер задан через расстояние, чтобы цифра читалась и вблизи, и на дальних башнях.
    const float ScalePerMeter = 0.08f;
    const float MinScale = 0.12f;
    const float MaxScale = 1.2f;
    const int FontSize = 64;

    static Font _font;

    TextMesh _text;
    Color _baseColor;
    float _age;
    Camera _camera;

    public static void Spawn(Vector3 point, float amount)
    {
        if (!GameSettings.ShowDamageNumbers || amount <= 0f)
            return;

        var go = new GameObject("DamageNumber");
        Vector3 jitter = new Vector3(
            Random.Range(-SidewaysJitter, SidewaysJitter), 0f, Random.Range(-SidewaysJitter, SidewaysJitter));
        go.transform.position = point + jitter;
        go.AddComponent<DamageNumbers>().Init(Mathf.CeilToInt(amount));
        Destroy(go, Life);
    }

    void Init(int value)
    {
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _text = gameObject.AddComponent<TextMesh>();
        _text.font = _font;
        _text.fontSize = FontSize;
        _text.characterSize = 0.1f;
        _text.anchor = TextAnchor.MiddleCenter;
        _text.alignment = TextAlignment.Center;
        _text.fontStyle = FontStyle.Bold;
        _text.text = value.ToString();
        GetComponent<MeshRenderer>().sharedMaterial = _font.material;

        _baseColor = new Color(1f, 0.9f, 0.3f, 1f);
        _text.color = _baseColor;
        _camera = Camera.main;
        FaceCamera();
    }

    void Update()
    {
        _age += Time.deltaTime;
        transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

        Color c = _baseColor;
        c.a = 1f - Mathf.Clamp01(_age / Life);
        _text.color = c;
        FaceCamera();
    }

    void FaceCamera()
    {
        if (_camera == null)
            return;
        Transform cam = _camera.transform;
        transform.rotation = cam.rotation;
        float distance = Vector3.Distance(cam.position, transform.position);
        transform.localScale = Vector3.one * Mathf.Clamp(distance * ScalePerMeter, MinScale, MaxScale);
    }
}
