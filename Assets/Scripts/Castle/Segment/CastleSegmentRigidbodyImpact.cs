using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>Dynamic castle modules: impact damage from collisions.</summary>
    [DisallowMultipleComponent]
    public sealed class CastleSegmentRigidbodyImpact : MonoBehaviour
    {
        [SerializeField] CastleImpactSettings _settings;

        CastleSegmentView _segment;
        Rigidbody _rigidbody;
        static bool _warnedMissing;

        void Awake()
        {
            _segment = GetComponent<CastleSegmentView>();
            _rigidbody = GetComponent<Rigidbody>();
            if (_settings == null)
                _settings = CastleImpactContext.Settings;
        }

        void Start()
        {
            if (GetComponent<CastleDebrisImpactDamage>() != null)
            {
                Destroy(this);
                return;
            }

            if (_segment == null || _rigidbody == null)
                Destroy(this);
        }

        CastleImpactSettings ResolveSettings() =>
            _settings != null ? _settings : CastleImpactContext.Settings;

        void OnCollisionEnter(Collision collision)
        {
            if (_segment == null || _segment.IsDestroyed || _rigidbody == null || _rigidbody.isKinematic)
                return;

            var data = ResolveSettings();
            if (data == null)
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning("[Castle] Segment rigidbody impact skipped — no CastleImpactSettings.");
                }
                return;
            }

            CastleImpactPhysics.ApplyCollisionImpulseDamage(_rigidbody, collision, data);
        }
    }
}
