using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>Impact damage tuning for debris / segment collisions (FPS stand-in for BattleGameData).</summary>
    [CreateAssetMenu(fileName = "CastleImpactSettings", menuName = "Catapulto/Castle/Impact Settings")]
    public sealed class CastleImpactSettings : ScriptableObject
    {
        [SerializeField] float _impactMinRelativeSpeed = 1.5f;
        [SerializeField] float _impactDamagePerSpeedUnit = 1.5f;
        [SerializeField] float _impactDamageMax = 40f;
        [SerializeField] LayerMask _impactDamageableLayers = ~0;
        [SerializeField] bool _debugLogDebrisImpacts;

        public bool DebugLogDebrisImpacts => _debugLogDebrisImpacts;
        public float ImpactMinRelativeSpeed => _impactMinRelativeSpeed;
        public float ImpactDamagePerSpeedUnit => _impactDamagePerSpeedUnit;
        public float ImpactDamageMax => _impactDamageMax;
        public LayerMask ImpactDamageableLayers => _impactDamageableLayers;

        public float ComputeImpactDamage(float relativeSpeedMagnitude)
        {
            if (_impactDamagePerSpeedUnit <= 0f || relativeSpeedMagnitude < _impactMinRelativeSpeed)
                return 0f;
            float excess = relativeSpeedMagnitude - _impactMinRelativeSpeed;
            return Mathf.Min(_impactDamageMax, excess * _impactDamagePerSpeedUnit);
        }

        public bool LayerCanReceiveImpactDamage(int layer)
        {
            int bits = _impactDamageableLayers.value;
            if (bits == ~0 || bits == -1)
                return true;
            return (bits & (1 << layer)) != 0;
        }
    }
}
