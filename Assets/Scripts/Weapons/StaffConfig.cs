using UnityEngine;

[CreateAssetMenu(fileName = "StaffConfig", menuName = "Catapulto/Weapons/Staff Config")]
public sealed class StaffConfig : ScriptableObject
{
    [Header("Channel")]
    [SerializeField, Min(0.05f)] float _raiseDuration = 0.5f;
    [SerializeField, Min(0.05f)] float _lowerDuration = 0.35f;
    [SerializeField, Min(0.2f)] float _missilesPerSecond = 1f;
    [SerializeField, Min(0f)] float _manaCostPerMissile = 12f;

    [Header("Seek")]
    [SerializeField, Min(4f)] float _searchRadius = 32f;
    [SerializeField, Min(1f)] float _missileSpeed = 16f;
    [SerializeField, Min(0.1f)] float _missileDamage = 10f;
    [SerializeField, Min(0.5f)] float _missileLifetime = 5f;
    [SerializeField, Min(0.04f)] float _missileRadius = 0.16f;

    [Header("VFX")]
    [SerializeField] GameObject _projectileVisual;
    [SerializeField] GameObject _impactVisual;
    [SerializeField] GameObject _muzzleFlash;

    public float RaiseDuration => _raiseDuration;
    public float LowerDuration => _lowerDuration;
    public float MissilesPerSecond => _missilesPerSecond;
    public float FireInterval => 1f / Mathf.Max(0.2f, _missilesPerSecond);
    public float ManaCostPerMissile => _manaCostPerMissile;
    public float SearchRadius => _searchRadius;
    public float MissileSpeed => _missileSpeed;
    public float MissileDamage => _missileDamage;
    public float MissileLifetime => _missileLifetime;
    public float MissileRadius => _missileRadius;
    public GameObject ProjectileVisual => _projectileVisual;
    public GameObject ImpactVisual => _impactVisual;
    public GameObject MuzzleFlash => _muzzleFlash;
}
