using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>On fast collision, applies impact damage to colliding castle segments.</summary>
    [DisallowMultipleComponent]
    public sealed class CastleDebrisImpactDamage : MonoBehaviour
    {
        [SerializeField] CastleImpactSettings _settings;

        static bool _warnedMissing;

        Rigidbody _rigidbody;

        void Awake()
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();
            if (_settings == null)
                _settings = CastleImpactContext.Settings;
        }

        public void Initialize(Rigidbody rb, CastleImpactSettings data)
        {
            _rigidbody = rb != null ? rb : GetComponent<Rigidbody>();
            _settings = data;
        }

        CastleImpactSettings ResolveSettings() =>
            _settings != null ? _settings : CastleImpactContext.Settings;

        public static void ConfigureSpawnedDebris(GameObject debrisRoot)
        {
            if (debrisRoot == null)
                return;

            var data = CastleImpactContext.Settings;
            int wired = 0;
            var bodies = debrisRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body == null || body.isKinematic)
                    continue;
                var go = body.gameObject;
                var dmg = go.GetComponent<CastleDebrisImpactDamage>();
                if (dmg == null)
                    dmg = go.AddComponent<CastleDebrisImpactDamage>();
                dmg.Initialize(body, data);
                wired++;
            }

            if (wired == 0)
                Debug.LogWarning($"[Castle] ConfigureSpawnedDebris: no dynamic Rigidbodies under '{debrisRoot.name}'.");
        }

        void OnCollisionEnter(Collision collision)
        {
            var data = ResolveSettings();
            if (data == null || _rigidbody == null)
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning("[Castle] Debris impact skipped — no CastleImpactSettings (Resources/Castle/ImpactSettings).");
                }
                return;
            }

            CastleImpactPhysics.ApplyCollisionImpulseDamage(_rigidbody, collision, data);
        }
    }
}
