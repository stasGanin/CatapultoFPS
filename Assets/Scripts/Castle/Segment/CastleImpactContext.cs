using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>Optional scene registry for impact settings. Falls back to Resources/Castle/ImpactSettings.</summary>
    public sealed class CastleImpactContext : MonoBehaviour
    {
        [SerializeField] CastleImpactSettings _settings;

        static CastleImpactSettings _runtime;

        public static CastleImpactSettings Settings
        {
            get
            {
                if (_runtime != null)
                    return _runtime;
                _runtime = Resources.Load<CastleImpactSettings>("Castle/ImpactSettings");
                return _runtime;
            }
        }

        void Awake()
        {
            if (_settings != null)
                _runtime = _settings;
        }

        void OnDestroy()
        {
            if (_runtime == _settings)
                _runtime = null;
        }
    }
}
