using UnityEngine;

/// <summary>Единая точка визуала взрыва: частицы из пака + видимая сфера радиуса.</summary>
public static class ExplosionEffect
{
    const string ConfigPath = "Combat/ExplosionConfig";

    static ExplosionConfig _config;

    public static void Spawn(Vector3 point, float radius)
    {
        BlastRadiusFlash.Spawn(point, radius);

        if (_config == null)
            _config = Resources.Load<ExplosionConfig>(ConfigPath);
        if (_config == null)
        {
            Debug.LogError($"ExplosionConfig not found at Resources/{ConfigPath}");
            return;
        }
        if (_config.VfxPrefab == null)
            return;

        var fx = Object.Instantiate(_config.VfxPrefab, point, Quaternion.identity);
        fx.name = "ExplosionVfx";
        fx.transform.localScale = Vector3.one * (radius / _config.VfxBaseRadius);
        Object.Destroy(fx, _config.VfxLifetime);
    }
}
