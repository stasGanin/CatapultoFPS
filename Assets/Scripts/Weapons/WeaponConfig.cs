using UnityEngine;

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "Catapulto/Weapons/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("Fire")]
    [SerializeField, Min(0.05f)] float _shotsPerSecond = 1f;
    [SerializeField] bool _automatic;

    [Header("Cannonball")]
    [SerializeField, Min(0.05f)] float _projectileRadius = 0.22f;
    [SerializeField, Min(0.1f)] float _projectileMass = 8f;
    [SerializeField, Min(1f)] float _muzzleSpeed = 45f;
    [SerializeField, Min(0.1f)] float _blastRadius = 0.95f;
    [SerializeField, Min(0f)] float _explosionForce = 650f;
    [SerializeField, Min(0.5f)] float _projectileLifetime = 8f;

    [Header("Aim")]
    [SerializeField, Min(5f)] float _aimRange = 80f;
    [SerializeField, Min(5f)] float _autoRange = 70f;

    [Header("VFX")]
    [SerializeField] GameObject _projectileVisual;
    [SerializeField] GameObject _impactVisual;
    [SerializeField] GameObject _muzzleFlash;

    public float ShotsPerSecond => _shotsPerSecond;
    public bool Automatic => _automatic;
    public float FireInterval => 1f / _shotsPerSecond;
    public float ProjectileRadius => _projectileRadius;
    public float ProjectileMass => _projectileMass;
    public float MuzzleSpeed => _muzzleSpeed;
    public float BlastRadius => _blastRadius;
    public float ExplosionForce => _explosionForce;
    public float ProjectileLifetime => _projectileLifetime;
    public float AimRange => _aimRange;
    /// <summary>Дальность поиска цели у башни в автоматическом режиме.</summary>
    public float AutoRange => _autoRange;
    public GameObject ProjectileVisual => _projectileVisual;
    public GameObject ImpactVisual => _impactVisual;
    public GameObject MuzzleFlash => _muzzleFlash;
}
