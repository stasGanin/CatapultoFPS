using UnityEngine;

/// <summary>Player-castle rubble: shrinks away instead of staying as mineable loot.</summary>
public sealed class DebrisShrinkDespawn : MonoBehaviour
{
    const float DefaultDuration = 3f;

    Vector3 _startScale;
    float _duration = DefaultDuration;
    float _elapsed;
    bool _playing;

    public void Play(float duration = DefaultDuration)
    {
        _duration = Mathf.Max(0.1f, duration);
        _startScale = transform.localScale;
        _elapsed = 0f;
        _playing = true;
        enabled = true;
    }

    void Start()
    {
        if (!_playing)
            Play(DefaultDuration);
    }

    void Update()
    {
        if (!_playing)
            return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float k = 1f - t * t;
        transform.localScale = _startScale * Mathf.Max(0.02f, k);
        if (t < 1f)
            return;

        _playing = false;
        enabled = false;
        var chunk = GetComponent<CastleWallChunk>();
        if (chunk != null)
        {
            chunk.ParkForRepair();
            return;
        }

        Destroy(gameObject);
    }

    public void Cancel()
    {
        _playing = false;
        enabled = false;
        transform.localScale = _startScale;
    }
}
