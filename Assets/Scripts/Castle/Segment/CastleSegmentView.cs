using System;
using UnityEngine;

namespace Catapulto.Battle.Castle
{
    /// <summary>
    /// One castle part: HP + collider. On death hides this piece and spawns a pre-authored debris prefab
    /// (dynamic Rigidbodies on shards). Ported from Catapulto battle, stripped for FPS.
    /// </summary>
    public sealed class CastleSegmentView : MonoBehaviour, IDamageable
    {
        [SerializeField] int _segmentId;
        [SerializeField] string _displayName = "";
        [SerializeField] CastleSegmentRole _segmentRole = CastleSegmentRole.Normal;
        [SerializeField] UnityEngine.Object _weaponModule; // kept for prefab serialization compatibility
        [SerializeField] float _maxHealth = 100f;
        [SerializeField] bool _addBoxColliderIfMissing = true;
        [SerializeField] GameObject _destroyedPrefab;
        [SerializeField] Transform _debrisSpawnParent;
        [SerializeField] bool _matchDebrisWorldScale = true;
        [SerializeField] float _debrisSpawnScaleMultiplier = 1f;
        [SerializeField] bool _rigidbodyKinematicWhileIntact = true;
        [SerializeField] bool _preserveNonKinematicRigidbodyFromPrefab = true;
        [SerializeField] float _debrisImpulse;
        [SerializeField] bool _destroyGameObjectWhenDestroyed;
        [SerializeField] bool _receiveRigidbodyCollisionDamage = true;

        float _currentHealth;

        public int SegmentId => _segmentId;
        public CastleSegmentRole SegmentRole => _segmentRole;
        public bool IsEnemyCore => _segmentRole == CastleSegmentRole.EnemyCore;
        public string DisplayName =>
            string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName.Trim();
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDestroyed => _currentHealth <= 0f;

        public event Action<CastleSegmentView, float> Damaged;
        public event Action<CastleSegmentView> Destroyed;

        void Awake()
        {
            _currentHealth = _maxHealth;

            if (_segmentRole == CastleSegmentRole.EnemyCore)
                ConfigureAsStaticCore();

            if (_rigidbodyKinematicWhileIntact)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    bool skipForce = _preserveNonKinematicRigidbodyFromPrefab && !rb.isKinematic;
                    if (!skipForce)
                        rb.isKinematic = true;
                }
            }

            if (_addBoxColliderIfMissing && GetComponent<Collider>() == null &&
                GetComponentInChildren<Collider>() == null)
                gameObject.AddComponent<BoxCollider>();

            TryAddRigidbodyImpactReceiver();
        }

        void ConfigureAsStaticCore()
        {
            var bodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;
                ZeroOutRigidbodyMotion(rb);
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        void TryAddRigidbodyImpactReceiver()
        {
            if (_segmentRole == CastleSegmentRole.EnemyCore)
                return;
            if (!_receiveRigidbodyCollisionDamage)
                return;
            var rb = GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic)
                return;
            if (GetComponent<CastleSegmentRigidbodyImpact>() != null)
                return;
            gameObject.AddComponent<CastleSegmentRigidbodyImpact>();
        }

        public void ApplyDamage(float amount, in DamageInfo info)
        {
            ApplyDamage(amount);
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDestroyed)
                return;

            _currentHealth -= amount;
            Damaged?.Invoke(this, amount);

            if (_currentHealth > 0f)
                return;

            _currentHealth = 0f;
            Destroyed?.Invoke(this);
            SetDestroyedVisuals();
        }

        void SetDestroyedVisuals()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;

            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            StopPhysicsOnThisSegment();
            SpawnDestroyedPrefabIfConfigured();

            if (_destroyGameObjectWhenDestroyed)
                Destroy(gameObject);
        }

        void StopPhysicsOnThisSegment()
        {
            var bodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;
                ZeroOutRigidbodyMotion(rb);
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }

        static void ZeroOutRigidbodyMotion(Rigidbody rb)
        {
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
        }

        void SpawnDestroyedPrefabIfConfigured()
        {
            if (_destroyedPrefab == null)
                return;

            var instance = Instantiate(_destroyedPrefab, transform.position, transform.rotation, _debrisSpawnParent);
            ApplySpawnedDebrisScale(instance.transform);
            CastleDebrisImpactDamage.ConfigureSpawnedDebris(instance);

            if (_debrisImpulse <= 0f)
                return;

            Vector3 dir = (UnityEngine.Random.onUnitSphere + Vector3.up * 0.5f).normalized;
            var bodies = instance.GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body == null || body.isKinematic)
                    continue;
                body.AddForce(dir * _debrisImpulse, ForceMode.Impulse);
            }
        }

        void ApplySpawnedDebrisScale(Transform instance)
        {
            float mult = Mathf.Max(0.0001f, _debrisSpawnScaleMultiplier);

            if (!_matchDebrisWorldScale)
            {
                if (!Mathf.Approximately(mult, 1f))
                    instance.localScale = instance.localScale * mult;
                return;
            }

            Vector3 targetWorld = transform.lossyScale * mult;
            if (_debrisSpawnParent == null)
            {
                instance.localScale = targetWorld;
                return;
            }

            Vector3 p = _debrisSpawnParent.lossyScale;
            instance.localScale = new Vector3(
                SafeDiv(targetWorld.x, p.x),
                SafeDiv(targetWorld.y, p.y),
                SafeDiv(targetWorld.z, p.z));
        }

        static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;
    }
}
