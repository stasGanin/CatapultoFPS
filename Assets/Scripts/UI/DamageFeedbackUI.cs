using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Incoming-damage feedback: red screen edges, arcs pointing at the damage source, death overlay.
/// </summary>
public sealed class DamageFeedbackUI : MonoBehaviour
{
    const int SortingOrder = 48;
    const float EdgeFadeTime = 0.6f;
    const float EdgeMaxAlpha = 0.55f;
    const float EdgeThickness = 90f;
    const float ArrowFadeTime = 1.1f;
    const float ArrowRadius = 150f;
    const int ArrowPool = 4;
    // Сильный удар (≥ этой доли HP) даёт полную вспышку краёв; чип-урон — едва заметную.
    const float FullFlashDamageFraction = 0.2f;
    static readonly Color EdgeColor = new Color(0.75f, 0.05f, 0.03f, 1f);
    static readonly Color ArrowColor = new Color(1f, 0.25f, 0.15f, 1f);

    PlayerHealth _health;
    PlayerRespawn _respawn;
    Transform _camera;
    Image[] _edges;
    RectTransform[] _arrows;
    Image[] _arrowImages;
    Vector3[] _arrowSources;
    float[] _arrowTimes;
    int _nextArrow;
    float _edgeStrength;
    GameObject _deathOverlay;
    Text _deathText;

    void Awake()
    {
        Build();
    }

    // PlayerHealth/PlayerRespawn вешает другой бутстрап, порядок AfterSceneLoad не гарантирован — берём в Start.
    void Start()
    {
        _health = GetComponent<PlayerHealth>();
        _respawn = GetComponent<PlayerRespawn>();
        var cam = GetComponentInChildren<Camera>();
        _camera = cam != null ? cam.transform : transform;
        if (_health != null)
            _health.Damaged += OnDamaged;
        else
            Debug.LogError("DamageFeedbackUI: no PlayerHealth on player.", this);
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.Damaged -= OnDamaged;
    }

    void OnDamaged(float amount, DamageInfo info)
    {
        float fraction = amount / Mathf.Max(1f, _health.MaxHealth);
        _edgeStrength = Mathf.Max(_edgeStrength, Mathf.Clamp01(0.35f + fraction / FullFlashDamageFraction));

        // Источник — точка попадания минус направление полёта: так стрелку видно и для снарядов.
        Vector3 source = info.Point - info.Direction.normalized * 4f;
        _arrowSources[_nextArrow] = source;
        _arrowTimes[_nextArrow] = Time.time;
        _nextArrow = (_nextArrow + 1) % ArrowPool;
    }

    void LateUpdate()
    {
        if (_health == null)
            return;
        _edgeStrength = Mathf.MoveTowards(_edgeStrength, 0f, Time.deltaTime / EdgeFadeTime);
        float lowHealth = !_health.IsDead
            ? Mathf.Clamp01(1f - _health.Health / Mathf.Max(1f, _health.MaxHealth * 0.3f)) * 0.35f
            : 0f;
        float alpha = Mathf.Max(_edgeStrength, lowHealth) * EdgeMaxAlpha;
        for (int i = 0; i < _edges.Length; i++)
            _edges[i].color = new Color(EdgeColor.r, EdgeColor.g, EdgeColor.b, alpha);

        for (int i = 0; i < ArrowPool; i++)
            UpdateArrow(i);

        bool dead = _respawn != null && _respawn.IsWaiting;
        _deathOverlay.SetActive(dead);
        if (dead)
            _deathText.text = $"YOU HAVE FALLEN\n<size=26>Respawning at your mage in {Mathf.CeilToInt(_respawn.SecondsLeft)}</size>";
    }

    void UpdateArrow(int i)
    {
        float age = Time.time - _arrowTimes[i];
        bool visible = age < ArrowFadeTime;
        _arrows[i].gameObject.SetActive(visible);
        if (!visible)
            return;

        Vector3 to = _arrowSources[i] - _camera.position;
        Vector3 local = _camera.InverseTransformDirection(to);
        float angle = Mathf.Atan2(local.x, local.z);
        _arrows[i].anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * ArrowRadius;
        _arrows[i].localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
        Color c = ArrowColor;
        c.a = 1f - age / ArrowFadeTime;
        _arrowImages[i].color = c;
    }

    void Build()
    {
        Canvas canvas = UiFactory.CreateCanvas(transform, "DamageFeedbackCanvas", SortingOrder);
        Transform root = canvas.transform;

        _edges = new[]
        {
            Edge(root, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -EdgeThickness), Vector2.zero),
            Edge(root, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, EdgeThickness)),
            Edge(root, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(EdgeThickness, 0f)),
            Edge(root, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-EdgeThickness, 0f), Vector2.zero),
        };

        _arrows = new RectTransform[ArrowPool];
        _arrowImages = new Image[ArrowPool];
        _arrowSources = new Vector3[ArrowPool];
        _arrowTimes = new float[ArrowPool];
        for (int i = 0; i < ArrowPool; i++)
        {
            _arrowTimes[i] = float.NegativeInfinity;
            _arrows[i] = UiFactory.CreateRect(root, "DamageArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 12f));
            _arrowImages[i] = _arrows[i].gameObject.AddComponent<Image>();
            _arrowImages[i].raycastTarget = false;
            _arrows[i].gameObject.SetActive(false);
        }

        _deathOverlay = UiFactory.CreateFill(root, "DeathOverlay", new Color(0.08f, 0f, 0f, 0.72f)).gameObject;
        _deathText = UiFactory.CreateText(_deathOverlay.transform, "DeathText", 54, new Color(0.98f, 0.9f, 0.75f), TextAnchor.MiddleCenter);
        _deathText.supportRichText = true;
        _deathText.fontStyle = FontStyle.Bold;
        _deathOverlay.SetActive(false);
    }

    static Image Edge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Edge" + name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }
}
