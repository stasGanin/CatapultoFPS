using UnityEngine;

/// <summary>Общий визуал любого взрыва (<see cref="ExplosionEffect"/>). Грузится из Resources/Combat.</summary>
[CreateAssetMenu(menuName = "Catapulto/Explosion Config", fileName = "ExplosionConfig")]
public sealed class ExplosionConfig : ScriptableObject
{
    [SerializeField] GameObject _vfxPrefab;
    [Tooltip("Радиус взрыва, под который сделан префаб эффекта при масштабе 1.")]
    [SerializeField, Min(0.1f)] float _vfxBaseRadius = 2f;
    [SerializeField, Min(0.1f)] float _vfxLifetime = 2f;

    public GameObject VfxPrefab => _vfxPrefab;
    public float VfxBaseRadius => _vfxBaseRadius;
    public float VfxLifetime => _vfxLifetime;
}
