using UnityEngine;

/// <summary>Enemy castle core — sorcerer heart. Killing it collapses the whole castle.</summary>
public sealed class EnemyCastleMage : MonoBehaviour, IDamageable
{
    [SerializeField] float _maxHealth = 120f;
    [SerializeField] EnemyCastle _castle;

    float _health;
    bool _dead;
    Color[] _baseColors;
    Renderer[] _renderers;

    public float Health => _health;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _dead;

    public void Bind(EnemyCastle castle, float maxHealth)
    {
        _castle = castle;
        _maxHealth = maxHealth;
        _health = maxHealth;
    }

    void Awake()
    {
        if (_health <= 0f)
            _health = _maxHealth;
        CacheRenderers();
    }

    void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].sharedMaterial != null && _renderers[i].sharedMaterial.HasProperty("_BaseColor"))
                _baseColors[i] = _renderers[i].sharedMaterial.GetColor("_BaseColor");
            else if (_renderers[i].sharedMaterial != null && _renderers[i].sharedMaterial.HasProperty("_Color"))
                _baseColors[i] = _renderers[i].sharedMaterial.GetColor("_Color");
            else
                _baseColors[i] = new Color(0.55f, 0.2f, 0.75f);
        }
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_dead || amount <= 0f)
            return;
        if (!info.FromPlayer)
            return;

        _health -= amount;
        Flash();

        if (_health > 0f)
            return;

        _dead = true;
        LootDrop.Mage(transform.position);
        if (_castle != null)
            _castle.OnMageKilled(info);
        else
            Destroy(gameObject);
    }

    void Flash()
    {
        if (_renderers == null || _renderers.Length == 0)
            CacheRenderers();

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", Color.white);
        block.SetColor("_Color", Color.white);
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].SetPropertyBlock(block);
        }

        CancelInvoke(nameof(RestoreColor));
        Invoke(nameof(RestoreColor), 0.08f);
    }

    void RestoreColor()
    {
        if (_renderers == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null)
                continue;
            var block = new MaterialPropertyBlock();
            Color c = _baseColors != null && i < _baseColors.Length ? _baseColors[i] : new Color(0.55f, 0.2f, 0.75f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            _renderers[i].SetPropertyBlock(block);
        }
    }
}
